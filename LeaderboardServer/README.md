# Dünya skor servisi

Python 3.10+, yalnızca standart kütüphane. SQLite kalıcı disk gerektirir. Bu repo servisi otomatik olarak internete yayınlamaz.

1. Unity sahne üreticisindeki Console çıktısından `Leaderboard rules hash` değerini al.
2. Sunucuda `BIDWARSS_SCORE_SECRET` (en az 32 karakter rastgele sır), `BIDWARSS_ALLOWED_RULES` (virgülle ayrılmış izinli hash'ler), isteğe bağlı `BIDWARSS_SCORE_DB` ayarla.
3. `python LeaderboardServer/server.py` çalıştır. Varsayılan `127.0.0.1:8787`; dış erişimi HTTPS reverse proxy üzerinden sağla. Proxy'de istek boyutu, hız ve bağlantı sınırları koy. SQLite dosyasını yedekle.
4. Unity sahnesindeki LeaderboardClient `serviceUrl` alanına HTTPS adresini yaz. Bu alan herkese açık okuma içindir, sır içermez.
5. Güvendiğin bir makinede aynı oyunun dedicated server build'ini `-batchmode -nographics -bidwarssServer` ile çalıştır. Bu süreç için `BIDWARSS_SCORE_URL` ve aynı `BIDWARSS_SCORE_SECRET` ortam değişkenlerini ayarla. Sırrı client build'ine veya oyuncunun host ettiği odaya verme.

Dedicated server tamamlanan sonucu HMAC-SHA256 ile imzalar; istemci ve oyuncunun host ettiği LAN odası dünya puanı göndermez. Servis imzayı, 5 dakikalık zaman aralığını, nonce tekrarını, kural hash'ini ve veri sınırlarını doğrular. Tamamlanan aynı koşunun tekrar gönderimi yeni nonce ile idempotenttir; aynı koşu kimliğiyle farklı içerik reddedilir. Tamamlanan koşu başına sunucu oturumunu yeniden başlat; otomatik sunucu filo/oda yönetimi bu sürümde yok.

Bu model güvenilir oyun sunucusuna dayanır; servis tek başına maçın hareketlerini yeniden oynatıp doğrulamaz. Sunucu sırrını ele geçiren biri puan gönderebilir. Hesap sistemi ve isim sahipliği henüz yok; ekip adları takma addır. Yerel kayıtlar dünya listesine taşınmaz.

- GET `/health`
- GET `/v1/leaderboard?rules_hash=<hash>&player_count=2&seed=42&limit=20` (seed isteğe bağlı)
- POST `/v1/runs`, en çok 8192 bayt; `X-Bidwarss-Time`, `X-Bidwarss-Nonce`, `X-Bidwarss-Signature`.
- İmza girdisi UTF-8: `timestamp + "\n" + nonce + "\n" + ham JSON gövdesi`.

Katalog/denge değiştiğinde yeni hash'i açıkça izin listesine ekle. Aynı kural seti ve oyuncu sayısı ayrı tabloda tutulur; para azalan, eşitlikte süre artan sıralanır. Günlük ortak yarışma için seed filtresini kullan.
