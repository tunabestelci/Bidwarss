# V2 doğrulama

Çalıştırılanlar: gerçek C# domain derlemesi ve 2.000 seed / 266.033 assertion; Python skor servisi 4 test grubu (imza, nonce tekrarı, süre aşımı, idempotent tekrar, veri sınırları, çakışma, ekip/seed filtreleri, sıralama ve gerçek HTTP GET).

Unity Editor bu ortamda yok; Unity assembly derlemesi, shader importu, scene üretimi, Windows PowerShell kurulumları ve aşağıdaki Play Mode testleri henüz yürütülmedi. Domain testleri Unity/co-op testlerinin yerine geçmez.

1. Unity 6 URP projesinde kurulum; Console hatasız, Create Gameplay Scene sahneyi üretir. Tekrar üretme kendi asset'lerini silmez.
2. Editor host + Windows build client. İki farklı isim, dört kişilik sınır, farklı katalog reddi. İkinci bağımsız bilgisayarda LAN tekrarı.
3. Aynı kasaya iki oyuncu E tutar; tek açılış. Tuş bırakma, uzaklaşma, duvar arkasından deneme, pencere odağı kaybı açmayı keser.
4. Aynı eşya için eşzamanlı E; tek sahip. En çok 10 aynı tür; farklı tür ve dolu palet reddedilir. Doğru paletin önündeki turkuaz işaretten yerleştirilir.
5. İstifin son eşyasını geri almak sayaç/para değerini azaltır. Q dolu zemine bırakamaz. Eşya taşırken bağlantı kopması eşyaları giriş alanına bırakır.
6. Ortasında katıl; açılmış kasalar ve taşınan/yerleşmiş eşyalar eşit görünür. Host ayrılınca menüye dönüş.
7. 120 eşyanın tamamını yerleştir; sonuç bir kez oluşur, 7 durum tutarı toplam kazanca eşittir. Aynı seed yeniden başlatmada içerik aynı; yeni seed yeni içerik. Süre parayı azaltmaz.
8. Sonuçtan yeni depoya geç; eski modeller, kapaklar, ilerleme, eldeki eşya temizlenir. Yerel kayıt oyunu kapatıp açınca kalır.
9. Dünya servisi olmadan açık durum mesajı. HTTPS servis + dedicated server ile sonuç kaydı; oyuncu-host oda dünya kaydı göndermez. Servis kesilip açıldığında aynı tamamlanmış sonucun tekrarı çoğalmaz.
10. Kendi görsel prefab'ını bağla, taşıma hücresine oturmasını ve collider'ların etkileşimi engellememesini kontrol et.

## DepoLevel entegrasyonu

Bes geometri testi gecti: dugum kimlikleri/sayilari, 4 spawn ve merkez koridor, konteyner basina en cok 30 esya cikisi, 12 raf/120 hucre ve yaklasma alanlari, 300 esyalik kurtarma gridi. AABB verisi kullanir; Unity fizik motorunun yerine gecmez.

Unity'de Build Uploaded Depot (Co-op) sonrasi: giris zemini ve kapilardan yurume; her konteyneri E ile acma; iki kanadin koridora cikmamasi; acilmis kapiya gec katilim; 120 esyayi gercek raf hucrelerine yerlestirme; yeni run kapilari kapatirken oyuncularin konteyner icinde kalmamasi; dogru sahnenin client build'inde acilmasi; shaderlar/dokular/TMP'siz yazi ve FPS olcumu.
