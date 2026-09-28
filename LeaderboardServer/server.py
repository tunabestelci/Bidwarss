"""Bidwarss verified-server leaderboard. Python 3.10+, standard library only.
Never distribute BIDWARSS_SCORE_SECRET to game clients or player-hosted rooms.
"""
import hashlib
import hmac
import json
import os
import re
import sqlite3
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HEX32 = re.compile(r"^[a-f0-9]{32}$")
HEX64 = re.compile(r"^[a-f0-9]{64}$")


class ScoreStore:
    def __init__(self, path, secret, allowed_rules):
        if len(secret) < 32 or not allowed_rules or any(not HEX64.fullmatch(r) for r in allowed_rules):
            raise ValueError("A 32+ character server secret and allowed rules hashes are required")
        self.path, self.secret, self.allowed_rules = str(path), secret.encode(), set(allowed_rules)
        with self.connect() as db:
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

    def connect(self):
        return sqlite3.connect(self.path, timeout=10)

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
        with self.connect() as db:
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
        limits = {"player_count": (1, 4), "item_count": (10, 300), "seed": (-2147483648, 2147483647),
                  "total_dollars": (1, 300000000), "elapsed_milliseconds": (1, 604800000)}
        for field, (low, high) in limits.items():
            if type(data.get(field)) is not int or not low <= data[field] <= high:
                raise ValueError()
        if data["item_count"] % 10:
            raise ValueError()
        team = data.get("team")
        if not isinstance(team, str) or not 1 <= len(team) <= 160 or any(ord(c) < 32 or c in "<>" for c in team):
            raise ValueError()
        if not isinstance(data.get("finished_utc"), str) or len(data["finished_utc"]) > 50:
            raise ValueError()

    def board(self, rules, players, seed=None, limit=20):
        if not HEX64.fullmatch(rules or "") or players not in (1, 2, 3, 4):
            raise ValueError()
        query = "SELECT body FROM runs WHERE rules_hash=? AND player_count=?"
        params = [rules, players]
        if seed is not None:
            query += " AND seed=?"
            params.append(seed)
        query += " ORDER BY total_dollars DESC, elapsed_milliseconds ASC, run_id ASC LIMIT ?"
        params.append(max(1, min(100, limit)))
        with self.connect() as db:
            return {"entries": [json.loads(row[0]) for row in db.execute(query, params)]}


class ScoreHandler(BaseHTTPRequestHandler):
    server_version = "BidwarssScores/2"
    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def respond(self, status, body):
        raw = json.dumps(body, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/health":
            return self.respond(200, {"status": "ok"})
        if parsed.path != "/v1/leaderboard":
            return self.respond(404, {"error": "not found"})
        try:
            q = parse_qs(parsed.query)
            board = self.server.store.board(q.get("rules_hash", [""])[0], int(q.get("player_count", ["0"])[0]),
                int(q["seed"][0]) if "seed" in q else None, int(q.get("limit", ["20"])[0]))
            self.respond(200, board)
        except (ValueError, TypeError):
            self.respond(400, {"error": "invalid board filter"})

    def do_POST(self):
        if self.path != "/v1/runs":
            return self.respond(404, {"error": "not found"})
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


def create_server(store, host="127.0.0.1", port=8787):
    server = ThreadingHTTPServer((host, port), ScoreHandler)
    server.store = store
    return server


if __name__ == "__main__":
    store = ScoreStore(os.environ.get("BIDWARSS_SCORE_DB", "scores.sqlite3"),
        os.environ.get("BIDWARSS_SCORE_SECRET", ""),
        [v.strip() for v in os.environ.get("BIDWARSS_ALLOWED_RULES", "").split(",") if v.strip()])
    server = create_server(store, os.environ.get("BIDWARSS_SCORE_BIND", "127.0.0.1"), int(os.environ.get("PORT", "8787")))
    print("Bidwarss score service listening on", server.server_address, flush=True)
    server.serve_forever()
