FROM python:3.12-slim
RUN useradd --system --create-home --uid 10001 bidwarss && mkdir /data && chown bidwarss /data
WORKDIR /app
COPY server.py .
# The secret and allowed rules hashes are passed at run time, never baked into the image.
ENV BIDWARSS_SCORE_DB=/data/scores.sqlite3 \
    BIDWARSS_SCORE_BIND=0.0.0.0 \
    PORT=8787 \
    PYTHONUNBUFFERED=1
VOLUME /data
USER bidwarss
EXPOSE 8787
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD python -c "import os,urllib.request;urllib.request.urlopen('http://127.0.0.1:'+os.environ.get('PORT','8787')+'/health',timeout=4)"
CMD ["python", "server.py"]
