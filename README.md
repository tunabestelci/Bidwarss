# Bidwarss — co-op depo düzenleme prototipi

Önceden satın alınmış 10 kasayı aç, içindekileri türüne göre 10'lu gruplarda yerleştir ve depo bitince toplam kazancını gör. **Açık artırma yok.** Unity 6 / Universal RP için kaynak paketi; sahne ve örnek varlıklar editörde üretilir.

## Hızlı kurulum — D diski

Repo klasöründe `git pull --ff-only` çalıştır. Unity kapalıyken PowerShell'de:

```powershell
.\New-BidwarssProject.ps1 -ProjectPath 'D:\UnityProjects\Bidwarss'
```

Bu komut kurulu Unity 6'yı bulur, Unity paket kayıt servisinden editöre uygun kararlı URP sürümünü seçer, Hub'ın şablon ekranını kullanmadan proje oluşturur, kaynakları kurar ve sahneyi üretir. Unity lisansı etkin olmalı ve paket indirmek için internet gerekir. Editör farklı diskteyse:

```powershell
.\New-BidwarssProject.ps1 -ProjectPath 'D:\UnityProjects\Bidwarss' -EditorPath 'D:\Unity\6000.5.0f1\Editor\Unity.exe'
```

İkinci komuttaki editör yolu örnektir; kendi kurulum yolunu yaz. Paket kayıt servisine erişilemiyorsa editörüne uygun sürümü `-UrpVersion` ile verebilirsin. Hedef klasör zaten varsa yeni proje komutu durur; mevcut projeyi güncellemek için:

```powershell
.\Install-Bidwarss.ps1 -ProjectPath 'D:\UnityProjects\Bidwarss'
```

Kurulum mevcut Bidwarss kaynaklarını ve proje ayarlarını `.bidwarss-backups` altında yedekler. Kullanıcının Generated/GeneratedV2 varlıklarını silmez. Aynı adlı kaynak dosyalarını günceller. Kurulum sırasında Unity kapalı olsun.

Unity'de paketler derlendikten sonra **Bidwarss > Create Gameplay Scene**. Sahne `Assets/Bidwarss/GeneratedV2/Warehouse.unity`; Play > Oda Kur. V2 klasörü varsa üretici üzerine yazmaz, mevcut sahneyi aç. V1 `Generated` sahnesi eski sürümdür; V2 sahnesini kullan. Hata durumunda proje klasörünün yanındaki `bidwarss-create.log` / `bidwarss-build-scene.log` dosyalarına bak.

## Oyun döngüsü

- Varsayılan: 10 kasa, 120 eşya, 12 istif paleti, 1–4 oyuncu.
- Her eşya türünün **deponun tamamındaki adedi** 10'un katıdır. Tek kasadaki adet 10 olmak zorunda değildir; kasa başına karışık türler çıkar.
- Dağılım oyun başında seed ile hazırlanır. Açılmamış eşyanın türü, durumu ve fiyatı istemciye gönderilmez. Palet tabelaları türlerin hedef adetlerini gösterir.
- E'yi 1,35 saniye basılı tut: kasa açılır, kapak hareket eder, toz ve kısa ses çıkar. Bakış uzaklaşırsa veya tuş bırakılırsa ilerleme sıfırlanır. Kasa açarken eller boş olmalı.
- E ile eşya al. Aynı türden en fazla 10 eşya taşı. Doğru paletin önündeki turkuaz alana bakıp E ile yerleştir. Her palet 10 alır; farklı durumlar aynı tür istifinde bulunabilir.
- Q ile bir eşya bırak. Son yerleştirilen eşya geri alınabilir; değer ve ilerleme geri düşer.
- Bütün kasalar açılıp bütün eşyalar doğru paletlere yerleşince sonuç kilitlenir. Kazanç, eşyaların gerçek değerlerinin toplamıdır; tekrar işlemle para çoğaltılamaz.
- Sonuç ekranında durum dökümü, ekip, süre, para; host için aynı seed / yeni depo. TAB sonucu açar, ESC menüyü açar. Co-op menüde durmaz.
- Günlük senaryo UTC tarihini seed yapar. Aynı katalog ve kurallar + aynı seed aynı içerikleri üretir.

## Durum ve para

Nadirlik kullanılmaz. Her türün `baseDollars` değeri ile durum çarpanı birlikte fiyatı belirler. Örnek temel değeri $100 olan masa:

