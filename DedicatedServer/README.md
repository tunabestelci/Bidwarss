# Dedicated server

Authoritative headless server for competitive (verified) runs. Player-hosted rooms never upload world scores.

1. In Unity install the *Linux Dedicated Server Build Support* (or Windows) module from Unity Hub.
2. `Bidwarss > Build > Dedicated Server (Linux)` or, headless:
   `Unity -batchmode -quit -projectPath <project> -executeMethod Bidwarss.Editor.BuildTools.LinuxServer`
3. Run it: `./BidwarssServer.x86_64 -batchmode -nographics -bidwarssServer`
   - `-bidwarssPort <n>` UDP port, default 7777 (run several servers on one machine with different ports)
   - `-bidwarssSeed <int>` seed of the first depot; later depots use fresh seeds
4. The startup log prints `Leaderboard rules hash: ...`. Add it to the score service's `BIDWARSS_ALLOWED_RULES`.
5. For verified scores set `BIDWARSS_SCORE_URL` and `BIDWARSS_SCORE_SECRET` (same secret as the score service) in the server's environment only.

After a depot is finished the server keeps the result visible for 30 seconds, waits for the score upload to settle
(up to two more minutes), then starts a fresh depot automatically. Players connect with `host:port` or just `host`.
Use `Dockerfile` here to containerise the Linux build.
