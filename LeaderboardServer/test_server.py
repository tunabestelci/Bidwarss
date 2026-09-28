import hashlib, hmac, json, tempfile, time, unittest, uuid, threading
from pathlib import Path
from urllib.request import urlopen
from server import ScoreStore, create_server

class ScoresTest(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.secret='test-secret-'*4; self.rules='a'*64
        self.store=ScoreStore(Path(self.tmp.name)/'scores.db',self.secret,[self.rules])
        self.score=dict(run_id=uuid.uuid4().hex,rules_hash=self.rules,team='Tuna / Test',finished_utc='2026-09-28T00:00:00Z',seed=42,total_dollars=12000,elapsed_milliseconds=60000,player_count=2,item_count=120)
    def tearDown(self): self.tmp.cleanup()
    def signed(self,score=None,stamp=None,nonce=None):
        raw=json.dumps(score or self.score).encode(); stamp=str(stamp or int(time.time())); nonce=nonce or uuid.uuid4().hex
        sig=hmac.new(self.secret.encode(),stamp.encode()+b'\n'+nonce.encode()+b'\n'+raw,hashlib.sha256).hexdigest()
        return raw,stamp,nonce,sig
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
    def test_http_read(self):
        server=create_server(self.store,port=0);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            base='http://127.0.0.1:'+str(server.server_port)
            with urlopen(base+'/health') as response:self.assertEqual(json.load(response)['status'],'ok')
            with urlopen(base+'/v1/leaderboard?rules_hash='+self.rules+'&player_count=2') as response:self.assertEqual(json.load(response)['entries'],[])
        finally:server.shutdown();server.server_close();thread.join()

if __name__=='__main__':unittest.main()
