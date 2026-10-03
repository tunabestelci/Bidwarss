# Bruno — Bidwarss karakteri

Onaylanan turuncu yelekli taslaktan üretilen özgün 3D karakter. Gerçek mesh, iskelet ve animasyon içerir.

## Önizleme

`Bruno-Preview.html` dosyasını indirip masaüstü tarayıcısında açın. İnternet veya kurulum gerekmez. Sürükleyerek döndürün, tekerlekle yaklaşın. Bekle, Yürü, Taşı, Taşı + Yürü ve Selam ver düğmeleri çalışır. Nefes, göz kırpma ve baş hareketi varsayılan olarak açıktır. Tarayıcı materyali Unity shader'ının uyarlamasıdır; sahne ışıkları nedeniyle birebir aynı piksel sonucu beklenmez.

## Mevcut Unity projesine kurulum

Unity 6 / URP ve mevcut Bidwarss kaynakları gereklidir. Bu paket yeni bir tam Unity projesi değildir.

1. Unity'yi kapatıp ZIP'i ayrı bir klasöre çıkarın.
2. PowerShell'de paketin `Install-Bruno.ps1 -ProjectPath 'C:\UnityProjects\Bidwarss'` komutunu çalıştırın.
3. Unity'yi açıp import/derlemenin bitmesini bekleyin. Materyaller, Animator ve prefab ilk import sonunda otomatik hazırlanır.
4. **Bidwarss > Bruno > Open Character Preview** ile ayrı inceleme sahnesini açın, Play'e basın. Sağ fare tuşu döndürür; 1–4 taşıma/yürüme, 5 maket bıçağı, 6 çivi sökücü, E selamı seçer.
5. Mevcut depo sahnesinde yeni oturum açın. Diğer oyuncular Bruno olarak görünür; yerel oyuncu birinci şahısta Bruno'nun ellerini görür.

Otomatik hazırlık tamamlanmazsa **Bidwarss > Bruno > Build Character Assets** komutunu çalıştırın. İnceleme sahnesi her açılışta benzersiz adla kaydedilir; depo sahnesinin üzerine yazmaz. Kurucu dosyaları `.bidwarss-backups` altında yedekler. Güncellenecek dosyalarda başka değişiklik varsa üzerine yazmadan durur; paketteki patch birleştirilmelidir.

## İçerik

- `Bruno.fbx`: 18 kemikli Generic rig, 38.436 üçgen, iki skinned mesh, UV, 17 materyal.
- `BrunoHands.fbx`: aynı modelden çıkarılmış birinci şahıs elleri/ön kolları.
- `Idle`, `Walk`, `CarryIdle`, `CarryWalk`, `Greet`, `BoxCut`, `Pry`: yedi yerinde animasyon; root motion kapalı.
- `Blink_L`, `Blink_R`: gerçek göz kapağı blendshape'leri.
- `BrunoCel.shader`: üç tonlu mat yüzey, serin gölge rengi, ince kontur, gölge/derinlik geçişleri.
- `BrunoMotion.cs`: mevcut hareket ve taşıma durumuna göre geçiş; rastlantısal göz kırpma ve sınırlı baş hareketi.
- Düzenlenebilir Blender dosyası, tekrar üretilebilir modelleme betiği, GLB ve bağımsız HTML önizleme.

Prefab 0,82 ölçekle yaklaşık 1,80 m boyundadır. Mevcut kamera yüksekliği, CharacterController ve ağ otoritesi değiştirilmez. Yürüyüş/taşıma mevcut çoğaltılmış durumdan hesaplanır; kozmetik bakış ve göz kırpma istemcilerde birebir eşzamanlı değildir. Selam önizlemede kullanılabilir; oyun içi ağ emote sistemi eklenmemiştir.

## Doğrulama ve sınırlar

Blender 4.2'de FBX tekrar içe aktarıldı: 18 kemik, iki mesh, göz kapağı şekilleri, yedi animasyon take'i, normalleştirilmiş ağırlıklar ve sonlu animasyon koordinatları kontrol edildi. Chromium'da yedi hareket, göz kapağı kapanması ve kontroller görsel olarak incelendi; JavaScript/WebGL hatası görülmedi. C# dosyaları Tree-sitter sözdizimi kontrolünden geçti.

