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

- **Her depo tamamen rastgeledir.** Seed; hangi konteynerlerin kullanıldığını (6–10 kasa), kaç rafın dolduğunu (8–12), hangi eşya türlerinin geldiğini, her türden kaç adet olduğunu, hangi kasada ne bulunduğunu, her parçanın durumunu ve fiyatını belirler. Boş kasa yoktur; kullanılmayan konteyner kapalı kalır ve işaretlenir. 1–4 oyuncu.
- Bir türden genelde **5, 10, 15 veya 20** adet çıkar, ara sıra 7 ya da 13 gibi tek parti de olur. **Hiçbir türden 20'den fazla çıkmaz** ve büyük/ağır parçalar sınırlanır: en uzun kenar 150 cm ve üstü ya da 60 kg ve üstü → en çok 5; 100 cm / 30 kg → en çok 10; 70 cm / 15 kg → en çok 15. 2 metrelik boy saatinden en fazla 5 tane gelir. Katalogda `maxCount` ile elle de sınırlanabilir.
- Kasaların yükü çok farklıdır (birinde 3, ötekinde 20+ parça); bir kasaya en çok 24 parça girer. Her tür, adedine göre 10'luk raflara bölünür (23 adet = 10+10+3), raf tabelası `x / kapasite` gösterir.
- Dağılım oyun başında seed ile hazırlanır. Açılmamış eşyanın türü, durumu ve fiyatı istemciye gönderilmez.
- E'yi basılı tut ve kasaya yaklaş: kasa üzerindeki `CrateOpeningProfile` açılış yolunu belirler (yoksa bantlı tahta kasa varsayılır: önce maket bıçağı, sonra çivi sökücü, en az 3,2 sn). `Hands` modunda (konteyner kapısı) eller kapı kollarını tutup gerilir, kapaklar zorlanıp titrer ve aralanır, direnç kırılınca kanatlar ardına kadar savrulur. Her modda içindekiler sırayla yay çizerek dönerek fırlar, yere çarparken ezilip toz ve talaş saçar. Bakış uzaklaşırsa veya tuş bırakılırsa ilerleme sıfırlanır. Kasa açarken eller boş olmalı.
- E ile eşya al. Aynı türden en fazla 10 eşya taşı. Doğru paletin önündeki turkuaz alana bakıp E ile yerleştir. Her palet 10 alır; farklı durumlar aynı tür istifinde bulunabilir.
- Q ile bir eşya bırak. Son yerleştirilen eşya geri alınabilir; değer ve ilerleme geri düşer.
- Kullanılan bütün kasalar açılıp bütün eşyalar doğru raflara yerleşince sonuç kilitlenir. Kazanç, eşyaların gerçek değerlerinin toplamıdır; tekrar işlemle para çoğaltılamaz.
- Sonuç ekranında durum dökümü, ekip, süre, para; host için aynı seed / yeni depo. TAB sonucu açar, ESC menüyü açar. Co-op menüde durmaz.
- Günlük senaryo UTC tarihini seed yapar (makinenin kültüründen bağımsız, Miladi takvim). Aynı katalog ve kurallar + aynı seed aynı depoyu üretir; her yeni oyunda seed yeni olduğundan kasalar ve içerikleri baştan çekilir.
- Gamepad: sol çubuk hareket, sağ çubuk bakış, A = E (kasa için basılı tut), B = Q, Start = ESC, Geri = TAB. Menüler fare ister.
- Ayarlar (ana menü ve mola menüsü): fare/çubuk hassasiyeti, ses, müzik, görüş alanı. Bu bilgisayarda kaydedilir, ağa gitmez.
- Ses çalışma anında üretilir: kasa açılışı, alma/koyma/bırakma, adımlar ve ortam müziği. Depoda ses dosyası yoktur; yazılı sesler sonra aynı çağrılara bağlanabilir.

## Durum ve para

Nadirlik ayrı tutulmaz; her eşyanın piyasa değeri (`baseValue`, "Orta" durumun merkezi) ve bir koleksiyon çarpanı vardır. Yedi durum sınıfı ("Çok kötü, Kötü, Orta, İyi, Çok iyi, Destansı, Efsanevi") wiki ile aynıdır. Çıkma şansları varsayılan olarak %30 / 26 / 20 / 13 / 7 / 3,5 / 0,5'tir; fiyat aralıkları değerden ve koleksiyon çarpanından türer, ya da wiki'de eşya başına elle yazılır. Örnek: $100'lık bir eşyada "Orta" yaklaşık $90–110, "Efsanevi" çok daha yukarıdadır; koleksiyon çarpanı yüksek eşyada üst sınıflar hızla pahalanır.

