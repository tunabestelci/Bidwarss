# V2 doğrulama

Otomatik (CI'da her push'ta): C# domain testleri (2.000 seed, rastgele depo değişmezleri, boyuta göre tür sınırı, adet dağılımı, katalog dosyası, tam tur, devir, kural sınırları, oyuncu adı temizleme, golden depo dosyası); JS depo simülatörünün (wiki) C# ile aynı depoyu üretmesi (`Tests/depot_sim_check.js`); Python skor servisi testleri (imza ve ham gövde, nonce tekrarı, süre aşımı, idempotent tekrar, veri sınırları, tarih biçimi, yasak kelime, sayfalama, hız sınırı 429, gerçek HTTP); depo denetimi (.meta/GUID, parantez dengesi, bileşen dosya adları, Domain bağımsızlığı); PowerShell sözdizimi; Docker imajı derlemesi.

Unity Editor bu ortamda yok; Unity assembly derlemesi, shader importu, scene üretimi, Windows PowerShell kurulumları ve aşağıdaki Play Mode testleri henüz yürütülmedi. Domain testleri Unity/co-op testlerinin yerine geçmez.

1. Unity 6 URP projesinde kurulum; Console hatasız, Create Gameplay Scene sahneyi üretir. Tekrar üretme kendi asset'lerini silmez.
2. Editor host + Windows build client. İki farklı isim, dört kişilik sınır, farklı katalog reddi. İkinci bağımsız bilgisayarda LAN tekrarı.
3. Aynı kasaya iki oyuncu E tutar; tek açılış. Tuş bırakma, uzaklaşma, duvar arkasından deneme, pencere odağı kaybı açmayı keser.
4. Aynı eşya için eşzamanlı E; tek sahip. En çok 10 aynı tür; farklı tür ve dolu palet reddedilir. Doğru paletin önündeki turkuaz işaretten yerleştirilir.
5. İstifin son eşyasını geri almak sayaç/para değerini azaltır. Q dolu zemine bırakamaz. Eşya taşırken bağlantı kopması eşyaları giriş alanına bırakır.
6. Ortasında katıl; açılmış kasalar ve taşınan/yerleşmiş eşyalar eşit görünür. Host ayrılınca menüye dönüş.
7. Turdaki bütün eşyaları yerleştir; sonuç bir kez oluşur, 7 durum tutarı toplam kazanca eşittir. Aynı seed yeniden başlatmada içerik aynı; yeni seed yeni kasa sayısı, kasa yükleri, tür ve adetler. Üst üste 10 yeni depoda kullanılan kasa sayısı, raf sayısı ve türler farklı çıkar; hiçbir türden 20'den, boy saati gibi büyük parçadan 5'ten fazla yoktur. Kullanılmayan konteyner kapalı ve işaretli kalır. Süre parayı azaltmaz.
8. Sonuçtan yeni depoya geç; eski modeller, kapaklar, ilerleme, eldeki eşya temizlenir. Yerel kayıt oyunu kapatıp açınca kalır.
9. Dünya servisi olmadan açık durum mesajı. HTTPS servis + dedicated server ile sonuç kaydı; oyuncu-host oda dünya kaydı göndermez. Servis kesilip açıldığında aynı tamamlanmış sonucun tekrarı çoğalmaz.
10. Kendi görsel prefab'ını bağla, taşıma hücresine oturmasını ve collider'ların etkileşimi engellememesini kontrol et.
11. Ayarlar: hassasiyet, ses, müzik, görüş alanı değişir ve oyun yeniden açılınca korunur; ESC ile oyuna dönünce panel kapanır. Gamepad: çubuklarla yürü/bak, A ile kasa aç (basılı) ve al/istifle, B ile bırak, Start menü, Geri sonuç.
12. Sesler: kasa açılışı, alma, istifleme, bırakma, adım ve ortam müziği duyulur; müzik sıfırlanınca susar; 10 eşya birden istiflenince ses üst üste binmez.
13. Host mola menüsü: odayı kilitleyince yeni katılım "Host odayı kilitledi" ile reddedilir; AT düğmesi oyuncuyu eşyalarını kurtarma alanına bırakarak çıkarır; host kendini atamaz.
14. Adres: `127.0.0.1:7800` ile host edip aynı adresle katıl; alan adıyla katıl; hatalı port ve IPv6 açık mesaj verir.
15. Rekor tablosu: 1–4 kişi, Günlük ve "Bu kurallar" süzgeçleri yerel listeyi doğru daraltır; Dünya sekmesi süzgeç değişince yeniden ister; ESC ile panel kapanır.
16. Dedicated server (`-bidwarssServer`): açılışta `Leaderboard rules hash` loglanır; depo bitince 30 sn sonra (skor yüklemesi bittiyse) yeni depo başlar ve bağlı oyuncular başlangıç noktasına döner; `-bidwarssPort` çalışır; kalıcı ret alan sonuç tekrar denenmez.
17. Kurulum: `Install-Bidwarss.ps1` iki kez çalıştırılır, repodan bir dosya silinip yeniden çalıştırılınca eski dosya `Removed/` altına taşınır; `New-BidwarssProject.ps1` macOS/Linux'ta (`pwsh`) Unity'yi bulur; `PrototypeBuilder` yarım kalan klasörü `GeneratedV2_incomplete` altına alıp yeniden üretir.
18. Build menüsü: Windows Client, Dedicated Server (Windows/Linux) çıktıları `Builds/` altında oluşur ve çalışır.