**Bu ortamda Unity Editor yok: Unity/shader derlemesi, Play Mode, iki gerçek istemcili oturum ve FPS ölçümü yapılmadı.** Oyunda son kabul bunlardan sonra yapılmalıdır. Konuşma/lip-sync, parmak rig'i, Humanoid retargeting ve LOD bu sürümde yoktur. Materyal slotları nedeniyle iki skinned mesh, iki draw call anlamına gelmez.

## Yeniden üretme

Repo kökünde:

```bash
blender -b --python Tools/Bruno/build_bruno.py
blender -b --python Tools/Bruno/build_tools.py
blender -b --python Tools/Bruno/validate_fbx.py
cd Tools/Bruno
npm install
npm run build
```

Model üretimi yalnızca kendi Bruno çıktılarını günceller. Oyun sahnelerini değiştirmez.

Teknik başvuru: [Unity ModelImporter](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityeditor/modelimporter), [URP gölge yöntemleri](https://docs.unity.cn/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-shadows.html).

## v2 — eşya tutuşu ve araçlar

Eşyalar artık oyuncunun sağındaki sabit dünya noktasına değil, iki avuçla ortak destek düzlemine bağlanır. 1–10 eşya için aralarında boşluk olan kompakt düzen kullanılır; çoklu eşyalar yalnızca eldeyken küçültülür, bırakıldığında asıl ölçeğe döner. İlk alma 0,14 saniyede ele gelir; sonrasında kamera hareketinde konum gecikmesi uygulanmaz. Üçüncü şahısta iki kemikli kol IK'sı bilekleri aynı destek noktalarına getirir.

Maket bıçağı ve gerçek çatallı çivi sökücü meshleri eklenmiştir. Sağ el kavrama şekline geçer, sol el yüzeyi destekler. Araç sapı ve bilek aynı pozdan hesaplanır. Kesme boyunca uç yüzeyde yana ilerler; sökmede kaldıraç ucu etrafında dönüş olur. Araç değişiminde bilek pozları karıştırılır.

Kasa üzerinde `CrateOpeningProfile` varsa profil geçerlidir: `Hands` yalnız kapı; `BoxCutter` bant; `PryBar` çivi; `CutThenPry` önce kesme, sonra sökme. Profil bulunmayan eski kasalar varsayılan `CutThenPry` kullanır. Gerçekten yalnız kapı olan nesnelere `Hands` seçilebilir. Bu güncelleme kasaların görsel yüzeyine yeni bant/çivi dekoru eklemez.

Araç kullanımı **E basılı tut** ile otomatik başlar. Araçlı kullanımda yüzeye 0,8 m içinde yaklaşmak ve elleri boşaltmak gerekir. İşlem doğrulanınca yürüme durur. E'yi bırakmak, menüyü açmak veya hedeften uzaklaşmak işlemi iptal eder. Kesme+sökme en az 3,2 saniye; tek araç en az 1,6 saniyedir. Kasa ancak sunucu ilerlemeyi tamamlayınca açılır; animasyon kendi başına eşya üretmez.

Temas noktası, yüzey normali, seçili işlem profili ve ilerleme sunucudan paylaşılır. **Ağ protokolü 4 oldu; host ve tüm istemciler aynı güncellemeyi kurmalıdır.** Önceki v1/ana dal istemcileriyle karma oturum desteklenmez.

Yeni testler: 1–10 eşya için çakışmama/taşıma hacmi, aşama sınırı, normalize araç ilerlemesi. Mevcut domain senaryolarıyla toplam **266.750 doğrulama, 2.000 senaryo geçti**. Gerçek Chromium önizlemesinde yedi klip, iki araç ve on eşyalı taşıma yüklendi. Unity Editor, shader derlemesi, gerçek FP kol görünümü ve iki istemciyle temas/iptal/geç katılım kontrolleri bu ortamda çalıştırılamadı; bunlar manuel son kabul maddeleridir.
