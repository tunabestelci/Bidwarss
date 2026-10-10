# Kasa Avcısı – Depo Level v7 (Unity)

> **Bidwarss içinde kullanım:** Bu dosya orijinal paketin belgesidir. Bidwarss'ta depo **Bidwarss > Build Uploaded Depot (Co-op)** ile kurulur; ışık bake'i, test oyuncusu ve satış/çöp tetikleri kullanılmaz. Güncel kurulum ve oyun kuralları için kök [README.md](../../README.md) geçerlidir. Aşağıdaki Level Builder adımları yalnız deponun tek başına denenmesi içindir.

Bu paket, web önizlemesindeki deponun aynısını Unity sahnesine tek tuşla kurar. Tüm ölçüler metre, eksenler Unity ile aynıdır: X doğu, Y yukarı, Z kuzey. Tek kaynak `Data/DepoLayout.json` dosyasıdır, web önizlemesi de bu dosyadan çizilir.

Hedef sürüm Unity 2021.3 ve üstüdür (2022 LTS ve Unity 6 dahil). Cel-shade görünüm **URP** içindir. Built-in veya HDRP projesinde depo yine kurulur ama standart Lit materyallerle, mürekkep çizgisi olmadan.

## v7'de neler değişti

- **Raflar genişledi.**
  - Gondol raf 2,0 × 0,6 m'den 2,6 × 0,8 m'ye çıktı. Her slot artık 1,25 m genişliğinde ve 0,7 m derinliğinde.
  - Kitaplık 2,6 × 0,5 m, lastik rafı 2,6 × 0,7 m oldu.
  - 10'lu kural aynen geçerli: her birim 10 eşya alır (alet panosu 20). Büyük eşyalar (motor, bateri, bisiklet, beyaz eşya, mobilya) bu kuralın dışında.
- **Toplam slot 9187'den 5987'ye indi (yaklaşık üçte iki):**

  | Bölüm | Slot |
  |---|---|
  | A Elektronik | 1851 |
  | D Hobi, oyuncak, spor | 1490 |
  | C Alet ve oto | 1326 |
  | B Ev ve mutfak | 692 |
  | E Giyim | 547 |
  | F Mobilya | 81 büyük alan |

- **Duvar yazısı değişti.** Güney duvardaki "AÇIK ARTIRMA" yazısı kaldırıldı, yerine "DEPO GİRİŞİ" graffitisi geldi.
- Aynı kalanlar: konteynerler (36 × 7,2 × 5,0 m, boş), koridor, bölüm yerleşimi ve standlar.

## Kurulum

1. Önceki bir sürüm kuruluysa önce eski `Assets/DepoLevel` klasörünü sil. Ardından zip'ten çıkan `Assets/DepoLevel` klasörünü projendeki `Assets` klasörüne sürükle.
2. TMP kaynakları projede yoksa **Window > TextMeshPro > Import TMP Essential Resources** menüsünü çalıştır.
3. Boş bir sahne aç ve **Tools > Depo > Level Builder** penceresinde **DEPOYU KUR** düğmesine bas. Yaklaşık 23 bin parça kurulduğu için bu işlem 1–2 dakika sürebilir.
4. Pencerede **Işıkları bake et** düğmesine bas. Bu işlem birkaç dakika sürer. Toon görünüm bake edilmiş ışıkla oturur.
5. Play'e bas. Oyun koridorun güney girişinde başlar. Kontroller:
   - WASD: yürü, Shift: koş, Space: zıpla
   - E: envanter panosuyla etkileşim

## Cel-shade nasıl çalışıyor

`Shaders/DepoCel.shader` (`Toon/DepoCel`) web önizlemesiyle aynı mantığı kullanır. Üç parçadan oluşur:

- **Bantlı ışık.** Bake edilmiş ışık (lightmap veya light probe) ve varsa ana ışık üç sert banda bölünür. Gölge tarafı hafif morumsu bir tona kayar.
- **Kenar çizgileri.** Her kutunun kenarlarına piksel kalınlığı sabit bir mürekkep çizgisi çizilir. Çizgi uzaktaki küçük yüzeylerde kendiliğinden kaybolur.
- **Dış kontur.** Ters kabuk yöntemiyle 2,8 cm kalınlığında koyu bir çerçeve çizilir. Unity'nin küp ve silindirinde köşeler yırtılmaz. Kendi modellerinde materyaldeki "Kontur: normal yönü" seçeneğini aç.

