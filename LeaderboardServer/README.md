# Dünya skor servisi

Python 3.10+, yalnızca standart kütüphane. SQLite kalıcı disk gerektirir. Bu repo servisi otomatik olarak internete yayınlamaz.

1. Kural hash'ini al: sahne üretildiğinde, oyun host edilirken ve dedicated server açılırken Console/log'a `Leaderboard rules hash: ...` yazılır. Katalog veya denge değişince yenisini **Bidwarss > Print Leaderboard Rules Hash** ile al.
2. Sunucuda ortam değişkenlerini ayarla (tablo aşağıda). `BIDWARSS_SCORE_SECRET` en az 32 karakter rastgele sır, `BIDWARSS_ALLOWED_RULES` virgülle ayrılmış izinli hash'ler.
3. Çalıştır: `python LeaderboardServer/server.py`, ya da `deploy/` altındaki hazır dosyalarla:
   - **Docker + otomatik TLS:** `deploy/docker-compose.yml` (Caddy sertifikayı kendisi alır ve yeniler). `.env` içine `SCORES_DOMAIN`, `BIDWARSS_SCORE_SECRET`, `BIDWARSS_ALLOWED_RULES` yaz, `docker compose up -d --build`.
   - **Tek konteyner:** `docker build -t bidwarss-scores LeaderboardServer`; veri `/data` biriminde durur.
   - **systemd:** `deploy/bidwarss-scores.service` (sertleştirilmiş birim; kurulum adımları dosyanın başında).
   - Varsayılan `127.0.0.1:8787`. Dış erişimi TLS'li bir ters vekil veya `BIDWARSS_TLS_CERT` / `BIDWARSS_TLS_KEY` ile sağla. SQLite dosyasını yedekle.
4. Unity sahnesindeki LeaderboardClient `serviceUrl` alanına HTTPS adresini yaz. Bu alan herkese açık okuma içindir, sır içermez.
5. Güvendiğin bir makinede dedicated server build'ini çalıştır ([../DedicatedServer/README.md](../DedicatedServer/README.md)). Bu süreç için `BIDWARSS_SCORE_URL` ve aynı `BIDWARSS_SCORE_SECRET` ortam değişkenlerini ayarla. Sırrı client build'ine veya oyuncunun host ettiği odaya verme.

| Değişken | Anlamı | Varsayılan |
|---|---|---|
| `BIDWARSS_SCORE_SECRET` | HMAC sırrı, 32+ karakter | zorunlu |
| `BIDWARSS_ALLOWED_RULES` | İzinli kural hash'leri | zorunlu |
| `BIDWARSS_SCORE_DB` | SQLite dosyası | `scores.sqlite3` |
| `BIDWARSS_SCORE_BIND`, `PORT` | Dinleme adresi ve portu | `127.0.0.1`, `8787` |
| `BIDWARSS_BLOCKED_WORDS` | Ekip adında reddedilecek kelimeler (virgülle) | boş |
| `BIDWARSS_MAX_CONNECTIONS` | Eşzamanlı bağlantı sınırı | `64` |
| `BIDWARSS_READ_LIMIT`, `BIDWARSS_WRITE_LIMIT` | İstemci başına dakikada GET / POST sınırı (0 = sınırsız) | `240`, `60` |
| `BIDWARSS_TRUST_PROXY` | `1` ise istemci adresi `X-Forwarded-For` son girdisinden alınır (yalnız kendi vekilinin arkasında) | kapalı |
| `BIDWARSS_TLS_CERT`, `BIDWARSS_TLS_KEY` | PEM dosyaları; ikisi de verilirse servis doğrudan HTTPS sunar | kapalı |

Dedicated server tamamlanan sonucu HMAC-SHA256 ile imzalar; istemci ve oyuncunun host ettiği LAN odası dünya puanı göndermez. Servis imzayı, 5 dakikalık zaman aralığını, nonce tekrarını, kural hash'ini, ISO-8601 tarih biçimini, yasak kelimeleri ve veri sınırlarını doğrular. Tamamlanan aynı koşunun tekrar gönderimi yeni nonce ile idempotenttir; aynı koşu kimliğiyle farklı içerik reddedilir. Kalıcı ret (HTTP 409/413/422) alan dedicated server aynı sonucu tekrar göndermez ve hata logu yazar; ağ hatası ve 5xx yeniden denenir. Dedicated server sonucu 30 saniye gösterip gönderim bitince yeni depoyu kendisi başlatır.

Bu model güvenilir oyun sunucusuna dayanır; servis tek başına maçın hareketlerini yeniden oynatıp doğrulamaz. Sunucu sırrını ele geçiren biri puan gönderebilir. Hesap sistemi ve isim sahipliği henüz yok; ekip adları takma addır ve yasak kelime listesi yalnızca basit bir süzgeçtir. Yerel kayıtlar dünya listesine taşınmaz.

- GET `/health` (veritabanına erişemezse 503)
- GET `/v1/leaderboard?rules_hash=<hash>&player_count=2&seed=42&limit=20&offset=0` (seed, limit ve offset isteğe bağlı; limit en çok 100)
- POST `/v1/runs`, en çok 8192 bayt; `X-Bidwarss-Time`, `X-Bidwarss-Nonce`, `X-Bidwarss-Signature`.
- İmza girdisi UTF-8: `timestamp + "\n" + nonce + "\n" + ham JSON gövdesi`. Hız sınırında 429 ve `Retry-After`, dolu sunucuda 503 döner.

Katalog/denge değiştiğinde (rastgele depo kuralları, kural sürümü 3 ile eski hash'leri geçersiz kıldı) yeni hash'i açıkça izin listesine ekle. Aynı kural seti ve oyuncu sayısı ayrı tabloda tutulur; para azalan, eşitlikte süre artan sıralanır. Günlük ortak yarışma için seed filtresini kullan.
