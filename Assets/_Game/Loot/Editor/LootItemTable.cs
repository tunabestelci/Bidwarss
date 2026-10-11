// Elimizdeki hazir esya prefab'larinin "kunyesi". Fiyat, durum (sinif) sanslari, kilo ve olculer KASA DEFTERI'nden gelir
// (https://claude.ai/artifact/V2oXCKkU7jt64FpDkaszgx). ledger = defterdeki item kimligi; defter disindaki esyalarin
// degerleri burada tahmini (orta sinif piyasa degeri + koleksiyon puani, ayni formulle 7 sinif uretilir).
// Hangi kasadan cikacagi (mask) ve kolide kacarli paketlendigi (pack) burada.
using Bidwarss;
using UnityEngine;

namespace Bidwars.Loot.EditorTools
{
    public static class LootItemTable
    {
        public sealed class Def
        {
            public string path, key, title, category;
            public Vector3 size;       // x genislik, y yukseklik, z derinlik (metre) - Kasa Defteri olculeri
            public ItemRarity rarity;  // ne siklikla cikar (koleksiyon puanindan)
            public int dollars;        // orta sinif piyasa degeri
            public bool lyingOK;
            public bool byLength;      // true: en uzun kenar = target
            public float target;       // metre
            public int mask;           // hangi kasalar (K=1, U=4, Y=8, Ko=16)
            public int pack;           // bir kasada en fazla kac tane: ahsap kasa HER ZAMAN 1 (kasa esyaya gore boyutlanir), koli 2-10
            public float kg, collector;
            public string ledger;      // Kasa Defteri item kimligi (null = defterde yok)
            public int count;          // her turda depoda kac tane (kucukler 10-20, buyukler 3-10)
            public string[] hide;      // prefab'ta gizlenecek fazlalik parcalar: nesne adi, "*son" = materyal adi bununla biter
            public int align;          // AlignAxis: capraz duran modeli en uzun ekseniyle hizala, AlignYaw: yukari eksen etrafinda duz cevir
        }

        public const int AlignNone = 0, AlignAxis = 1, AlignYaw = 2;