Ayarların hepsi materyal Inspector'ında durur: bant eşikleri, gölge tonu, çizgi kalınlığı ve kontur rengi. Görünümü tek yerden değiştirmek için `Assets/DepoLevel/Materials` klasöründe istediğin materyalleri seçip birlikte düzenleyebilirsin.

Zemin boyaları, yazılar ve lambalar kontur çizmez. JSON'daki `edge` ve `outline` alanları bunu belirler.

URP Forward+ kullanıyorsan gerçek zamanlı ek ışıklar bu shader'da görünmez. Varsayılan Baked modda bu sorun olmaz.

## Oyun bileşenleri

| Bileşen | Ne işe yarar |
|---|---|
| `DepoLevelRoot` | Bölge listesini tutar. `GetZoneAt(pos)` konumun bölgesini, `FindNearestFreeSlot(zone, pos, boyut)` en yakın boş slotu, `GetContainer(no)` konteyneri döndürür. |
| `DepoShelfSlot` | Yerleştirme slotu. `TryPlace(item)` eşyayı yerine koyar, `CanFit(boyut)` sığıp sığmadığını söyler, `Release()` slotu boşaltır. Türler: `raf`, `kitap`, `palet`, `lastik`, `aski`, `gitar_aski`, `gitar`, `teshir`, `kaide`, `kanca`, `motor`, `bateri`, `bisiklet`, `dik`, `sepet`, `manken`, `aksesuar`, `mobilya`, `mobilya_uzun`, `dik_stand`, `oto`, `beyaz_esya`. |
| `DepoTrigger` | Etkileşim noktası: `envanter_panosu`. Sell/Trash/Keep/Interact türleri kendi satış, çöp ve etkileşim noktaların için hazır bekler. |
| `DepoZone` | Bölge ve konteyner hacimleri. Rigidbody'li bir eşya girip çıktığında olay yayınlar. |
| `DepoContainer` | Konteyner numarası, 51 doğma noktası, `SpawnLoot(prefablar, adet, seed)` ve `ClearPlaceholders()`. |
| `DepoLootSpawn`, `DepoPlayerSpawn`, `DepoTestPlayer`, `DepoExampleHooks` | v1 ile aynı. |

Tetikler yalnızca Rigidbody'si olan nesnelerde çalışır, bu yüzden eşya prefablarına Rigidbody ve Collider ekle.

## Kendi modellerini koymak (Prefab Map)

1. **Tools > Depo > Prefab Map Oluştur** menüsünü çalıştır.
2. İstediğin anahtara kendi prefabını ata.
3. Asset'i Level Builder penceresine verip yeniden kur.

Atanan prefab o grubun kutularının yerine geçer. Slotlar, tetikler, doğma noktaları, yazılar ve yer tutucular yerinde kalır.

Grup anahtarlarında pivot, grubun orijinidir (zemin seviyesi). Aşağıdaki ölçüler grubun yerel eksenlerindeki metre değerleridir:

