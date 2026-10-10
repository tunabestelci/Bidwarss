import hashlib, hmac, json, tempfile, time, unittest, uuid, threading
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import urlopen
from server import ScoreStore, RateLimiter, create_server


class ScoresTest(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.secret='test-secret-'*4; self.rules='a'*64
        self.store=ScoreStore(Path(self.tmp.name)/'scores.db',self.secret,[self.rules])
        self.score=dict(run_id=uuid.uuid4().hex,rules_hash=self.rules,team='Tuna / Test',finished_utc='2026-09-28T00:00:00Z',seed=42,total_dollars=12000,elapsed_milliseconds=60000,player_count=2,item_count=120)
    def tearDown(self): self.tmp.cleanup()
    def sign(self,raw,stamp=None,nonce=None):
        stamp=str(stamp or int(time.time())); nonce=nonce or uuid.uuid4().hex
        sig=hmac.new(self.secret.encode(),stamp.encode()+b'\n'+nonce.encode()+b'\n'+raw,hashlib.sha256).hexdigest()
        return raw,stamp,nonce,sig
    def signed(self,score=None,stamp=None,nonce=None):
        return self.sign(json.dumps(score or self.score).encode(),stamp,nonce)
    def test_authentication_and_replay(self):
        args=self.signed(); self.assertEqual(self.store.accept(*args)[0],201)
        self.assertEqual(self.store.accept(*args)[0],409)
        self.assertEqual(self.store.accept(*self.signed())[0],200)
        bad=list(self.signed());bad[3]='x'*64;self.assertEqual(self.store.accept(*bad)[0],401)
        bad[3]='ş';self.assertEqual(self.store.accept(*bad)[0],401)
        self.assertEqual(self.store.accept(*self.signed(stamp=int(time.time())-600))[0],401)
    def test_validation_and_conflicting_retry(self):
        self.assertEqual(self.store.accept(*self.signed())[0],201)
        other=dict(self.score,total_dollars=12001);self.assertEqual(self.store.accept(*self.signed(other))[0],409)
        for change in [dict(item_count=121),dict(rules_hash='b'*64),dict(player_count=5),dict(total_dollars=True),dict(team='<script>')]:
            self.assertEqual(self.store.accept(*self.signed(dict(self.score,**change)))[0],422)
    def test_ranking_and_isolation(self):
        for value,players,seed in [(100,2,42),(300,2,42),(200,2,43),(999,1,42)]:
            score=dict(self.score,run_id=uuid.uuid4().hex,total_dollars=value,player_count=players,seed=seed)
            self.assertEqual(self.store.accept(*self.signed(score))[0],201)
        self.assertEqual([x['total_dollars'] for x in self.store.board(self.rules,2)['entries']],[300,200,100])
        self.assertEqual(len(self.store.board(self.rules,2,42)['entries']),2)
    def test_pagination(self):
        for value in range(1,6):
            score=dict(self.score,run_id=uuid.uuid4().hex,total_dollars=value*100)
            self.assertEqual(self.store.accept(*self.signed(score))[0],201)
        page=self.store.board(self.rules,2,limit=2,offset=2)['entries']
        self.assertEqual([x['total_dollars'] for x in page],[300,200])
        self.assertEqual(self.store.board(self.rules,2,limit=2,offset=-5)['entries'][0]['total_dollars'],500)
    def test_finished_timestamp_must_be_iso(self):
        for bad in ['yesterday','2026-13-01T00:00:00Z','2026-10-10T25:00:00Z','2026-10-10','']:
            score=dict(self.score,run_id=uuid.uuid4().hex,finished_utc=bad)
            self.assertEqual(self.store.accept(*self.signed(score))[0],422,bad)
        # DateTime.ToString("O") in the game: seven fractional digits and a Z suffix.
        score=dict(self.score,run_id=uuid.uuid4().hex,finished_utc='2026-10-10T20:43:00.1234567Z')
        self.assertEqual(self.store.accept(*self.signed(score))[0],201)
    def test_blocked_words_are_matched_after_unicode_folding(self):
        store=ScoreStore(Path(self.tmp.name)/'blocked.db',self.secret,[self.rules],['badword'])
        for team in ['x BADWORD y','ｂａｄｗｏｒｄ']:
            score=dict(self.score,run_id=uuid.uuid4().hex,team=team)
            self.assertEqual(store.accept(*self.signed(score))[0],422,team)
        self.assertEqual(store.accept(*self.signed(dict(self.score,run_id=uuid.uuid4().hex)))[0],201)
    def test_signature_covers_raw_bytes_exactly_as_the_game_sends_them(self):
        # Unity's JsonUtility emits compact JSON in field order, which differs from json.dumps' spacing.
        run=uuid.uuid4().hex
        raw=('{"run_id":"%s","rules_hash":"%s","team":"Tuna / Ömer","finished_utc":"2026-10-10T20:43:00.1234567Z",'
             '"seed":-5,"total_dollars":12000,"elapsed_milliseconds":60000,"player_count":2,"item_count":120}'%(run,self.rules)).encode()
        self.assertEqual(self.store.accept(*self.sign(raw))[0],201)
        tampered=raw.replace(b'12000',b'99999')
        args=list(self.sign(raw)); args[0]=tampered
        self.assertEqual(self.store.accept(*args)[0],401)
        self.assertEqual(self.store.board(self.rules,2,-5)['entries'][0]['team'],'Tuna / Ömer')
    def test_rate_limiter_window(self):
        limiter=RateLimiter(2,window=10)
        self.assertTrue(limiter.allow('a',now=0)); self.assertTrue(limiter.allow('a',now=1))
        self.assertFalse(limiter.allow('a',now=2)); self.assertTrue(limiter.allow('b',now=2))
        self.assertTrue(limiter.allow('a',now=11.5))
        self.assertTrue(RateLimiter(0).allow('x'))
    def test_http_read(self):
        server=create_server(self.store,port=0);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            base='http://127.0.0.1:'+str(server.server_port)
            with urlopen(base+'/health') as response:self.assertEqual(json.load(response)['status'],'ok')
            with urlopen(base+'/v1/leaderboard?rules_hash='+self.rules+'&player_count=2') as response:self.assertEqual(json.load(response)['entries'],[])
        finally:server.shutdown();server.server_close();thread.join()
    def test_http_rate_limit_returns_429(self):
        server=create_server(self.store,port=0,read_limit=2);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            url='http://127.0.0.1:'+str(server.server_port)+'/v1/leaderboard?rules_hash='+self.rules+'&player_count=2'
            for _ in range(2):
                with urlopen(url) as response:self.assertEqual(response.status,200)
            with self.assertRaises(HTTPError) as caught:urlopen(url)
            self.assertEqual(caught.exception.code,429)
            self.assertEqual(caught.exception.headers.get('Retry-After'),'30')
            with urlopen('http://127.0.0.1:'+str(server.server_port)+'/health') as response:self.assertEqual(response.status,200)
        finally:server.shutdown();server.server_close();thread.join()

if __name__=='__main__':unittest.main()
