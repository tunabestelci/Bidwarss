"""Bidwarss verified-server leaderboard. Python 3.10+, standard library only.
Never distribute BIDWARSS_SCORE_SECRET to game clients or player-hosted rooms.

Environment:
  BIDWARSS_SCORE_SECRET     32+ character shared secret (required)
  BIDWARSS_ALLOWED_RULES    comma separated rules hashes accepted for new runs (required)
  BIDWARSS_SCORE_DB         SQLite file, default scores.sqlite3
  BIDWARSS_SCORE_BIND       bind address, default 127.0.0.1
  PORT                      listen port, default 8787
  BIDWARSS_BLOCKED_WORDS    optional comma separated words rejected in team names
  BIDWARSS_MAX_CONNECTIONS  concurrent connection cap, default 64
  BIDWARSS_READ_LIMIT       GET requests per client per minute, default 240 (0 = unlimited)
  BIDWARSS_WRITE_LIMIT      POST requests per client per minute, default 60 (0 = unlimited)
  BIDWARSS_TRUST_PROXY      set to 1 behind a reverse proxy that appends X-Forwarded-For
  BIDWARSS_TLS_CERT / BIDWARSS_TLS_KEY   optional PEM files to serve HTTPS directly
"""
import collections
import contextlib
import datetime
import hashlib
import hmac
import json
import os
import re
import sqlite3
import ssl
import threading
import time
import unicodedata
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HEX32 = re.compile(r"^[a-f0-9]{32}$")
HEX64 = re.compile(r"^[a-f0-9]{64}$")
# What the game's DateTime.ToString("O") produces for a UTC time, e.g. 2026-10-10T20:43:00.1234567Z
ISO_UTC = re.compile(r"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})?$")


def normalise(text):
    """Case/width-folded form used to compare team names against the block list."""
    return unicodedata.normalize("NFKC", text).casefold()


class RateLimiter:
    """Sliding-window limiter keyed by client address. limit <= 0 disables it."""

    def __init__(self, limit, window=60.0):
        self.limit, self.window = int(limit), float(window)
        self.hits = {}
        self.lock = threading.Lock()

    def allow(self, key, now=None):
        if self.limit <= 0:
            return True
        now = time.monotonic() if now is None else now
        with self.lock:
            queue = self.hits.setdefault(key, collections.deque())
            while queue and now - queue[0] > self.window:
                queue.popleft()
            if len(queue) >= self.limit:
                return False
            queue.append(now)
            if len(self.hits) > 10000:
                for stale in [k for k, v in self.hits.items() if not v or now - v[-1] > self.window]:
                    del self.hits[stale]
            return True


