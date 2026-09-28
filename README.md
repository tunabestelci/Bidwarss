# Bidwarss — co-op depo düzenleme temeli

**Oyunda açık artırma olmayacak.** Oyuncular halihazırda alınmış bir depoyu birlikte düzenleyecek. Eşyaları Tuna özel olarak tasarlayacak. Gelecekte senaryolar, belirlenen sınırlar ve eşya başına 10/20/30/40 gibi adet gruplarıyla değişecek; bu dağıtım sistemi şimdilik ertelendi.

## Bu ilk commit ne içeriyor?

- Unity 6 / URP için dört oyunculuk host + client temeli (1 host + 3 client).
- WASD ve fare ile birinci şahıs hareket; E ile alma/rafa yerleştirme, Q ile zemine bırakma, ESC ile oda menüsü.
- Host tarafından doğrulanan eşya sahipliği, erişim mesafesi, dolu raf ve bırakma alanı kontrolleri.
- Late join için eşya durumunu paylaşan NetworkList; ayrılan oyuncunun eşyasını giriş alanına geri bırakma.
- Tek komutla depo sahnesi, 10 temsili kasa, 60 deneme raf yuvası ve oyuncu prefabı üreten editör aracı.
- **Sadece etkileşim testi için 10 sabit test kutusu.** Bunlar gerçek eşya tasarımı, kasa başına adet veya nihai oyun hedefi değildir. `Warehouse State > Spawn Test Items` kapatılarak kaldırılabilir.
- Üç bantlı, PBR içermeyen basit URP prototip shader'ı.

Kasalar şu an temsili ve içerik üretmiyor. Rastgele loot/ekonomi/satın alma/açık artırma sistemi yok. Önceki ZIP'teki sabit 6 eşya/kasa varsayımı bu repo sürümünde kaldırıldı.

## Kurulum

Bu repo henüz Unity Hub'da doğrudan açılan tam bir proje değil; **yeni bir Unity 6 Universal 3D (URP) projesine kurulacak kaynak temelidir**. Yerel Unity sürümünle sahne ve proje ayarları oluşturulur.

1. Kaynakları al:

   ```bat
   git clone https://github.com/tunabestelci/Bidwarss.git C:\UnitySources\Bidwarss
   ```

2. Unity Hub'da ayrı bir **Universal 3D (URP)** proje oluştur: örneğin `C:\UnityProjects\Bidwarss`. Bir kez açıp kapat.
3. PowerShell'de aşağıdaki komutu çalıştır:

   ```powershell
   C:\UnitySources\Bidwarss\Install-Bidwarss.ps1 -ProjectPath 'C:\UnityProjects\Bidwarss'
   ```

   Script çalıştırma politikası engellerse aşağıdaki manuel yolu kullan.
4. Unity'yi aç; paket çözümlemesi ve derlemeyi bekle. **Player > Active Input Handling = Both** seçili olsun. Gerekirse Unity'yi yeniden başlat.
5. Üst menü: **Bidwarss > Create Prototype Scene**.
6. `Assets/Bidwarss/Generated/Warehouse.unity` açılır. **Play > Oda kur (host)**.

Script mevcut `Assets/Bidwarss` üzerine yazmaz; manifest ve Player Settings yedeği alır. URP'nin kendi bağımlılıklarını korur. Mevcut Input System/Netcode sürümü hedef sürümden farklıysa durur; otomatik sürüm düşürmez.

### Manuel yol

Package Manager > Add package by name ile `com.unity.netcode.gameobjects` **2.7.0**, `com.unity.inputsystem` **1.14.2** kur. Unity Transport, Netcode bağımlılığı olarak gelir. Kaynaktaki `Assets/Bidwarss` klasörünü projenin `Assets` klasörüne kopyala. Both giriş ayarını yapıp sahne komutunu çalıştır.