        // Modellerdeki fazlaliklar (model incelendi): zemin plakasi, yere uzanan kablo, bisikletin etrafindaki cicekler.
        // Bu parcalar olcuyu sisirip esyayi kuculturdu (davul 2.2 m kablo, bisiklet 1.75 m kare zemin, ZIL buzdolabi 2 m zemin).
        static readonly System.Collections.Generic.Dictionary<string, string[]> Hide = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "drum_set", new[] { "object_v4", "object_v5", "*plug_edk_t14", "*terminals_edk_t14" } },
            { "fridge_bell", new[] { "plane_010_ground", "*_ground" } },
            { "retro_bike", "pCube98 pCube112 pCube113 pCube114 pCube115 pCube116 pCube117 pCube118 pCube119 pCube120 pCube121 pCube122 pCube123 pCube124 pCube125 pCube126 pCube127 pCube128 pCube129 pCube131 pCube132 pCube133 pCube134 pCube135 pCube136 pCube138 pCube139 pCube140 pCube141 pCube142 pCube143 pCube144 pCube145 pCube146 pCube147 pCube148 pCube149 pCube150 pCube151 pCube152 pCube153 pCube154 pCube155 pCube156 pCube157 pCube158 pCube159 pCube160 pCube161 pCube162 pCube163 pCube164 pCube165 pCube166 pCube167 pCube168 pCube169 pCube170 pCube171 pCube172 pCube173 pCube174 pCube175 pCube176 pCube177 pCube178 pCube179 pCube180 pCube181 pCube182 pCube183 pCube184 pCube185 pCube186 pCube195 pCube196 pCube197 pCube198 pCube199 pCube200 pCube201 pCube202 pCube203 pCube204 pCube205 pCube206 pCube207 pCube208 pCube209 pCube210 pCube211 pCube212 pCube213 pCube214 pCube215".Split(' ') },
        };

        // Egik duran modeller: Japon kilici capraz (kasadan tasiyordu), masa 52 derece donuk, fener 14 derece donuk.
        static readonly System.Collections.Generic.Dictionary<string, int> Aligns = new System.Collections.Generic.Dictionary<string, int>
        {
            { "japanese_sword", AlignAxis }, { "katana_plain", AlignAxis }, { "flashlight", AlignAxis }, { "marquetry_table", AlignYaw },
        };

        const int K = 1, U = 4, Y = 8, Ko = 16;

        // Boyu belirleyici olan (dik duran) esyalar: model YUKSEKLIGI gercek boya getirilir (masa 76 cm, buzdolabi 170 cm).
        // Digerleri (kilic, gitar, tabanca, ayna, teker...) en uzun kenarina gore olceklenir.
        static readonly System.Collections.Generic.HashSet<string> ByHeight = new System.Collections.Generic.HashSet<string>
        {
            "trash_can", "oil_lamp", "crt_tv", "old_tv", "gramophone", "footstool", "soviet_chair", "stove", "wooden_table", "marquetry_table",
            "fridge", "fridge_open", "fridge_open_2", "china_cabinet", "grandfather_clock", "vending_machine", "vending_machine_deluxe",
            "espresso_machine", "coffee_maker", "candelabra", "pepper_mill", "rum_bottle", "floral_vase", "vase", "vase_oldart", "table_clock",
            "disco_ball", "drum_set", "fridge_bell"
        };

        static Def D(string path, string key, string title, string category, float w, float h, float d, ItemRarity r, int dollars, bool lying,
                     float target, int mask, int pack, float kg, float collector, string ledger, int count)
        {
            bool byHeight = ByHeight.Contains(key) || key.StartsWith("arcade");
            return new Def { path = path, key = key, title = title, category = category, size = new Vector3(w, h, d), rarity = r, dollars = dollars,
                lyingOK = lying, byLength = !byHeight, target = byHeight ? h : target, mask = mask, pack = pack, kg = kg, collector = collector, ledger = ledger, count = count,
                hide = Hide.ContainsKey(key) ? Hide[key] : null, align = Aligns.ContainsKey(key) ? Aligns[key] : AlignNone };
        }

        const ItemRarity C = ItemRarity.Common, Un = ItemRarity.Uncommon, R = ItemRarity.Rare, E = ItemRarity.Epic, L = ItemRarity.Legendary;

        // path = Assets/_Game/Items/Prefabs/ altindaki yol (uzantisiz). Olculer gercek hayat (oyuncu 1.70 m).
        // Kasa duzeni: kucuk/ozel esyalar karton koliden (maket bicagi), orta boy esyalar kucuk ahsap kasadan,
        // uzun ince esyalar yatik kasadan, boy boyu esyalar yandan acilan uzun kasadan cikar.
        public static readonly Def[] All =
        {
            // ---- karton koli (maket bicagi, 3-5'li paket) ----
            // Para/banknot: otomat modelinin parcalari, oyundan kaldirildi (mask 0, adet 0: hic cikmaz; satir dursun ki katalog geri eklemesin)
            D("Pyp_Upload1", "pipe", "Ahşap Pipo", "Dekor", 0.16f, 0.06f, 0.05f, Un, 35, false, 0.16f, Ko, 5, 0.06f, 3f, "99mce0d6lv5ar08omasq", 20),
            D("PepperMill_Antique1", "pepper_mill", "Antika Biber Değirmeni", "Mutfak", 0.08f, 0.28f, 0.08f, E, 460, false, 0.28f, Ko, 4, 0.4f, 7.8f, "6u0tpktwzxtuq71r4sww", 15),
            D("VendineMachine1/Money2", "banknote", "Banknot", "Para", 0.16f, 0.01f, 0.07f, C, 20, false, 0.16f, 0, 5, 0.001f, 2f, null, 0),
            D("VendineMachine1/0", "coin_1", "Antika Para I", "Para", 0.04f, 0.004f, 0.04f, Un, 25, false, 0.04f, 0, 10, 0.01f, 4f, null, 0),
            D("VendineMachine1/0_2", "coin_2", "Antika Para II", "Para", 0.04f, 0.004f, 0.04f, Un, 30, false, 0.04f, 0, 10, 0.01f, 4f, null, 0),
            D("VendineMachine1/Money1", "banknote_bundle", "Banknot Destesi", "Para", 0.16f, 0.05f, 0.07f, C, 150, false, 0.16f, 0, 5, 0.1f, 2f, null, 0),
            D("VendineMachine1/0_1", "coin_3", "Antika Para III", "Para", 0.04f, 0.004f, 0.04f, R, 60, false, 0.04f, 0, 10, 0.01f, 5f, null, 0),
            D("VendineMachine1/1", "coin_4", "Antika Para IV", "Para", 0.04f, 0.004f, 0.04f, R, 120, false, 0.04f, 0, 10, 0.01f, 6f, null, 0),
            D("swiecznik1", "candelabra", "Mumluk", "Dekor", 0.14f, 0.35f, 0.14f, Un, 50, true, 0.35f, Ko, 4, 0.5f, 3f, "ryyqphm1wvd92kceefi0", 15),
            D("m9a1", "silenced_pistol", "M9A3 Tabanca", "Silah", 0.22f, 0.14f, 0.04f, Un, 120, false, 0.22f, Ko, 4, 0.95f, 3f, "j2kmov8pykupvpl2ceu6", 15),
            D("pistol1", "steampunk_pistol", "Vintage Tabanca", "Silah", 0.28f, 0.17f, 0.05f, E, 365, false, 0.28f, Ko, 4, 0.9f, 7f, "s3zj4rng6aok0puoz35y", 10),
            D("void_frame_studio__flashlight1", "flashlight", "El Feneri", "Araç-Gereç", 0.06f, 0.22f, 0.06f, C, 8, false, 0.22f, Ko, 5, 0.3f, 0f, "a4iv3vkone5w2jbo1ldc", 20),
            D("bottle_of_brazilian_rum___j__haberfeld_factory1", "rum_bottle", "Brezilya Romu", "İçki", 0.08f, 0.3f, 0.08f, C, 115, false, 0.3f, Ko, 4, 1.2f, 1f, "7szekwr74x4ge7csmm0o", 15),
            D("floral_vase1", "floral_vase", "Çiçek Desenli Vazo", "Dekor", 0.17f, 0.35f, 0.17f, Un, 45, false, 0.35f, Ko, 4, 1.2f, 3f, "s7w432mng6h3gqbi197o", 15),
            D("vase1", "vase", "Vazo", "Dekor", 0.15f, 0.3f, 0.15f, C, 41, false, 0.3f, Ko, 4, 1.2f, 2f, "xxwbx7960d2bbwcb3bhc", 15),
            D("Vase_Remake1", "vase_oldart", "Eski Sanat Vazo (Replika)", "Dekor", 0.2f, 0.4f, 0.2f, C, 55, false, 0.4f, Ko, 3, 1.8f, 2f, "xp23xu7sxsv2thwrun9p", 15),
            D("basic_clock_rigged1", "table_clock", "Basit Masa Saati", "Dekor", 0.22f, 0.3f, 0.09f, C, 30, false, 0.3f, Ko, 4, 1.5f, 1f, "1h8w11tp5drlagtbi1wx", 15),
            D("Saat1", "wall_clock", "Ahşap Duvar Saati", "Dekor", 0.34f, 0.55f, 0.12f, R, 175, false, 0.55f, Ko, 3, 3.5f, 5f, "0p49a380iru1b8h6uq9h", 10),
            D("espresso_coffee_machine1", "espresso_machine", "Espresso Makinesi", "Mutfak", 0.3f, 0.32f, 0.28f, C, 25, false, 0.35f, Ko, 3, 8f, 0f, "iw1mahjiviwhx3g9utuk", 10),
            D("CAFETERAsinluz1", "coffee_maker", "Filtre Kahve Makinesi", "Mutfak", 0.22f, 0.35f, 0.25f, C, 20, false, 0.35f, Ko, 3, 1.8f, 0f, "b4ne1i4dttdy3g9n4pyw", 10),
            D("void_frame_studio___radio1", "radio_v", "Retro Radyo", "Elektronik", 0.35f, 0.25f, 0.18f, C, 42, false, 0.35f, Ko, 3, 3.5f, 2f, "te3i8uqwunxs4929ijeb", 10),
            D("DiscoBall1", "disco_ball", "Disko Topu", "Dekor", 0.4f, 0.4f, 0.4f, Un, 35, false, 0.4f, Ko, 4, 1.5f, 3f, "kumre8n3kk9cgovy5363", 10),
            D("model1", "cassette_deck", "Kasetçalar (Stereo Deck)", "Elektronik", 0.43f, 0.12f, 0.3f, Un, 60, false, 0.43f, Ko, 3, 5f, 3f, null, 10),
            D("vintage_pocket_watch1", "pocket_watch", "Antika Cep Saati", "Saat", 0.04f, 0.05f, 0.02f, R, 180, false, 0.05f, Ko, 10, 0.1f, 6f, "y1uvpxnwabko8wwdk2yd", 15),
            
            D("RadioSet1", "walkie_talkie", "El Telsizi (Walkie-Talkie)", "Elektronik", 0.06f, 0.18f, 0.04f, C, 18, false, 0.18f, Ko, 5, 0.3f, 1f, "blww66lvttq19mt7rsrq", 15),
            D("revolver_requiem_inspired1", "revolver", "Revolver", "Silah", 0.04f, 0.15f, 0.3f, Un, 150, false, 0.3f, Ko, 4, 1.1f, 4f, null, 10),
            D("eiffel_tower_telephone1", "eiffel_phone", "Eyfel Kulesi Telefonu", "Elektronik", 0.38f, 0.28f, 0.21f, Un, 90, false, 0.38f, Ko, 3, 1.5f, 4f, null, 10),
            D("weathered_antique_table_mirror___game_model1", "table_mirror", "Antika Masa Aynası", "Dekor", 0.3f, 0.45f, 0.08f, Un, 85, false, 0.45f, Ko, 3, 2.5f, 4f, "v2jdzbaow7hetkx7a7q7", 10),
            D("weathered_antique_table_mirror___game_model2", "table_mirror_copy", "Antika Masa Aynası (yinelenen model)", "Dekor", 0.3f, 0.45f, 0.08f, Un, 85, false, 0.45f, 0, 3, 2.5f, 4f, null, 0),
            D("DJ_set_from_ep1", "dj_set", "DJ Seti", "Elektronik", 0.6f, 0.12f, 0.4f, C, 180, false, 0.6f, Ko, 2, 8f, 1f, "flfdvohb2cvg03p9ik8z", 8),
            // ---- kucuk ahsap kasa ----
            D("SM_Trash_Can1", "trash_can", "Çöp Kovası", "Ev", 0.38f, 0.5f, 0.38f, C, 12, false, 0.55f, K, 1, 1.8f, 0f, "xht7d9h4ubudkgg1wtjk", 8),
            D("old_wheel1", "old_tire", "Eski Lastik", "Araç-Gereç", 0.68f, 0.68f, 0.22f, C, 15, true, 0.68f, K, 1, 9f, 0f, null, 8),
            D("wheel___lowpoly1", "wheel", "Tekerlek", "Araç-Gereç", 0.65f, 0.65f, 0.2f, C, 8, true, 0.65f, K, 1, 4f, 0f, "6u9uvhkgpdwbgqgf1oef", 8),
            D("lamp_hipoly1", "oil_lamp", "Gaz Lambası", "Dekor", 0.2f, 0.4f, 0.2f, Un, 45, false, 0.4f, K, 1, 1.2f, 3f, null, 8),
            D("television1", "crt_tv", "Tüplü Televizyon", "Elektronik", 0.6f, 0.52f, 0.5f, C, 40, false, 0.6f, K, 1, 22f, 2f, null, 8),
            D("TV1/tv", "old_tv", "Eski TV", "Elektronik", 0.66f, 0.46f, 0.54f, C, 50, false, 0.66f, K, 1, 28f, 2f, null, 8),
            D("TV1/video_vhs", "vhs_player", "Video Oynatıcı (VHS)", "Elektronik", 0.43f, 0.1f, 0.3f, C, 35, false, 0.43f, K, 1, 4f, 2f, null, 8),
            D("Panasonic_S_XBS_low1", "boombox", "Radyo Teyp", "Elektronik", 0.62f, 0.32f, 0.2f, Un, 80, false, 0.62f, K, 1, 6f, 3f, null, 10),
            D("old_military_radio_clean_for_sketchfab2", "army_radio", "Eski Askeri Telsiz", "Elektronik", 0.5f, 0.35f, 0.3f, Un, 100, false, 0.5f, K, 1, 9f, 3.8f, "bafhqt0lt6chi0cbqo8f", 8),
            D("vintage_gramophone1", "gramophone", "Antika Gramofon", "Müzik", 0.5f, 0.75f, 0.5f, R, 210, false, 0.75f, K, 1, 10f, 5.5f, "v0o7ufjgju3n498o0cqm", 8),
            D("remake___leather_footstool1", "footstool", "Deri Puf", "Mobilya", 0.5f, 0.4f, 0.5f, C, 25, false, 0.5f, K, 1, 4f, 1f, "lygjh73zdxfo293ulpg1", 8),
            D("old_soviet_chair1", "soviet_chair", "Eski Sovyet Sandalyesi", "Mobilya", 0.45f, 0.9f, 0.48f, Un, 55, true, 0.9f, K, 1, 5f, 3f, "q9nxkjz4fac60gbmffzp", 8),
            D("Stove1", "stove", "Gazlı Ocak", "Mutfak", 0.6f, 0.85f, 0.6f, C, 69, false, 0.85f, K, 1, 38f, 1f, "fg157eph2v1g0t0e535s", 5),
            D("table1", "wooden_table", "Vintage Ahşap Masa", "Mobilya", 1.3f, 0.76f, 0.8f, Un, 190, false, 1.3f, K, 1, 28f, 3f, "44cr648ib5qnicejhe0u", 5),
            D("marquetry_table1", "marquetry_table", "Kakma Desenli Masa", "Mobilya", 1.2f, 0.76f, 0.7f, E, 500, false, 1.2f, K, 1, 35f, 7f, "c39dzt84h43lsu6l35i7", 5),
            D("electronic_drum_set1", "drum_set", "Elektronik Davul Seti", "Müzik", 1.3f, 1.1f, 1.2f, C, 220, false, 1.3f, K, 1, 18f, 1f, "hizmplg8uqhg5lw7k86l", 3),
            // ---- yatik ahsap kasa (uzun seyler yatar) ----
            D("item1", "retro_bike", "Retro Bisiklet", "Araç", 0.6f, 1.05f, 1.75f, Un, 140, true, 1.75f, Y, 1, 15f, 3f, "7q2du38b3fzd9o53kgsd", 5),
            D("spy_hypersport1", "moto_model", "Spor Motosiklet", "Araç", 0.75f, 1.1f, 2.1f, E, 3200, true, 2.1f, Y, 1, 190f, 6f, null, 3),
            D("maya2sketchfab1", "electric_guitar", "Elektro Gitar", "Müzik", 0.36f, 1.02f, 0.07f, R, 400, true, 1.02f, Y, 1, 3.6f, 5f, null, 5),
            D("SM_Katana1", "katana", "Ejderhalı Katana", "Silah", 0.1f, 1f, 0.06f, L, 515, true, 1f, Y, 1, 1.2f, 8.5f, "lfvlkodkk65m02uo789z", 5),
            D("Katana1", "katana_plain", "Katana", "Silah", 0.06f, 1f, 0.03f, E, 290, true, 1f, Y, 1, 1.2f, 6.5f, "k7mvmotwy14qedjnvf10", 5),
            D("japanese_sword1", "japanese_sword", "Japon Kılıcı", "Silah", 0.08f, 1f, 0.04f, R, 235, true, 1f, Y, 1, 1.2f, 6f, "cexd0b7loulbb06sswrn", 5),
            D("echo_wall_mirror___salon_de_lecho1", "wall_mirror", "Süslü Duvar Aynası", "Dekor", 0.8f, 1.2f, 0.06f, Un, 70, true, 1.2f, Y, 1, 6f, 3f, "syj6u8yofoplalupu5nc", 6),
            // ---- uzun dik ahsap kasa (yandan acilir) ----
            D("fridge_zil1", "fridge_bell", "ZIL Buzdolabı (Sovyet)", "Mutfak", 0.57f, 1.34f, 0.68f, R, 260, false, 1.34f, U, 1, 50f, 5f, "5mmepn0ck3diupq0008z", 5),
            D("FRIDGE1/body_1", "fridge", "Retro Buzdolabı", "Mutfak", 0.7f, 1.7f, 0.72f, Un, 450, false, 1.7f, U, 1, 80f, 4f, null, 5),
            D("FRIDGE1/body", "fridge_open", "Retro Buzdolabı (Kapağı Açık)", "Mutfak", 0.7f, 1.7f, 0.72f, Un, 400, false, 1.7f, 0, 5, 80f, 4f, null, 0),
            D("FRIDGE1/body_2", "fridge_open_2", "Retro Buzdolabı (Kapağı Açık 2)", "Mutfak", 0.7f, 1.7f, 0.72f, Un, 400, false, 1.7f, 0, 5, 80f, 4f, null, 0),
            D("china_cabinet1", "china_cabinet", "Antika Vitrin Dolabı", "Mobilya", 1.1f, 1.9f, 0.45f, R, 450, false, 1.9f, U, 1, 70f, 6f, "yph14hrvqtb2jf1j93si", 5),
            D("ClockGrandfather1", "grandfather_clock", "Boy Saati (Grandfather)", "Mobilya", 0.52f, 2.1f, 0.28f, E, 450, false, 2.1f, U, 1, 55f, 7f, "twunl06pgpijjzkdv5ys", 5),
            D("VendineMachine1/Vend_Simple", "vending_machine", "Otomat", "Elektronik", 0.9f, 1.85f, 0.85f, Un, 900, false, 1.85f, U, 1, 250f, 3f, null, 5),
            D("VendineMachine1/Vend_MoreDetailed", "vending_machine_deluxe", "Otomat (Detaylı)", "Elektronik", 0.9f, 1.85f, 0.85f, Un, 1400, false, 1.85f, U, 1, 280f, 4f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_ArcadeMachineBake_Cube", "arcade_classic", "Arcade Makinesi (Klasik)", "Oyun", 0.66f, 1.8f, 0.8f, Un, 600, false, 1.8f, U, 1, 120f, 4f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_BlenderCabin", "arcade_blender", "Arcade: Blender Kabini", "Oyun", 0.66f, 1.8f, 0.8f, Un, 500, false, 1.8f, U, 1, 120f, 3f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_DefenderCabin", "arcade_defender", "Arcade: Defender", "Oyun", 0.66f, 1.8f, 0.8f, R, 1200, false, 1.8f, U, 1, 120f, 5f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_GalaxianCabin", "arcade_galaxian", "Arcade: Galaxian", "Oyun", 0.66f, 1.8f, 0.8f, R, 1300, false, 1.8f, U, 1, 120f, 5f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_MissleCabin", "arcade_missile", "Arcade: Missile", "Oyun", 0.66f, 1.8f, 0.8f, R, 1100, false, 1.8f, U, 1, 120f, 5f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_MultiCabin", "arcade_multi", "Arcade: Multi Oyun", "Oyun", 0.66f, 1.8f, 0.8f, Un, 900, false, 1.8f, U, 1, 120f, 4f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_StreetCabin", "arcade_street", "Arcade: Street", "Oyun", 0.66f, 1.8f, 0.8f, R, 1500, false, 1.8f, U, 1, 120f, 5f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_PacManCabin", "arcade_pacman", "Arcade: PacMan", "Oyun", 0.66f, 1.8f, 0.8f, R, 1800, false, 1.8f, U, 1, 120f, 6f, null, 3),
            D("Arcade_cabins1/Arcade_cabins1_TronCabin", "arcade_tron", "Arcade: Tron", "Oyun", 0.66f, 1.8f, 0.8f, E, 2500, false, 1.8f, U, 1, 120f, 7f, null, 3),
        };

        public static Def FindKey(string key)
        {
            foreach (var d in All) if (d.key == key) return d;
            return null;
        }

        public static Def Find(string path)
        {
            foreach (var d in All) if (string.Equals(d.path, path, System.StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        public static Def FindLedger(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in All) if (d.ledger == id) return d;
            return null;
        }
    }
}