Aralık içinde tam sayı fiyat çekilir. Süre kaydedilir, kazançtan düşülmez. Aynı para puanındaki sıralama eşitliğinde kısa süre öne gelir.

## Kendi eşyalarını eklemek (wiki ile eşleşme)

Eşyalar **Kasa Defteri** wiki'sinde (`Site/kasa-defteri.html`, yayınlanmış sayfanın kaynağı) yazılır; oyun aynı formatı okur. Wiki'de Itemler sekmesinde **Oyun kataloğu** düğmesi `bidwarss-catalog` JSON dosyasını indirir (en çok 40 eşya, tam 7 durum sınıfı). Unity'de **Bidwarss > Katalog > Wiki dosyasından içe aktar (JSON)...** ile seç; `ItemCatalog` asset'i güncellenir. Dosya geçersizse nedeni Türkçe bildirilir ve hiçbir şey değişmez. **Varsayılan kataloğu yeniden uygula** `Assets/Bidwarss/Data/ItemCatalog.json` içindeki 14 eşyayı geri yükler.

Her kayıt: kalıcı `key`, `title`, `shape`, `kg`, `heightCm/widthCm/depthCm`, `baseValue`, `collector`, `selectionWeight`, `maxCount` (0 = ölçüden otomatik) ve isteğe bağlı 7 sınıf satırı (`chance`, `priceMin`, `priceMax`). Katalog ya da kural değişince kural hash'i değişir (sürüm 3); yeni hash'i skor servisine izin listesine ekle.

Wiki'nin **Depo düzeni** sekmesi oyunun depo çekimini birebir çalıştırır (aynı seed, aynı sonuç): bir seed için kasaları, raf adetlerini ve toplam değeri gösterir, 400 turluk deneme ile büyük eşyaların sınırını aşmadığını kanıtlar. Eşleşme `node Tests/depot_sim_check.js` ve C# testindeki ortak "golden" dosyayla (`Tests/golden_depots.txt`) her push'ta denetlenir.

Prefab sadece görseldir; NetworkObject/oynanış scripti ekleme. Çocuk collider'ları devre dışı bırakılır, görünüm taşıma hücresine otomatik ölçeklenir; gerçek etkileşim collider'ını oyun sağlar. Prefabı olmayan eşyalar `shape` alanındaki geçici siluetle (sandalye, masa, saat, vazo, kılıç, radyo...) gösterilir. Durum ayrı renk mührü ile gösterilir; kendi modelinin malzemesi boyanmaz.

Warehouse State üzerindeki `totalGroups` en çok kaç rafın dolacağını, `minGroups` en azını; `crateCount` / `minCrates` kullanılacak kasa aralığını belirler. Şimdiki sahne 12 raf ve 10 konteyner içerir; artırırken `slots`, `stackLabels` dizilerini ve fiziksel rafları da büyüt. Kod sınırları 300 eşya, 20 kasa, 40 tür. Katalog kapasitesi (türlerin sınırları toplamı) hedef rafları karşılamazsa oyun açıklayıcı hatayla başlamaz. Özel sahnede eşya çıkarma alanlarını ve bağlantı kopması kurtarma alanını boş tut.

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

Session Menu > Spawn Points alanina 4 bos Transform atayarak oyuncu giris noktalarini belirle. Warehouse State > Recovery Origin ayrilan oyuncularin esyalarinin birakilacagi bos alanin baslangicidir; grid saga 23.4 metre, geriye en cok 3.22 metre uzanabilir (300 esya sinirinda). Bir turda en cok 120 esya (12 raf) cikar; bu 0.92 metre derinlik gerektirir. Bu alan model duvarlarina gelmemeli. Q birakma zemini raycast ile bulur. Palet, crate origin, kamera ve spawn yerlesimi icin gercek model ile Play Mode kontrolu gerekir.

Rastgele depo, boyut siniri ve wiki esleşmesi C# domain testleri ve JS simulatoru ile dogrulandi; Unity derlemesi ve goruntu/animasyon dogrulamasi bu ortamda yapilmadi.

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

## Bruno karakteri

Özgün 3D karakter, yedi animasyon, göz kırpma, cel shader ve birinci şahıs elleri eklendi. **Bidwarss > Bruno > Open Character Preview** ile inceleyin. [Kurulum, bağımsız 3D önizleme ve doğrulama notları](Documentation/Bruno/README.md). Unity Editor/Play Mode testi bu ortamda yapılmadı.

Bruno v2: ele bağlı eşya taşıma, kol IK, maket bıçağı/çivi sökücü ve sunucu kontrollü açma aşamaları. Host ve istemciler protokol 4 sürümünü birlikte kurmalıdır.