class ScoreStore:
    def __init__(self, path, secret, allowed_rules, blocked_words=None):
        if len(secret) < 32 or not allowed_rules or any(not HEX64.fullmatch(r) for r in allowed_rules):
            raise ValueError(
                "A 32+ character server secret and allowed rules hashes are required. "
                "Get the hash from the Unity Console line 'Leaderboard rules hash:' "
                "(Bidwarss > Print Leaderboard Rules Hash, or the dedicated server's startup log).")
        self.path, self.secret, self.allowed_rules = str(path), secret.encode(), set(allowed_rules)
        self.blocked = [normalise(w) for w in (blocked_words or []) if w and w.strip()]
        with self.session() as db:
            db.executescript("""
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS runs (
                    run_id TEXT PRIMARY KEY, rules_hash TEXT NOT NULL, player_count INTEGER NOT NULL,
                    seed INTEGER NOT NULL, total_dollars INTEGER NOT NULL, elapsed_milliseconds INTEGER NOT NULL,
                    body TEXT NOT NULL, body_hash TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS board ON runs(rules_hash, player_count, total_dollars DESC, elapsed_milliseconds);
                CREATE TABLE IF NOT EXISTS nonces(nonce TEXT PRIMARY KEY, seen INTEGER NOT NULL);
            """)

    @contextlib.contextmanager
    def session(self):
        """A connection that is always committed or rolled back, and always closed."""
        db = sqlite3.connect(self.path, timeout=10)
        try:
            yield db
            db.commit()
        except BaseException:
            db.rollback()
            raise
        finally:
            db.close()

    def healthy(self):
        try:
            with self.session() as db:
                db.execute("SELECT 1 FROM runs LIMIT 1").fetchall()
            return True
        except sqlite3.Error:
            return False

    def accept(self, raw, timestamp, nonce, signature):
        try:
            stamp = int(timestamp)
        except (ValueError, TypeError):
            return 401, {"error": "invalid authentication"}
        if abs(int(time.time()) - stamp) > 300 or not HEX32.fullmatch(nonce or ""):
            return 401, {"error": "expired or invalid authentication"}
        expected = hmac.new(self.secret, timestamp.encode() + b"\n" + nonce.encode() + b"\n" + raw, hashlib.sha256).hexdigest()
        if not HEX64.fullmatch(signature or "") or not hmac.compare_digest(expected, signature):
            return 401, {"error": "invalid signature"}
        try:
            data = json.loads(raw)
            self.validate(data)
        except (ValueError, TypeError, KeyError, UnicodeDecodeError):
            return 422, {"error": "invalid completed run or unsupported rules"}
        canonical = json.dumps(data, sort_keys=True, ensure_ascii=False, separators=(",", ":"))
        digest = hashlib.sha256(canonical.encode()).hexdigest()
        with self.session() as db:
            db.execute("BEGIN IMMEDIATE")
            db.execute("DELETE FROM nonces WHERE seen < ?", (int(time.time()) - 600,))
            if db.execute("SELECT 1 FROM nonces WHERE nonce=?", (nonce,)).fetchone():
                return 409, {"error": "replayed request"}
            db.execute("INSERT INTO nonces VALUES (?,?)", (nonce, int(time.time())))
            existing = db.execute("SELECT body_hash FROM runs WHERE run_id=?", (data["run_id"],)).fetchone()
            if existing:
                return (200, {"status": "already stored"}) if existing[0] == digest else (409, {"error": "run id conflict"})
            db.execute("INSERT INTO runs VALUES (?,?,?,?,?,?,?,?)", (
                data["run_id"], data["rules_hash"], data["player_count"], data["seed"],
                data["total_dollars"], data["elapsed_milliseconds"], canonical, digest))
        return 201, {"status": "stored", "run_id": data["run_id"]}

    def validate(self, data):
        if not isinstance(data, dict) or not isinstance(data.get("run_id"), str) or not HEX32.fullmatch(data["run_id"]):
            raise ValueError()
        if data.get("rules_hash") not in self.allowed_rules:
            raise ValueError()
        limits = {"player_count": (1, 4), "item_count": (1, 300), "seed": (-2147483648, 2147483647),
                  "total_dollars": (1, 300000000), "elapsed_milliseconds": (1, 604800000)}
        for field, (low, high) in limits.items():
            if type(data.get(field)) is not int or not low <= data[field] <= high:
                raise ValueError()
        team = data.get("team")
        if not isinstance(team, str) or not 1 <= len(team) <= 160 or any(ord(c) < 32 or c in "<>" for c in team):
            raise ValueError()
        folded = normalise(team)
        if any(word in folded for word in self.blocked):
            raise ValueError()
        stamp = data.get("finished_utc")
        match = ISO_UTC.fullmatch(stamp) if isinstance(stamp, str) else None
        if not match:
            raise ValueError()
        datetime.datetime.strptime(match.group(1), "%Y-%m-%dT%H:%M:%S")  # rejects month 13, hour 25, ...

    def board(self, rules, players, seed=None, limit=20, offset=0):
        if not HEX64.fullmatch(rules or "") or players not in (1, 2, 3, 4):
            raise ValueError()
        query = "SELECT body FROM runs WHERE rules_hash=? AND player_count=?"
        params = [rules, players]
        if seed is not None:
            query += " AND seed=?"
            params.append(seed)
        query += " ORDER BY total_dollars DESC, elapsed_milliseconds ASC, run_id ASC LIMIT ? OFFSET ?"
        params.extend([max(1, min(100, limit)), max(0, min(10000, offset))])
        with self.session() as db:
            return {"entries": [json.loads(row[0]) for row in db.execute(query, params)]}


