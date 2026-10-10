# Bidwarss — co-op depo düzenleme prototipi

Önceden satın alınmış 10 kasayı aç, içindekileri türüne göre 10'lu gruplarda yerleştir ve depo bitince toplam kazancını gör. **Açık artırma yok.** Unity 6 / Universal RP için kaynak paketi; sahne ve örnek varlıklar editörde üretilir.

## Hızlı kurulum — C diski

Repo klasöründe `git pull --ff-only` çalıştır. Unity kapalıyken PowerShell'de:

```powershell
.\New-BidwarssProject.ps1 -ProjectPath 'C:\UnityProjects\Bidwarss'
```

Bu komut kurulu Unity 6'yı bulur (Windows, macOS ve Linux; macOS/Linux için PowerShell 7 `pwsh` gerekir), Unity paket kayıt servisinden editöre uygun kararlı URP sürümünü seçer, Hub'ın şablon ekranını kullanmadan proje oluşturur, kaynakları kurar, paketleri çözüp script'leri derleyen ayrı bir geçiş yapar ve sahneyi üretir. Derleme hatası olursa sahne üretimine geçmeden kendi logunda (`bidwarss-import.log`) durur. Editörü otomatik açmasını istemezsen `-NoLaunch` ekle. Unity lisansı etkin olmalı ve paket indirmek için internet gerekir. Editör farklı diskteyse:

```powershell
.\New-BidwarssProject.ps1 -ProjectPath 'C:\UnityProjects\Bidwarss' -EditorPath 'D:\Unity\6000.5.0f1\Editor\Unity.exe'
```

İkinci komuttaki editör yolu örnektir; kendi kurulum yolunu yaz. Paket kayıt servisine erişilemiyorsa editörüne uygun sürümü `-UrpVersion` ile verebilirsin. Hedef klasör zaten varsa yeni proje komutu durur; mevcut projeyi güncellemek için:

```powershell
.\Install-Bidwarss.ps1 -ProjectPath 'C:\UnityProjects\Bidwarss'
```

Kurulum mevcut Bidwarss kaynaklarını ve proje ayarlarını `.bidwarss-backups` altında yedekler. Kullanıcının Generated/GeneratedV2 varlıklarını silmez. Aynı adlı kaynak dosyalarını günceller. Repoda silinen veya adı değişen kaynaklar projede kalıp çift tür hatası vermesin diye kurucu kopyaladığı dosyaları projede `.bidwarss-installed.txt` listesinde tutar; bir sonraki kurulumda artık kaynakta olmayanları silmez, yedek klasörüne (`Removed/`) taşır. Kurulum sırasında Unity kapalı olsun.

Unity'de paketler derlendikten sonra **Bidwarss > Build Uploaded Depot (Co-op)**. Yukledigin depo ile yeni sahne `Assets/Bidwarss/GeneratedDepot/Bidwarss_Depot.unity` altinda olusur. Tekrar calistirirsan eski sahne korunur, yeni numarali kopya olusur. Basit eski prototip sahnesi `Assets/Bidwarss/GeneratedV2/Warehouse.unity`; Play > Oda Kur. V2 klasöründe tamamlanmış sahne varsa üretici üzerine yazmaz, mevcut sahneyi aç. Yarım kalmış (sahnesi olmayan) bir üretim `GeneratedV2_incomplete` altına taşınır ve temiz yeniden üretilir; hiçbir şey silinmez. Sahne üretildiğinde Console'a `Leaderboard rules hash` yazılır; sonradan görmek için **Bidwarss > Print Leaderboard Rules Hash**. V1 `Generated` sahnesi eski sürümdür; V2 sahnesini kullan. Hata durumunda proje klasörünün yanındaki `bidwarss-create.log` / `bidwarss-import.log` / `bidwarss-build-scene.log` dosyalarına bak.

## Oyun döngüsü