`Packages/bidwarss-dependencies.json` bir bağımlılık listesidir; Unity'nin `manifest.json` dosyasının yerine geçmez. URP şablonunun manifestini silme. Daha yeni bir Input System zaten kuruluysa manuel kurulumda korunabilir; burada daha yeni sürümle çalışma testi yapılmadı.

## Deneme akışı

Başlangıçta girişin ilerisinde 10 renkli test kutusu görünür. Kutuyu E ile al; orta raflardaki turkuaz yuvaya bakıp E ile bırak. Bir seferde bir eşya taşınır. Dolu yuvaya ikinci eşya konmaz. Yerleştirilmiş eşya tekrar alınabilir. Q ile bırakılacak alan doluysa eşya elde kalır. Sayaç test kutusu sayısından hesaplanır; final oyun sayısına sabitlenmez.

Eşyalar henüz Rigidbody fiziği kullanmaz; desteklenen konumlara yerleştirilir. Modeller, animasyonlar, fırlatma ve serbest yerleştirme bu temele daha sonra eklenebilir. Eşya boyutlarını mevcut küçük raf yuvalarına uygun tut; büyük eşyalara yönelik yerleştirme tasarımı henüz yapılmadı.

## Co-op bağlantısı

- Aynı kaynak ve üretilmiş sahne sürümünden development build al. Builder sahneyi build listesine ekler; Build Profiles'ta Warehouse'un etkinliğini kontrol et.
- Editörde host başlat. Build'i açıp `127.0.0.1` ile katıl.
- Aynı ağdaki başka PC için host'un LAN IPv4 adresini kullan. Port UDP **7777**. Gerekirse Windows'un uygulama ağ erişim istemini yanıtla.
- Dördüncü oyuncuya kadar giriş kabul edilir. Host ayrılırsa oda kapanır; menüden yeni oda kurulabilir.
- Steam, Relay, oda kodu, NAT geçişi ve host migration henüz yok. İnternet co-op'u bu teslimle doğrulanmış değildir.

## Dosyalar

| Dosya | Görev |
|---|---|
| `ItemCatalog.cs` | Geçici eşya isim/boyut/renk verisi; dağıtım mantığı içermez |
| `ItemState.cs` | Kimlik, tür, konum, taşıyan oyuncu ve raf yuvası |
| `WarehouseWorld.cs` | Host kontrollü ortak eşya durumu ve yerleştirme |
| `WarehousePlayer.cs` | Host üzerinde hareket, giriş ve sahiplik kontrollü RPC |
| `SessionMenu.cs` | Dört koltuk, IPv4 bağlantı, ayrılma/yeniden oda kurma |
| `PrototypeBuilder.cs` | Sahne, materyal, katalog, oyuncu prefabı ve prefab kaydı |
| `PrototypeToon.shader` | Basit üç bantlı prototip rengi |

## Doğrulama ve devam

Bu ortamda **Unity Editor yok**. C# sözdizimi ve JSON kontrolleri yapıldı; Unity derlemesi, shader derlemesi, PowerShell kurucusu ve gerçek multiplayer testleri çalıştırılmadı. Bu commit test edilmiş oyun build'i değildir. Client prediction olmadığından yüksek gecikmede hareket ağır hissedilebilir.

- [Doğrulama listesi](Documentation/VALIDATION.md)
- [Kapsam ve sonraki işler](Documentation/ROADMAP.md)

Üretilen sahne/prefab/materyal/katalog ve tüm `.meta` dosyalarını ileride tam proje commit'ine dahil et; `ProjectSettings`, `Packages/manifest.json` ve `Packages/packages-lock.json` da beraber gelmeli. `Library` klasörünü Git'e ekleme.

API kaynakları:
- https://docs.unity.cn/Packages/com.unity.netcode.gameobjects@2.7/manual/advanced-topics/message-system/rpc.html
- https://docs.unity.cn/Packages/com.unity.inputsystem@1.14/api/UnityEngine.InputSystem.InputSystem.html