class ScoreHandler(BaseHTTPRequestHandler):
    server_version = "BidwarssScores/3"

    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def respond(self, status, body, extra=None):
        raw = json.dumps(body, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        for name, value in (extra or {}).items():
            self.send_header(name, value)
        self.end_headers()
        self.wfile.write(raw)

    def client_key(self):
        if getattr(self.server, "trust_proxy", False):
            forwarded = self.headers.get("X-Forwarded-For", "")
            if forwarded.strip():
                return forwarded.split(",")[-1].strip()  # the hop our own proxy appended
        return self.client_address[0]

    def limited(self, limiter):
        if limiter is None or limiter.allow(self.client_key()):
            return False
        self.respond(429, {"error": "too many requests"}, {"Retry-After": "30"})
        return True

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/health":
            ok = self.server.store.healthy()
            return self.respond(200 if ok else 503, {"status": "ok" if ok else "database unavailable"})
        if parsed.path != "/v1/leaderboard":
            return self.respond(404, {"error": "not found"})
        if self.limited(getattr(self.server, "read_limiter", None)):
            return
        try:
            q = parse_qs(parsed.query)
            board = self.server.store.board(q.get("rules_hash", [""])[0], int(q.get("player_count", ["0"])[0]),
                int(q["seed"][0]) if "seed" in q else None, int(q.get("limit", ["20"])[0]),
                int(q.get("offset", ["0"])[0]))
            self.respond(200, board)
        except (ValueError, TypeError):
            self.respond(400, {"error": "invalid board filter"})
        except sqlite3.Error:
            self.respond(503, {"error": "score store unavailable"})

    def do_POST(self):
        if self.path != "/v1/runs":
            return self.respond(404, {"error": "not found"})
        if self.limited(getattr(self.server, "write_limiter", None)):
            return
        try:
            size = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            return self.respond(400, {"error": "invalid length"})
        if not 1 <= size <= 8192:
            return self.respond(413, {"error": "invalid body size"})
        raw = self.rfile.read(size)
        try:
            status, body = self.server.store.accept(raw, self.headers.get("X-Bidwarss-Time"),
                self.headers.get("X-Bidwarss-Nonce"), self.headers.get("X-Bidwarss-Signature"))
            self.respond(status, body)
        except sqlite3.Error:
            self.respond(503, {"error": "score store unavailable"})

    def log_message(self, fmt, *args):
        # Never log authorization headers or request bodies.
        pass


class BoundedServer(ThreadingHTTPServer):
    """Threaded server with a hard cap on concurrent connections (slow-client protection)."""
    daemon_threads = True
    request_queue_size = 64

    def __init__(self, address, handler, max_connections=64):
        super().__init__(address, handler)
        self._slots = threading.BoundedSemaphore(max(1, int(max_connections)))

    def process_request(self, request, client_address):
        if not self._slots.acquire(blocking=False):
            try:
                request.sendall(b"HTTP/1.1 503 Service Unavailable\r\nRetry-After: 2\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            except OSError:
                pass
            self.shutdown_request(request)
            return
        super().process_request(request, client_address)

    def process_request_thread(self, request, client_address):
        try:
            super().process_request_thread(request, client_address)
        finally:
            self._slots.release()


def create_server(store, host="127.0.0.1", port=8787, max_connections=64, read_limit=0, write_limit=0,
                  trust_proxy=False, tls_cert=None, tls_key=None):
    server = BoundedServer((host, port), ScoreHandler, max_connections)
    server.store = store
    server.trust_proxy = trust_proxy
    server.read_limiter = RateLimiter(read_limit) if read_limit else None
    server.write_limiter = RateLimiter(write_limit) if write_limit else None
    if tls_cert and tls_key:
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.minimum_version = ssl.TLSVersion.TLSv1_2
        context.load_cert_chain(tls_cert, tls_key)
        server.socket = context.wrap_socket(server.socket, server_side=True)
    return server


def split_env(name):
    return [v.strip() for v in os.environ.get(name, "").split(",") if v.strip()]


if __name__ == "__main__":
    store = ScoreStore(os.environ.get("BIDWARSS_SCORE_DB", "scores.sqlite3"),
        os.environ.get("BIDWARSS_SCORE_SECRET", ""),
        split_env("BIDWARSS_ALLOWED_RULES"),
        split_env("BIDWARSS_BLOCKED_WORDS"))
    server = create_server(store, os.environ.get("BIDWARSS_SCORE_BIND", "127.0.0.1"), int(os.environ.get("PORT", "8787")),
        max_connections=int(os.environ.get("BIDWARSS_MAX_CONNECTIONS", "64")),
        read_limit=int(os.environ.get("BIDWARSS_READ_LIMIT", "240")),
        write_limit=int(os.environ.get("BIDWARSS_WRITE_LIMIT", "60")),
        trust_proxy=os.environ.get("BIDWARSS_TRUST_PROXY") == "1",
        tls_cert=os.environ.get("BIDWARSS_TLS_CERT"), tls_key=os.environ.get("BIDWARSS_TLS_KEY"))
    print("Bidwarss score service listening on", server.server_address,
          "(TLS)" if os.environ.get("BIDWARSS_TLS_CERT") else "(plain HTTP: put a TLS proxy in front)", flush=True)
    server.serve_forever()
