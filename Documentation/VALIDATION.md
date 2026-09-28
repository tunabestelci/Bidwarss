# Doğrulama

Unity Editor ve C# derleyicisi bu ortamda bulunmuyor. **Unity derlemesi, shader derlemesi, Play Mode, PowerShell kurulum scripti ve gerçek multiplayer çalıştırılmadı.** Kaynak dosyaları C# sözdizimi ayrıştırıcısıyla; JSON/asmdef dosyaları JSON ayrıştırıcısıyla kontrol edilir. Bu, Unity API uyumluluğu veya oynanabilirlik testi değildir.

Önceki ZIP'teki rastgele dağıtım ve o dağıtıma ait testler kaldırıldı; çünkü kullanıcı bu sistemi şimdilik kapsam dışında bıraktı.

## İlk import

1. Unity 6 URP projesinde paket çözümlemesi ve C# derlemesi hatasız.
2. Builder sahne, kayıtlı oyuncu prefabı, katalog, 10 kasa ve 60 raf yuvası üretir.
3. Tekrar çalıştırmak mevcut Generated içeriğini değiştirmez.
4. Shader pembe görünmez; nesnelerde üç tonlu renk görünür.
5. Host başlatınca 10 sabit test kutusu oluşur. Kasaya E basmak rastgele eşya üretmez.
6. Spawn Test Items kapalıyken hiç eşya oluşmaz; tamamlandı mesajı görünmez.

## Host + client kabul listesi

| Senaryo | Beklenen sonuç |
|---|---|
| Host aç, client katıl | Ayrı oyuncular ve aynı depo |
| İki oyuncu aynı test eşyasını alsın | Yalnız biri taşır |
| İki oyuncu aynı boş yuvaya yerleştirsin | Yalnız biri yerleşir, diğeri elde kalır |
| Yerleşmiş eşya tekrar alınsın | Sayaç azalır, yuva serbest kalır |
| Uzak veya duvar arkasındaki nesneye istek | Reddedilir |
| Elde eşya varken ikinci eşya alınsın | Reddedilir |
| Q ile boş zemine bırak | Her oyuncu aynı konumu görür |
| Q ile duvara/rafa/eşyaya bırak | Eşya elde kalır |
| Oyuncu taşırken bağlantısı kesilsin | Eşya girişteki kurtarma alanına döner |
| Eşyalar yerleştikten sonra late join | Eşya konumları ve sayaç aynı |
| Eşya taşınırken late join | Taşıyan oyuncu ve eşya görünür |
| 4 oyuncudan sonra beşinci katılım | Reddedilir |
| Client ayrılsın, yenisi katılsın | Boş koltuk yeniden kullanılır |
| ESC menüde bekle | Hareket durur, imleç serbest |
| Host ayrılsın | Client menüye döner |
| Tekrar host kur | Temiz test düzeni, yeniden katılma mümkün |
| Bütün test eşyaları yerleştirilsin | 10/10; test tamamlama mesajı |

Client prediction, serbest fizik, kayıt/yükleme ve Steam/Relay yok. Açık artırma ise proje tasarımında yok; bir test eksiği değildir.