- Varsayılan: 10 kasa, 120 eşya, 12 istif paleti, 1–4 oyuncu.
- Her eşya türünün **deponun tamamındaki adedi** 10'un katıdır. Tek kasadaki adet 10 olmak zorunda değildir; kasa başına karışık türler çıkar.
- Dağılım oyun başında seed ile hazırlanır. Açılmamış eşyanın türü, durumu ve fiyatı istemciye gönderilmez. Palet tabelaları türlerin hedef adetlerini gösterir.
- E'yi 1,35 saniye basılı tut: kasa açılır, kapak hareket eder, toz ve kısa ses çıkar. Bakış uzaklaşırsa veya tuş bırakılırsa ilerleme sıfırlanır. Kasa açarken eller boş olmalı.
- E ile eşya al. Aynı türden en fazla 10 eşya taşı. Doğru paletin önündeki turkuaz alana bakıp E ile yerleştir. Her palet 10 alır; farklı durumlar aynı tür istifinde bulunabilir.
- Q ile bir eşya bırak. Son yerleştirilen eşya geri alınabilir; değer ve ilerleme geri düşer.
- Bütün kasalar açılıp bütün eşyalar doğru paletlere yerleşince sonuç kilitlenir. Kazanç, eşyaların gerçek değerlerinin toplamıdır; tekrar işlemle para çoğaltılamaz.
- Sonuç ekranında durum dökümü, ekip, süre, para; host için aynı seed / yeni depo. TAB sonucu açar, ESC menüyü açar. Co-op menüde durmaz.
- Günlük senaryo UTC tarihini seed yapar (makinenin kültüründen bağımsız, Miladi takvim). Aynı katalog ve kurallar + aynı seed aynı içerikleri üretir.
- Gamepad: sol çubuk hareket, sağ çubuk bakış, A = E (kasa için basılı tut), B = Q, Start = ESC, Geri = TAB. Menüler fare ister.
- Ayarlar (ana menü ve mola menüsü): fare/çubuk hassasiyeti, ses, müzik, görüş alanı. Bu bilgisayarda kaydedilir, ağa gitmez.
- Ses çalışma anında üretilir: kasa açılışı, alma/koyma/bırakma, adımlar ve ortam müziği. Depoda ses dosyası yoktur; yazılı sesler sonra aynı çağrılara bağlanabilir.

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

Unity Netcode for GameObjects 2.13.3+, Unity Transport, host/server otoritesi. Sahne durumunu sunucu üretir; istemci yalnızca hareket ve etkileşim niyeti gönderir. Sunucu mesafe/bakış/engel, sahiplik, istif türü ve kapasitesini doğrular. Geç katılan oyuncu güncel durumu alır; ayrılan oyuncunun eşyaları girişteki kurtarma alanına bırakılır.

Editor + ayrı Windows build ile test et. Adres alanına IPv4 adresi veya alan adı yazılır, istenirse `:port` eklenir (`192.168.1.10:7800`); aynı PC'de `127.0.0.1`. Varsayılan UDP 7777'dir ve ODA KUR aynı alandaki portu dinler. Host mola menüsünde (ESC) odayı kilitleyebilir (yeni oyuncu alınmaz) ve bir oyuncuyu odadan atabilir. Başsız sunucu (`-bidwarssServer`) depo bitince sonucu 30 saniye gösterir, skor yüklemesi bitince yeni depoyu kendisi başlatır: [DedicatedServer/README.md](DedicatedServer/README.md). İnternette doğrudan bağlantı için ağın buna izin vermesi gerekir. Relay/Steam daveti, otomatik NAT geçişi, host devri ve yarım kalmış depo kaydı bu sürümde yok. Host ayrılırsa oturum biter. Hareket sunucu otoritelidir; yüksek gecikmede istemci tahmini henüz yok. Herkes aynı paket sürümleri ve kataloğu kullanmalı; kural hash'i farklıysa bağlantı reddedilir.

## Rekorlar

Yerel ilk 100 sonuç `Application.persistentDataPath/bidwarss-results-v2.json` içinde kalır; yerel dosyalar doğrulanmış dünya puanı sayılmaz.