## DepoLevel entegrasyonu

Bes geometri testi gecti: dugum kimlikleri/sayilari, 4 spawn ve merkez koridor, konteyner basina en cok 30 esya cikisi, 12 raf/120 hucre ve yaklasma alanlari, 300 esyalik kurtarma gridi. AABB verisi kullanir; Unity fizik motorunun yerine gecmez.

Unity'de Build Uploaded Depot (Co-op) sonrasi: giris zemini ve kapilardan yurume; her konteyneri E ile acma; iki kanadin koridora cikmamasi; acilmis kapiya gec katilim; 120 esyayi gercek raf hucrelerine yerlestirme; yeni run kapilari kapatirken oyuncularin konteyner icinde kalmamasi; dogru sahnenin client build'inde acilmasi; shaderlar/dokular/TMP'siz yazi ve FPS olcumu.

## Animasyon ve el kontrolü (Unity'de gözle)

19. Kasa açma: Bruno dalı birleştirildi. Bantlı/tahta kasada maket bıçağı ve çivi sökücü, eller o aletlerin sapında kalır; `Hands` modundaki konteynerde E basılıyken eller kapı koluna gider, kapaklar ilerlemeyle birlikte aralanıp titrer; açılınca kanatlar savrulup hafifçe geri sekerek durur, eller geniş açılır.
20. Eşya çıkışı: parçalar kasadan yay çizip dönerek çıkar, yere ezilerek oturur; toz ve talaş görünür, parçalar üst üste binmez; büyük parçalar küçüklerden yavaş ve ağır çıkar.
21. Eller: parmaklar ve başparmak ayrı hareket eder; yürürken sallanma hıza uyar; eşya taşırken eller yükün altında kıvrılır; alma uzanıp kavrama, koyma ileri itme olarak okunur. Başka oyuncunun avatarında dirsekler ve taşıma/yükleme kol hareketi görünür.
22. Depo tazeliği: her yeni oyunda kasa sayısı, içerikleri ve rafların sırası değişir; HUD'da `x / kapasite` raf sayıları ve açılacak kasa sayısı doğrudur.
23. Katalog: wiki'den indirilen JSON menüden içe aktarılır, Console'da eşya ve kural hash'i yazılır; bozuk dosya Türkçe hata verir ve katalog değişmez.