| Anahtar | X | Y | Z | Slot |
|---|---|---|---|---|
| Raf_Standart (gondol) | −1,30 … 1,30 | 0 … 2,40 | −0,40 … 0,40 | 10 |
| Raf_Kitap | −1,30 … 1,30 | 0 … 2,30 | −0,25 … 0,25 | 10 |
| Raf_Duvar | −1,31 … 1,31 | 0 … 3,60 | −0,45 … 0,45 | 10 |
| Raf_Palet | −1,35 … 1,35 | 0 … 2,30 | −0,56 … 0,56 | 10 |
| Lastik_Rafi | −1,30 … 1,30 | 0 … 1,80 | −0,35 … 0,35 | 10 |
| Askilik | −0,92 … 0,92 | 0 … 1,79 | −0,25 … 0,25 | 10 |
| Dik_Stand | −1,70 … 1,70 | 0 … 1,95 | −0,65 … 0,65 | 4 |
| Takim_Dolabi | −0,45 … 0,45 | 0 … 1,60 | −0,26 … 0,25 | – |
| Transpalet | −0,36 … 0,36 | 0 … 1,40 | −0,89 … 0,57 | – |
| Tasima_Arabasi | −0,45 … 0,45 | 0 … 0,14 | −0,28 … 0,28 | – |
| Gitar_Duvari | −1,50 … 1,50 | 0 … 2,55 | −0,45 … 0,45 | 10 |
| Gitar_Standi | −0,23 … 0,23 | 0 … 1,20 | −0,18 … 0,25 | 1 |
| Bateri_Podyumu | −1,30 … 1,30 | 0 … 0,25 | −1,20 … 1,20 | 1 |
| Teshir_Masasi | −1,40 … 1,40 | 0 … 0,80 | −0,55 … 0,55 | 10 |
| Kaide_Kup | −0,55 … 0,55 | 0 … 0,50 | −0,55 … 0,55 | 1 |
| Podyum | ölçü değişken (G × D × Y) | | | – |
| Delikli_Pano | −1,23 … 1,23 | 0 … 2,34 | −0,40 … 0,40 | 20 |
| Motor_Standi | −0,40 … 0,40 | 0 … 1,00 | −0,55 … 0,55 | 1 |
| Bisiklet_Rafi | −1,50 … 1,65 | 0 … 0,40 | −0,25 … 0,25 (bisikletler +Z'de) | 5 |
| Dik_Raf | −1,03 … 1,03 | 0 … 2,00 | −0,25 … 0,25 | 10 |
| Tel_Sepet | −0,60 … 0,60 | 0 … 0,90 | −0,42 … 0,40 | 10 |
| Manken | −0,23 … 0,23 | 0 … 1,90 | −0,21 … 0,21 | 1 |
| Aksesuar_Standi | −0,50 … 0,50 | 0 … 1,85 | −0,50 … 0,50 | 10 |
| Konteyner_Buyuk | 0 … 36,00 | 0 … 5,00 | −3,60 … 3,60 | – |
| Tavan_Lambasi | −0,45 … 0,45 | −1,45 … 0 | −0,45 … 0,45 | – |
| Envanter_Panosu | −1,90 … 1,90 | 0,85 … 2,95 | −0,09 … 0 | – |

Özel pivotlar:
- `Konteyner_Buyuk`: orijin kapı yüzünün ortasıdır ve koridor kenarında durur. Gövde yerel +X yönünde uzanır. Açık kapı kanatları ile numara kutusu x < 0 tarafında kalır.
- `Tavan_Lambasi`: orijin kablonun tavana bağlandığı noktadır.
- `GirisKapisi` bir kutu anahtarıdır: pivotu kutunun alt ortasıdır.
- `Envanter_Panosu`: orijin duvar yüzeyindedir, yerel −Z odaya bakar.

## Işık ve performans

- Varsayılan ışık modu Baked'tir.
- Kurucu `DepoLightingSettings` dosyasını oluşturur: Progressive GPU, 8 texel/m, AO açık.
- 0,7 m'den küçük parçalar light probe kullanır. Light probe ızgarası 4 m aralıklıdır.
- Depo çok büyük olduğu için ilk bake 10–25 dakika sürebilir. Hızlı deneme için Lighting penceresinde Lightmap Resolution değerini 3–4'e düşür. Final için 8 yeterli.
- Işık çok karanlık veya çok parlak gelirse iki yol var: Level Builder'daki "Şiddet çarpanı" ile yeniden kur, ya da materyallerde `Pozlama` değerini değiştir.
- Ek performans için Occlusion Culling bake et. Büyük duvarlar zaten Occluder olarak işaretli.

## Sık karşılaşılan sorunlar

- **Her şey pembe görünüyor.** Pipeline URP değildir ya da shader derlenmemiştir. Console'daki hatayı kontrol et. Gerekirse "Cel-shade" seçeneğini kapatıp yeniden kur.
- **Sahne düz ve karanlık.** Bake henüz yapılmamıştır.
- **Yazılar görünmüyor.** TMP Essential Resources içeri alınmamıştır.
- **Duvar resimleri.** Mural ve graffitiler Quad olarak kurulur. Yön her zaman odaya doğrudur, ters görünmez.
- **v1 materyalleri kaldı.** Kurucu, shader'ı değişen materyalleri kendiliğinden yeniler. Yine de sorun olursa `Assets/DepoLevel/Materials` klasörünü silip yeniden kur.