**Dünya sıralaması henüz yayında değil.** Çalıştırılabilir servis ve Unity istemcisi `LeaderboardServer/` ile hazır. Kurulum: [LeaderboardServer/README.md](LeaderboardServer/README.md). Bağlı servis olmadan Dünya sekmesi bunu açıkça gösterir. Dünya kayıtları aynı kural hash'i ve ekip büyüklüğüne göre ayrılır; HTTP API ayrıca seed filtresi destekler. Oyuncu isimleri kullanıcı tarafından yazılır, doğrulanmış hesap kimliği değildir. Rekor tablosunda 1–4 kişi, Günlük (bugünün seed'i) ve yerel sekmede "Bu kurallar" süzgeçleri vardır; Dünya sekmesi aynı süzgeçlerle sunucudan ister. Servis hız ve bağlantı sınırı, yasak kelime listesi, isteğe bağlı TLS, sayfalama ve Docker/systemd dosyaları içerir.

## Build

**Bidwarss > Build > Windows Client / Dedicated Server (Windows) / Dedicated Server (Linux)** `Builds/` altına çıktı verir; komut satırı: `Unity -batchmode -quit -projectPath <proje> -executeMethod Bidwarss.Editor.BuildTools.LinuxServer`. Dedicated Server için Unity Hub'dan ilgili modül kurulu olmalı.

## Doğrulama

```sh
dotnet run --project Tests/DomainTests.csproj
python -m unittest discover -s LeaderboardServer -v
python -m pip install numpy
python -m unittest discover -s Tests -p "test_*.py" -v
```

`.github/workflows/ci.yml` bunları, PowerShell sözdizimini ve skor servisi Docker imajını her push'ta çalıştırır. `Tests/test_repo_hygiene.py` Unity gerektirmeden `.meta`/GUID bütünlüğünü, parantez dengesini, bileşen dosya adlarını ve Domain'in motor bağımsızlığını denetler. Unity derleme/Play Mode ve iki gerçek istemcili oturum hâlâ elle doğrulanmalıdır. [Manuel kontrol listesi](Documentation/VALIDATION.md).

### Unity 6.5 EntityId / CS0619 hatasi

Eski Netcode 2.7 paketi yeni EntityId API ile derlenmeyebilir. Bagimlilik tabani resmi Unity registry surumu 2.13.3 olarak guncellendi. Unity kapaliyken repoda `git pull --ff-only`, ardindan `Install-Bidwarss.ps1 -ProjectPath` ile mevcut projeyi guncelle. Kurucu manifesti yedekler, eski Netcode surumunu yukseltir ve daha yeni semantik surumu korur. Unity yeniden acilinca Package Manager paketleri cozer. `Library/PackageCache` icindeki kaynaklari elle degistirme. Bu paket guncellemesi Unity Editor ortaminda henuz derlenmedi.

## Karakter ve ic mekan kurulumu

Guncel kaynaklar, eski uretilmis sahnede de kapsul gorunumu yerine baretli depo calisani olusturur. Diger oyuncular yuruyus ve tasima pozunu gorur; yerel kamera iki is eldiveni gosterir. Bunlar rig gerektirmeyen gecici toon parcalardir, nihai karakter/animasyon assetleri degildir. Eski paletlerin tamami E etkilesimi kazanir; ahsap latalarla gorunur, yerlestirme ve esya yonu palet rotasyonunu takip eder.

Yuklenen DepoLevel_Unity.zip artik Assets/DepoLevel altinda entegredir. Asagidaki genel araclar baska mekanlar icin de kullanilabilir:

- **Bidwarss > Interior > Export Current Scene For Setup**: acik, kayitli sahneyi ve bagli varliklarini unitypackage olarak disa aktarir. Mekani birlikte duzenlemek icin bu dosyayi paylas.
- **Create Playable Copy Of Current Scene**: once temel Warehouse sahnesi uretilmis olmali. Kendi mekan sahneni ac ve bu menuyu kullan; kaynak sahneyi koruyarak yeni Bidwarss_Playable sahnesine kasa, palet, kamera, oyuncu ve ag sistemlerini ekler. Prototip duvar/zeminini eklemez. **Mekanin geometrisini analiz edip otomatik yerlesim yapmaz.** Ornek konumlardaki kasa/paletleri, kapak menteselerini, esya cikis noktalarini ve tabelalari mekanina gore yerlestir.
- **Add Mesh Colliders To Selection**: Hierarchy'de sabit mekan kokunu sec. Mevcut collider'lari koruyarak uygun mesh parcalarinda non-convex MeshCollider olusturur. Tek buyuk kutuyla kapi bosluklarini kapatmaz. Animator/Animation/Rigidbody/NetworkObject altindaki hareketli parcalar atlanir. Cam, dekor ve mevcut collider kalitesi ayrica kontrol edilmeli; undo desteklenir, sahneyi kaydet.

