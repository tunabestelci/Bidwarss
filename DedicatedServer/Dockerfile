# Build context: the Unity project root, after Bidwarss > Build > Dedicated Server (Linux)
#   docker build -f DedicatedServer/Dockerfile -t bidwarss-server .
#   docker run -d --restart unless-stopped -p 7777:7777/udp \
#     -e BIDWARSS_SCORE_URL=https://scores.example.com -e BIDWARSS_SCORE_SECRET=<same secret as the score service> \
#     bidwarss-server
FROM ubuntu:24.04
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/* \
    && useradd --system --create-home --uid 10001 bidwarss
WORKDIR /srv/bidwarss
COPY Builds/LinuxServer/ ./
RUN chmod +x BidwarssServer.x86_64
USER bidwarss
EXPOSE 7777/udp
# Extra flags: -bidwarssPort <n>  -bidwarssSeed <int>
ENTRYPOINT ["./BidwarssServer.x86_64", "-batchmode", "-nographics", "-bidwarssServer"]
