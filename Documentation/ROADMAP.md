# Bidwarss — sabit tasarım kararları ve devam

## Kullanıcının kararları

- Bu bir co-op tidy-up / depo düzenleme oyunu.
- Açık artırma oynanışı **hiç olmayacak**; ileride eklenecek bir özellik de değil.
- Depo/kasalar halihazırda satın alınmış kabul edilecek.
- Eşya modellerini kullanıcı özel olarak tasarlayacak. Test kutuları gerçek eşya tasarımı değildir.
- Gelecekte dağılım, belirlenmiş sınırlar içinde eşya türü başına 10/20/30/40 gibi adet gruplarıyla değişebilecek. Kesin sınırlar ve kurallar henüz belirlenmedi; şu an uygulanmayacak.

## 0 — Temel (kaynaklar hazır, Unity testleri bekliyor)

Host/client bağlantısı, dört oyuncu sınırı, birinci şahıs hareket, tek eşya taşıma, yuvalara yerleştirme, late join durumu ve oyuncu ayrılırken eşya geri bırakma. 10 sabit test kutusu yalnız altyapıyı denemek içindir. 10 kasa, 60 raf yuvası ve geometrik depo otomatik oluşturulur.

## 1 — İlk Unity doğrulaması

- Derleme ve iki oyunculu kabul listesini tamamla.
- Yeniden oda kurma, late join, aynı eşyayı alma ve aynı yuvaya yerleştirme yarışlarını dene.
- Kamera, hareket ve taşıma mesafesini oyun hissine göre ayarla.
- Sunucudan oyuncuya dolu yuva/erişim yok/bırakma alanı dolu geri bildirimi ekle.

## 2 — Kullanıcının tasarladığı eşyalar

- Özel modeller, collider boyutları ve görsel katalog.
- Farklı eşya ölçülerine uygun alanlar; döndürme, yerleştirme önizlemesi ve ihtiyaç varsa host kontrollü fizik.
- Kasaların fiziksel depo bağlantıları, kapakları ve taşıma animasyonları.
- Kullanıcı dağılım kurallarını belirlediğinde sınırlı adet gruplarıyla senaryo üretimi. Şimdiden sabit kasa başına eşya sayısı dayatma.
- Versiyonlanmış kayıt/yükleme: senaryo, eşya kimlikleri, pozisyonlar ve düzenleme ilerlemesi.

## 3 — Uzak ağ ve sunum

- Steam lobby veya Relay seçimi; davet/oda kodu.
- Gerçek uzak ağ testi, gecikme simülasyonu, yeniden bağlanma.
- Gerekiyorsa hareket tahmini ve host migration.
- Kullanıcının sanat yönüne göre karakter, depo, ses, UI ve performans ölçümü.