Session Menu > Spawn Points alanina 4 bos Transform atayarak oyuncu giris noktalarini belirle. Warehouse State > Recovery Origin ayrilan oyuncularin esyalarinin birakilacagi bos alanin baslangicidir; grid saga 23.4 metre, geriye en cok 3.22 metre uzanabilir (300 esya sinirinda). Varsayilan 120 esyada 0.92 metre derinlik gerekir. Bu alan model duvarlarina gelmemeli. Q birakma zemini raycast ile bulur. Palet, crate origin, kamera ve spawn yerlesimi icin gercek model ile Play Mode kontrolu gerekir.

Bu eklemede C# sozdizimi kontrolu ve mevcut domain testleri gecti; Unity derlemesi ve goruntu/animasyon dogrulamasi bu ortamda yapilmadi.

## Yuklenen deponun oynanabilir surumu

**Bidwarss > Build Uploaded Depot (Co-op)** kaynak paketteki DepoLevel v7 tasarimini kurup oyuna baglar. Yeni sahnede Play > Oda Kur; diger oyuncu ayni sahnenin build'iyle katilir.

- Orijinal 120 x 90 m ana depo, konteyner koridoru, duvar dokulari, graffiti, raflar ve bolumler korunur. 23.443 kaynak dugum, 5.987 orijinal slot vardir.
- On mevcut konteyner kasa olur. Kapilara E basili tutulunca iki kanat konteynerin icine dogru acilir; koridora salinmaz. Kapilarin collider'lari hareket eder. Acilma zamani agdan paylasilir; gec katilan oyuncu ayni durumu gorur.
- Her konteynerin girisinden 4 m iceride, tabanin ustunde esyalar belirir. Esya toplam adedi/durum/fiyat kurallari aynidir.
- Ilk 120 esya icin girise yakin **12 mevcut standart raf** aktiftir; her raftaki 10 gercek hucreye birer esya yerlestirilir. Raf tabelalari hedef turu ve dolulugu gosterir. E icin raf iskeletini veya on seridini hedefle. Diger raflar tasarimin parcasi olarak korunur; yeni esya listesi geldiginde kategorilere gore genisletilir.
- Shelf hucrelerine sigmasi icin istiflenmis gecici modeller %85 olcekte gosterilir; elde ve zeminde normal olcek kullanilir. Buyuk nihai mobilya modelleri icin ayri boyut/yerlesim tasarimi gerekir.
- 4 oyuncu guney girisinde dogar. Ayrilan oyuncunun esyalari 10 numarali konteynerin arka tarafindaki kurtarma alanina tasinir; 300 esya icin ayrik yer ayrilmistir.
- Mekanin kutu/parca collider'lari korunur; raf bosluklarina buyuk kutu eklenmez. Tum slot trigger'lari ve ayri test oyuncusu kurulmaz. Animasyonlu kapilar static batching disindadir.
- Ilk oyun icin light bake gerekmez: toon shader + ortam/ana isik kullanilir. Orijinal baked tavan lambalari daha sonra final aydinlatma bake'i icin korunur. Performans/FPS ve nihai isik gorunumu Unity'de olculmelidir.

Depo verisi `.json.gz` olarak kayipsiz saklanir; editor kurucu okurken acar. Daha once elle import edilmis `Data/DepoLayout.json` varsa kurucu onu oncelikli okur. Kurulum mevcut DepoLevel klasorunu yedekler, var olan .meta GUID'lerini korur. Kaynak ZIP'in Web onizlemesi oyuna dahil edilmez; tum Unity dokulari dahildir.

Ag protokolu kapilarin zaman bilgisini tasimak icin 3 oldu; tum oyuncular yeni build kullanmali.

Dogrulama: `python -m pip install numpy` ardindan `python -m unittest discover -s Tests -p test_depot_layout.py -v`. Bu kontroller veri geometrisi uzerindedir; Unity Editor derlemesi, goruntu, fizik ve iki bilgisayarli oyun testi burada yapilmamistir.