| Durum | Fiyat | Varsayılan ağırlık |
|---|---:|---:|
| Rezalet | $15–30 | 8 |
| Çok kötü | $35–50 | 14 |
| Kötü | $60–85 | 20 |
| Orta | $100–150 | 27 |
| İyi | $170–220 | 18 |
| Çok iyi | $250–300 | 10 |
| Efsane | $500–600 | 3 |

Bunlar ilk denge değerleridir; katalogdan değiştirilebilir. Aralık içinde tam sayı yüzde çekilir, temel değerle çarpılıp tam dolara yuvarlanır (aşağı). Süre kaydedilir, kazançtan düşülmez. Aynı para puanındaki sıralama eşitliğinde kısa süre öne gelir.

## Kendi eşyalarını eklemek

`GeneratedV2/ItemCatalog.asset` içindeki her kayıt: kalıcı ve benzersiz `key`, görünen `title`, `baseDollars`, seçim ağırlığı `selectionWeight`, en fazla onlu grup sayısı `maxGroups`, renk ve isteğe bağlı `visualPrefab`.

Prefab sadece görseldir; NetworkObject/oynanış scripti ekleme. Çocuk collider'ları devre dışı bırakılır, görünüm taşıma hücresine otomatik ölçeklenir; gerçek etkileşim collider'ını oyun sağlar. Beş geçici ayna/masa/sandalye/radyo/lamba silueti model bağlanana kadar kullanılır. Durum ayrı renk mührü ile gösterilir; kendi modelinin malzemesi boyanmaz.

Warehouse State üzerindeki `totalGroups` toplam eşya sayısını 10'lu gruplarla belirler. Şimdiki sahne 12 palet içerir; artırırken `slots` ve `stackLabels` dizilerini ve fiziksel paletleri de büyüt. Kod sınırları 300 eşya, 20 kasa, 16 türdür. Katalog kapasitesi hedef grupları karşılamazsa oyun açıklayıcı hatayla başlamaz. Özel sahnede eşya çıkarma alanlarını ve bağlantı kopması kurtarma alanını boş tut.

## Co-op

Unity Netcode for GameObjects 2.7+, Unity Transport, host/server otoritesi. Sahne durumunu sunucu üretir; istemci yalnızca hareket ve etkileşim niyeti gönderir. Sunucu mesafe/bakış/engel, sahiplik, istif türü ve kapasitesini doğrular. Geç katılan oyuncu güncel durumu alır; ayrılan oyuncunun eşyaları girişteki kurtarma alanına bırakılır.

Editor + ayrı Windows build ile test et. Aynı PC'de `127.0.0.1`, aynı ağda host'un IPv4 adresi. UDP 7777 kullanılır. İnternette doğrudan bağlantı için ağın buna izin vermesi gerekir. Relay/Steam daveti, otomatik NAT geçişi, host devri ve yarım kalmış depo kaydı bu sürümde yok. Host ayrılırsa oturum biter. Hareket sunucu otoritelidir; yüksek gecikmede istemci tahmini henüz yok. Herkes aynı paket sürümleri ve kataloğu kullanmalı; kural hash'i farklıysa bağlantı reddedilir.

## Rekorlar

Yerel ilk 100 sonuç `Application.persistentDataPath/bidwarss-results-v2.json` içinde kalır; yerel dosyalar doğrulanmış dünya puanı sayılmaz.

**Dünya sıralaması henüz yayında değil.** Çalıştırılabilir servis ve Unity istemcisi `LeaderboardServer/` ile hazır. Kurulum: [LeaderboardServer/README.md](LeaderboardServer/README.md). Bağlı servis olmadan Dünya sekmesi bunu açıkça gösterir. Dünya kayıtları aynı kural hash'i ve ekip büyüklüğüne göre ayrılır; HTTP API ayrıca seed filtresi destekler. Oyuncu isimleri kullanıcı tarafından yazılır, doğrulanmış hesap kimliği değildir.

## Doğrulama

```sh
dotnet run --project Tests/DomainTests.csproj
python -m unittest discover -s LeaderboardServer -v
```

2.000 seed üzerinde 266.033 C# kontrolü geçti. Skor servisinin kimlik doğrulama, tekrar gönderim, çakışma, sıralama, filtreleme ve HTTP okuma testleri geçti. Bu çalışma ortamında Unity Editor bulunmadığından Unity derleme/Play Mode ve iki gerçek istemcili oturum henüz çalıştırılmadı. [Manuel kontrol listesi](Documentation/VALIDATION.md).
