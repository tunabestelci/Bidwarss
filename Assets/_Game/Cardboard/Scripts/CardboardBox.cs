// =====================================================================================
//  CardboardBox  -  bantli karton koli (senin "set of cardboard boxes" paketinin dokusuyla, yazilar silinmis).
//  4 boy var (Küçük 29x17x19, Orta 43x21x27, Küp 29x29x29, Büyük 50x36x36 cm; uzunluk x genişlik x yükseklik). Sunucu her turda kolinin boyunu
//  icindeki esyaya gore secer (esya her zaman sigar; sigan boylardan biri rastgele).
//  Maket bicagiyla (3) E basili tutulunca bant kesilir: on yuzdeki bant ucundan baslar, ustteki dikis boyunca gider,
//  arkadan iner. E birakilirsa kesik oldugu yerde kalir, tekrar basilinca devam eder (sunucu ilerlemesi).
//  Bant bitince kapaklar yaylanarak acilir, toz + mukavva kirintisi cikar. Esya kolinin icinde, kagit dolgunun
//  ustunde durur; oyuncu kendisi alir.
//  Parcalar (govde, 4 kapak, bant, dolgu, collider) koddan uretilir (Build). Prefab: Tools > Bidwars > Karton > Kurulum.
// =====================================================================================
using System;
using System.Collections.Generic;
using Bidwars.Crates;
using Bidwarss;
using UnityEngine;

namespace Bidwars.Cardboard
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Bidwars/Cardboard Box")]
    public sealed class CardboardBox : MonoBehaviour, ICrateStage
    {
        public const float Wall = 0.005f;

        // ------------------------------------------------------------------ 4 boy (paketteki kolilerin olculeri)
        // Her yuzun doku eslemesi: uv = c0 + c1*s + c2*t (s,t = yuzun iki eksenindeki konum / boy, -0.5..0.5).
        // Yuz sirasi: +X, -X, +Y (ust, bantli), -Y, +Z, -Z. Eksenler: ±X yuzu (Z,Y), ±Y yuzu (X,Z), ±Z yuzu (X,Y).
        public struct FaceUV
        {
            public Vector2 c0, c1, c2;
            public FaceUV(Vector2 a, Vector2 b, Vector2 c) { c0 = a; c1 = b; c2 = c; }
        }
        public sealed class Variant
        {
            public readonly string name;
            public readonly Vector3 size;     // x uzunluk (bant yonu), y yukseklik, z genislik (metre)
            public readonly FaceUV[] faces;
            public Variant(string n, Vector3 s, FaceUV[] f) { name = n; size = s; faces = f; }
            public float Filler { get { return Mathf.Min(0.05f, size.y * 0.15f); } }
            public float FloorY { get { return Wall + Filler * 0.6f; } }
            public float TapeWidth { get { return size.z > 0.33f ? 0.058f : 0.046f; } }
            public Vector3 Inner { get { return new Vector3(size.x - 2f * Wall, size.y - FloorY, size.z - 2f * Wall); } }
        }
        public static readonly Variant[] Variants =
        {
            new Variant("Küçük", new Vector3(0.2900f, 0.1900f, 0.1700f), new[] {
                    new FaceUV(new Vector2(0.54921f, 0.61643f), new Vector2(-0.11225f, 0.00000f), new Vector2(0.00000f, -0.12546f)),
                    new FaceUV(new Vector2(0.24776f, 0.61643f), new Vector2(0.11225f, 0.00000f), new Vector2(0.00000f, -0.12546f)),
                    new FaceUV(new Vector2(0.25830f, 0.94260f), new Vector2(-0.19189f, -0.00001f), new Vector2(0.00001f, -0.11248f)),
                    new FaceUV(new Vector2(0.46251f, 0.89714f), new Vector2(0.19189f, -0.00000f), new Vector2(-0.00000f, -0.11248f)),
                    new FaceUV(new Vector2(0.39849f, 0.61643f), new Vector2(0.19149f, 0.00000f), new Vector2(0.00000f, -0.12546f)),
                    new FaceUV(new Vector2(0.09704f, 0.61643f), new Vector2(-0.19149f, 0.00000f), new Vector2(-0.00000f, -0.12546f)) }),
            new Variant("Orta", new Vector3(0.4300f, 0.2700f, 0.2100f), new[] {
                    new FaceUV(new Vector2(0.78381f, 0.27950f), new Vector2(-0.14047f, 0.00000f), new Vector2(-0.00000f, -0.18061f)),
                    new FaceUV(new Vector2(0.35803f, 0.27950f), new Vector2(0.14047f, -0.00000f), new Vector2(0.00000f, -0.18061f)),
                    new FaceUV(new Vector2(0.41441f, 0.75824f), new Vector2(0.28812f, 0.00000f), new Vector2(-0.00001f, 0.14071f)),
                    new FaceUV(new Vector2(0.14522f, 0.44886f), new Vector2(0.28812f, -0.00000f), new Vector2(-0.00000f, -0.14070f)),
                    new FaceUV(new Vector2(0.57092f, 0.27950f), new Vector2(0.28764f, -0.00000f), new Vector2(-0.00000f, -0.18061f)),
                    new FaceUV(new Vector2(0.14514f, 0.27950f), new Vector2(-0.28764f, -0.00000f), new Vector2(0.00000f, -0.18061f)) }),
            new Variant("Küp", new Vector3(0.2900f, 0.2900f, 0.2900f), new[] {
                    new FaceUV(new Vector2(0.39657f, 0.46174f), new Vector2(0.17293f, 0.00000f), new Vector2(-0.00000f, 0.17293f)),
                    new FaceUV(new Vector2(0.74047f, 0.46174f), new Vector2(-0.17293f, 0.00000f), new Vector2(0.00000f, 0.17293f)),
                    new FaceUV(new Vector2(0.65721f, 0.84219f), new Vector2(0.17320f, 0.00000f), new Vector2(-0.00001f, 0.17320f)),
                    new FaceUV(new Vector2(0.91242f, 0.84219f), new Vector2(-0.17320f, 0.00000f), new Vector2(0.00000f, 0.17319f)),
                    new FaceUV(new Vector2(0.56852f, 0.46174f), new Vector2(-0.17293f, 0.00000f), new Vector2(0.00000f, 0.17293f)),
                    new FaceUV(new Vector2(0.91242f, 0.46174f), new Vector2(0.17293f, 0.00000f), new Vector2(-0.00000f, 0.17293f)) }),
            new Variant("Büyük", new Vector3(0.5000f, 0.3625f, 0.3625f), new[] {
                    new FaceUV(new Vector2(0.09082f, 0.09082f), new Vector2(0.18613f, 0.00000f), new Vector2(-0.00000f, 0.18613f)),
                    new FaceUV(new Vector2(0.53158f, 0.09082f), new Vector2(-0.18613f, -0.00000f), new Vector2(-0.00000f, 0.18613f)),
                    new FaceUV(new Vector2(0.12959f, 0.78097f), new Vector2(-0.25708f, -0.00001f), new Vector2(0.00001f, -0.18638f)),
                    new FaceUV(new Vector2(0.74250f, 0.65037f), new Vector2(0.25708f, 0.00000f), new Vector2(0.00000f, -0.18638f)),
                    new FaceUV(new Vector2(0.31120f, 0.09082f), new Vector2(-0.25673f, 0.00000f), new Vector2(0.00000f, 0.18613f)),
                    new FaceUV(new Vector2(0.75195f, 0.09082f), new Vector2(0.25673f, 0.00000f), new Vector2(-0.00000f, 0.18613f)) }),
        };
        public static string[] VariantNames { get { var n = new string[Variants.Length]; for (int i = 0; i < n.Length; i++) n[i] = Variants[i].name; return n; } }

        // pose 1: dik; pose 2: sirtustu yatik (boy kolinin uzunlugu boyunca). 0: sigmiyor
        public static int Fit(Variant v, Vector3 item)
        {
            Vector3 i = v.Inner;
            float a = Mathf.Max(item.x, item.z), b = Mathf.Min(item.x, item.z);
            if (a <= i.x + 1e-4f && b <= i.z + 1e-4f && item.y <= i.y + 1e-4f) return 1;
            if (item.y <= i.x + 1e-4f && item.x <= i.z + 1e-4f && item.z <= i.y + 1e-4f) return 2;
            return 0;
        }
        /// <summary>Bu esya herhangi bir boy koliye sigar mi (Loot havuzu icin).</summary>
        public static bool FitsAny(Vector3 item) { return Mathf.Max(item.x, Mathf.Max(item.y, item.z)) <= 0.7f; }

        // boyuta bagli ic olculer (koli artik icindekine gore boyutlanir)
        public static float FillerFor(float h) { return Mathf.Min(0.05f, h * 0.15f); }
        public static float FloorYFor(float h) { return Wall + FillerFor(h) * 0.6f; }
        public static Vector3 InnerFor(Vector3 size) { return new Vector3(size.x - 2f * Wall, size.y - FloorYFor(size.y), size.z - 2f * Wall); }

        [Header("Oyun")]
        [Tooltip("Sahnedeki WarehouseWorld (bos ise otomatik bulunur).")] public WarehouseWorld world;
        [Tooltip("Bu kolinin depodaki numarasi (WarehouseWorld.boxes sirasi).")] public int crateIndex;
        [Tooltip("Bandi bastan sona kesmek icin kac saniye E basili tutulmali.")] [Range(0.5f, 8f)] public float cutSeconds = 1.4f;
        [Tooltip("Su an kurulu boy (oyunda sunucu her tur secer).")] public int variant;

        [Header("Gorunum")]
        public Material boxMaterial;
        [Tooltip("Ic yuzler, kapak altlari (dokunun daha koyu kopyasi).")] public Material insideMaterial;
        [Tooltip("Kirpik kagit dolgu.")] public Material fillerMaterial;
        public Material dustMaterial;
        public Color dustTint = new Color(1f, 0.96f, 0.88f, 1f);
        public Color fiberColor = new Color(0.78f, 0.62f, 0.42f, 1f);
        [Range(60f, 170f)] public float longFlapAngle = 128f;
        [Range(60f, 170f)] public float shortFlapAngle = 112f;

        [Header("Ses")]
        public AudioClip[] cutClips = new AudioClip[0];
        public AudioClip[] flapClips = new AudioClip[0];
        public AudioClip pierceClip, ripClip, dustClip;
        [Range(0f, 1f)] public float volume = 0.9f;

        [Header("Parcalar (Build doldurur)")]
        public Transform body;
        public Transform flapLongA, flapLongB, flapShortA, flapShortB;
        public MeshFilter tapeEnds, tapeTopA, tapeTopB;
        public GameObject closedColliders, openColliders;

        // ------------------------------------------------------------------ durum
        float shownCut = -1f;      // 0..1 gosterilen kesik
        float builtCut = -2f;
        bool opening, open, released = true;
        float openTime;
        string knownRun;
        bool knownOpen;
        float nextFx, nextCutSound;
        readonly float[] angle = new float[4], vel = new float[4];
        readonly bool[] flapStarted = new bool[4];
        CrateDust dust;
        AudioSource[] voices;
        int voiceIndex;
        System.Random rng = new System.Random(7);
        Mesh tapeEndsMesh, tapeAMesh, tapeBMesh;
        readonly List<Mesh> owned = new List<Mesh>();   // oyunda uretilen meshler (yeniden kurulunca silinir)
        int builtVariant = -1;
        Vector3 dims, builtShape = new Vector3(-1f, -1f, -1f);   // kurulu dis olcu (x uzunluk, y yukseklik, z genislik)

        Variant V { get { return Variants[Mathf.Clamp(variant, 0, Variants.Length - 1)]; } }
        Vector3 Size { get { return dims.x > 0f ? dims : V.size; } }
        float length { get { return Size.x; } }
        float height { get { return Size.y; } }
        float width { get { return Size.z; } }
        float tapeDrop { get { return height * 0.19f; } }
        float tapeWidth { get { return Mathf.Clamp(Mathf.Min(width, length) * 0.2f, 0.035f, 0.058f); } }
        float T { get { return Wall; } }
        float TopY { get { return height + Wall; } }
        float PathLength { get { return 2f * (tapeDrop + Wall) + length; } }

        // ================================================================== ICrateStage
        public bool ItemsReleased { get { return released; } }
        public float WorkSeconds { get { return cutSeconds; } }
        public int WorkSteps { get { return 1; } }

        // Bu turun olcusu (sunucunun sectigi), gorsel henuz kurulmamis olsa bile.
        Vector3 CurrentSize
        {
            get
            {
                if (world != null && world.IsSpawned && crateIndex >= 0 && crateIndex < world.Crates.Count)
                {
                    var st = world.Crates[crateIndex];
                    if (st.shape.x > 0f) return st.shape;
                    return Variants[Mathf.Clamp(st.variant, 0, Variants.Length - 1)].size;
                }
                return Size;
            }
        }

        public bool Interior(out Vector3 floorCenter, out Vector3 longAxis, out float length_, out float width_, out float height_)
        {
            if (world == null) world = WarehouseWorld.Instance;
            Vector3 size = CurrentSize;
            floorCenter = transform.TransformPoint(new Vector3(0f, FloorYFor(size.y), 0f));
            longAxis = transform.right; longAxis.y = 0f; longAxis.Normalize();
            Vector3 i = InnerFor(size);
            length_ = i.x; width_ = i.z; height_ = i.y;
            return true;
        }

        // Koli icindekine gore sekillenir: 3-5'li paket yan yana (CratePacking), doku orani en yakin paketteki koliden.
        public int ChooseShape(Vector3[] cells, int seed, out Vector3 shape)
        {
            if (cells == null || cells.Length == 0) { shape = Variants[0].size; return 0; }
            Vector3 cell = cells[0];
            foreach (var c in cells) if (c.x * c.y * c.z > cell.x * cell.y * cell.z) cell = c;
            shape = CratePacking.ParcelSize(cell, cells.Length);
            // doku icin oranlari en yakin paket kolisi
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < Variants.Length; i++)
            {
                Vector3 v = Variants[i].size;
                float d = Mathf.Abs(Mathf.Log(shape.x / shape.y) - Mathf.Log(v.x / v.y)) + Mathf.Abs(Mathf.Log(shape.z / shape.y) - Mathf.Log(v.z / v.y))
                        + 0.3f * Mathf.Abs(Mathf.Log(shape.x * shape.y * shape.z / (v.x * v.y * v.z)));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ================================================================== yasam dongusu
        void Awake()
        {
            // oyunda her koli kendi meshlerini kurar (bant her kolide ayri kesilir)
            Build(variant, Vector3.zero);
            ResetClosed();
        }

        void OnDestroy() { FreeOwned(); }

        void FreeOwned()
        {
            if (!Application.isPlaying) { owned.Clear(); return; }
            foreach (var m in owned) if (m != null) Destroy(m);
            owned.Clear();
        }

        Mesh Own(Mesh m) { if (m != null) owned.Add(m); return m; }

        public void ResetClosed()
        {
            opening = open = false; released = false;
            for (int i = 0; i < 4; i++) { angle[i] = 0f; vel[i] = 0f; flapStarted[i] = false; }
            ApplyFlaps();
            SetColliders(false);
            shownCut = 0f; RebuildTape(0f);
            if (dust != null) dust.Clear();
        }

        public void OpenInstant()
        {
            opening = false; open = true; released = true;
            angle[0] = angle[1] = longFlapAngle; angle[2] = angle[3] = shortFlapAngle;
            for (int i = 0; i < 4; i++) { vel[i] = 0f; flapStarted[i] = true; }
            ApplyFlaps();
            SetColliders(true);
            shownCut = 1f; RebuildTape(1f);
        }

        public void BeginOpen()
        {
            if (open || opening) return;
            shownCut = 1f; RebuildTape(1f);
            opening = true; openTime = Time.time;
            rng = new System.Random(Environment.TickCount ^ transform.position.GetHashCode());
            for (int i = 0; i < 4; i++) { vel[i] = 0f; flapStarted[i] = false; }
            // bant kopunca kapaklar gerilimle hafif ziplar
            vel[0] = vel[1] = 160f;
            Play(ripClip, transform.TransformPoint(new Vector3(0f, TopY, 0f)), 0.9f, R(0.95f, 1.08f));
            SeamDust(14, 0.6f);
        }

        void LateUpdate()
        {
            if (world == null) { world = WarehouseWorld.Instance; if (world == null) { Animate(); return; } }
            if (world.IsSpawned && crateIndex >= 0 && crateIndex < world.Crates.Count)
            {
                string run = world.RunId.Value.ToString();
                var state = world.Crates[crateIndex];
                if (run != knownRun)
                {
                    knownRun = run;
                    if (state.variant != builtVariant || state.shape != builtShape) Build(state.variant, state.shape);
                    ResetClosed();
                    knownOpen = state.opened;
                    if (state.opened) OpenInstant();
                    else { shownCut = state.progress; RebuildTape(shownCut); }
                }
                else if (state.opened && !knownOpen)
                {
                    knownOpen = true;
                    double age = world.NetworkManager.ServerTime.Time - state.openedAt;
                    if (age > 2.5) OpenInstant(); else BeginOpen();
                }
                else if (!state.opened)
                {
                    float server = state.progress, target = server;
                    // 20 Hz sunucu guncellemesi arasinda bicak akici ilerlesin (ama asla sunucunun cok onune gecmesin)
                    if (state.opener != ItemState.Nobody && shownCut >= server - 0.2f)
                        target = Mathf.Min(Mathf.Max(server, shownCut + Time.deltaTime / Mathf.Max(0.2f, cutSeconds)), Mathf.Min(0.995f, server + 0.04f));
                    target = Mathf.Max(target, server);
                    if (target > shownCut + 1e-4f)
                    {
                        float from = shownCut;
                        shownCut = Mathf.MoveTowards(shownCut, target, Time.deltaTime * 3f / Mathf.Max(0.2f, cutSeconds) + 0.002f);
                        CuttingFx(from, shownCut);
                    }
                    else if (target < shownCut - 0.3f) shownCut = target;   // tur sifirlandi vb.
                    if (Mathf.Abs(shownCut - builtCut) > 0.0005f) RebuildTape(shownCut);
                }
            }
            Animate();
        }

        // ================================================================== kapak animasyonu
        static readonly float[] FlapDelay = { 0.03f, 0.11f, 0.22f, 0.3f };

        void Animate()
        {
            if (!opening) return;
            float t = Time.time - openTime;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool settled = true;
            for (int i = 0; i < 4; i++)
            {
                float target = i < 2 ? longFlapAngle : shortFlapAngle;
                if (t < FlapDelay[i]) { settled = false; if (i < 2) Spring(i, 6f, dt, 300f, 18f); continue; }
                if (!flapStarted[i])
                {
                    flapStarted[i] = true;
                    vel[i] += i < 2 ? 260f : 200f;
                    Play(Pick(flapClips), FlapTip(i), i < 2 ? 0.8f : 0.55f, R(0.92f, 1.1f));
                    if (i == 0) released = true;
                    if (i == 0) SetColliders(true);
                    if (i == 1) OpenBurst();
                }
                Spring(i, target, dt, i < 2 ? 150f : 185f, i < 2 ? 11f : 13f);
                if (Mathf.Abs(angle[i] - target) > 0.4f || Mathf.Abs(vel[i]) > 4f) settled = false;
            }
            if (t > 0.7f && t < 1.9f && rng.NextDouble() < 0.25) LingerDust();
            ApplyFlaps();
            if (settled && t > 0.8f) { opening = false; open = true; released = true; }
        }

        void Spring(int i, float target, float dt, float k, float damp)
        {
            vel[i] += (k * (target - angle[i]) - damp * vel[i]) * dt;
            angle[i] += vel[i] * dt;
        }

        void ApplyFlaps()
        {
            if (flapLongA != null) flapLongA.localRotation = Quaternion.Euler(angle[0], 0f, 0f);
            if (flapLongB != null) flapLongB.localRotation = Quaternion.Euler(-angle[1], 0f, 0f);
            if (flapShortA != null) flapShortA.localRotation = Quaternion.Euler(0f, 0f, -angle[2]);
            if (flapShortB != null) flapShortB.localRotation = Quaternion.Euler(0f, 0f, angle[3]);
        }

        Vector3 FlapTip(int i)
        {
            Transform f = i == 0 ? flapLongA : i == 1 ? flapLongB : i == 2 ? flapShortA : flapShortB;
            return f != null ? f.position : transform.position + Vector3.up * TopY;
        }

        void SetColliders(bool isOpen)
        {
            if (closedColliders != null) closedColliders.SetActive(!isOpen);
            if (openColliders != null) openColliders.SetActive(isOpen);
        }

        // ================================================================== kesme
        // Kesik yolu (kolinin yerel uzayinda): on yuzde bandin ucundan yukari, ustte dikis boyunca, arkada asagi.
        Vector3 PathPoint(float s, out Vector3 normal, out int segment)
        {
            float e = 0.0006f, d1 = tapeDrop + T, d2 = length;
            if (s <= d1) { normal = Vector3.right; segment = 0; return new Vector3(length * 0.5f + e, height - tapeDrop + s, 0f); }
            s -= d1;
            if (s <= d2) { normal = Vector3.up; segment = 1; return new Vector3(length * 0.5f - s, TopY + e, 0f); }
            s -= d2;
            normal = Vector3.left; segment = 2;
            return new Vector3(-length * 0.5f - e, TopY - Mathf.Min(s, d1), 0f);
        }

        // ICrateStage: maket bicaginin ucu bu noktada, kesik yonunde ilerler (eldeki bicak animasyonu buna uzanir)
        public bool WorkPoint(out Vector3 point, out Vector3 along, out Vector3 normal)
        {
            float p = Mathf.Clamp01(shownCut < 0f ? 0f : shownCut);
            point = CutPointWorld(p, out normal);
            Vector3 n2;
            Vector3 ahead = CutPointWorld(Mathf.Min(1f, p + 0.02f), out n2);
            along = ahead - point;
            if (along.sqrMagnitude < 1e-8f) along = -transform.up;
            along.Normalize();
            return !open;
        }

        public Vector3 CutPointWorld(float progress, out Vector3 worldNormal)
        {
            Vector3 n; int seg;
            Vector3 p = PathPoint(Mathf.Clamp01(progress) * PathLength, out n, out seg);
            worldNormal = transform.TransformDirection(n);
            return transform.TransformPoint(p);
        }

        void CuttingFx(float from, float to)
        {
            if (to <= from) return;
            Vector3 n;
            Vector3 p = CutPointWorld(to, out n);
            if (from <= 0.0001f) Play(pierceClip, p, 0.75f, R(0.95f, 1.1f));
            if (Time.time >= nextCutSound)
            {
                nextCutSound = Time.time + R(0.17f, 0.26f);
                Play(Pick(cutClips), p, 0.65f, R(0.9f, 1.15f));
            }
            if (Time.time >= nextFx)
            {
                nextFx = Time.time + 0.045f;
                Vector3 along = (CutPointWorld(Mathf.Min(1f, to + 0.01f), out _) - p);
                along = along.sqrMagnitude > 1e-8f ? along.normalized : transform.up;
                for (int i = 0; i < 2; i++)
                    Dust.Puff(p + n * 0.004f, n * R(0.05f, 0.16f) + along * R(0.02f, 0.08f) + Vector3.up * R(0.02f, 0.08f) + RandomDir() * 0.03f,
                              R(0.009f, 0.018f), R(0.35f, 0.6f), R(0f, 360f));
                if (rng.NextDouble() < 0.45)
                    Dust.Chip(p + n * 0.004f, n * R(0.3f, 0.7f) + Vector3.up * R(0.4f, 1.0f) + RandomDir() * 0.2f, R(0.0025f, 0.005f), R(0.5f, 0.9f));
            }
        }

        // Bant: her parca iki yarim seritten olusur (dikisin iki yani). Kesilen kisimda yarimlar ayrilir ve kenarlari kalkar.
        void RebuildTape(float progress)
        {
            if (tapeEndsMesh == null) return;
            builtCut = progress;
            float cutS = Mathf.Clamp01(progress) * PathLength;
            var ends = new MeshBits(); var a = new MeshBits(); var b = new MeshBits();
            float d1 = tapeDrop + T, d2 = length;
            TapeSegment(ends, null, 0f, d1, cutS, Vector3.zero, Vector3.zero);
            TapeSegment(a, b, d1, d1 + d2, cutS, new Vector3(0f, height, width * 0.5f), new Vector3(0f, height, -width * 0.5f));
            TapeSegment(ends, null, d1 + d2, PathLength, cutS, Vector3.zero, Vector3.zero);
            ends.Apply(tapeEndsMesh); a.Apply(tapeAMesh); b.Apply(tapeBMesh);
        }

        // s0..s1 parcasi. halfB == null: iki yari ayni meshte (govde). Degilse ust bant: yari A kapak A'nin, yari B kapak B'nin uzayinda.
        void TapeSegment(MeshBits halfA, MeshBits halfB, float s0, float s1, float cutS, Vector3 pivotA, Vector3 pivotB)
        {
            const int Steps = 18;
            float w = tapeWidth * 0.5f;
            for (int i = 0; i < Steps; i++)
            {
                float u0 = Mathf.Lerp(s0, s1, i / (float)Steps), u1 = Mathf.Lerp(s0, s1, (i + 1) / (float)Steps);
                // kesik siniri bu adimin icindeyse tam orada bol
                if (cutS > u0 && cutS < u1) { StripPair(halfA, halfB, u0, cutS, true, pivotA, pivotB, w); StripPair(halfA, halfB, cutS, u1, false, pivotA, pivotB, w); }
                else StripPair(halfA, halfB, u0, u1, u1 <= cutS + 1e-5f, pivotA, pivotB, w);
            }
        }

        void StripPair(MeshBits halfA, MeshBits halfB, float u0, float u1, bool cut, Vector3 pivotA, Vector3 pivotB, float w)
        {
            Vector3 n0, n1; int g0, g1;
            Vector3 p0 = PathPoint(u0 + 1e-5f, out n0, out g0), p1 = PathPoint(u1 - 1e-5f, out n1, out g1);
            float gap = cut ? 0.0022f : 0f, lift = cut ? 0.0018f : 0f;
            Vector3 side = Vector3.forward;
            for (int h = 0; h < 2; h++)
            {
                float sign = h == 0 ? 1f : -1f;
                MeshBits target = h == 0 || halfB == null ? halfA : halfB;
                Vector3 pivot = h == 0 ? pivotA : pivotB;
                Vector3 inner = side * sign * gap, outer = side * sign * (w + gap * 0.3f);
                Vector3 a = p0 + inner + n0 * lift - pivot, b = p1 + inner + n1 * lift - pivot;
                Vector3 c = p1 + outer - pivot, d = p0 + outer - pivot;
                Vector3 n = Vector3.Normalize(n0 + n1);
                // doku: bandin altindaki yuzun baskisi (paketteki bant), kesilmemis yerine gore
                int face = g0 == 0 ? 0 : (g0 == 1 ? 2 : 1);
                Vector2 ta = FaceUVAt(face, p0), tb = FaceUVAt(face, p1);
                Vector2 tc = FaceUVAt(face, p1 + side * sign * w), td = FaceUVAt(face, p0 + side * sign * w);
                target.QuadAuto(a, b, c, d, n, ta, tb, tc, td, 0);
                if (cut && halfB == null)
                {
                    // uc yuzlerde kesigin altindaki karanlik yarik
                    Vector3 sa = p0 - side * sign * 0.0001f - n0 * 0.0003f, sb = p1 - side * sign * 0.0001f - n1 * 0.0003f;
                    Vector3 sc = p1 + side * sign * gap - n1 * 0.0003f, sd = p0 + side * sign * gap - n0 * 0.0003f;
                    Vector2 dk = DarkUV;
                    target.QuadAuto(sa, sb, sc, sd, n, dk, dk, dk, dk, 0);
                }
            }
        }

        // ================================================================== toz
        CrateDust Dust
        {
            get
            {
                if (dust == null) dust = CrateDust.Create(transform, dustMaterial, dustTint, fiberColor);
                return dust;
            }
        }

        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        Vector3 RandomDir() { return new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)); }

        void SeamDust(int count, float strength)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = transform.TransformPoint(new Vector3(R(-0.5f, 0.5f) * length, TopY + 0.01f, R(-0.02f, 0.02f)));
                Vector3 v = transform.up * R(0.2f, 0.5f) * strength + transform.forward * R(-0.4f, 0.4f) * strength;
                Dust.Puff(p, v, R(0.02f, 0.04f), R(0.45f, 0.8f), R(0f, 360f));
            }
        }

        void OpenBurst()
        {
            Vector3 c = transform.TransformPoint(new Vector3(0f, height * 0.75f, 0f));
            Play(dustClip, c, 0.7f, R(0.95f, 1.05f));
            for (int i = 0; i < 34; i++)
            {
                Vector3 p = transform.TransformPoint(new Vector3(R(-0.45f, 0.45f) * length, height * R(0.85f, 1.02f), R(-0.45f, 0.45f) * width));
                Vector3 outward = p - c; outward.y = 0f;
                Vector3 v = Vector3.up * R(0.5f, 1.2f) + outward.normalized * R(0.2f, 0.7f) + RandomDir() * 0.1f;
                Dust.Puff(p, v, R(0.035f, 0.075f), R(0.7f, 1.3f), R(0f, 360f));
            }
            for (int i = 0; i < 14; i++)
                Dust.Chip(c + Vector3.up * 0.1f, Vector3.up * R(1.0f, 1.8f) + RandomDir() * R(0.3f, 0.7f), R(0.003f, 0.007f), R(0.9f, 1.5f));
        }

        void LingerDust()
        {
            Vector3 p = transform.TransformPoint(new Vector3(R(-0.4f, 0.4f) * length, height * R(0.9f, 1.05f), R(-0.4f, 0.4f) * width));
            Dust.Puff(p, Vector3.up * R(0.08f, 0.2f) + RandomDir() * 0.05f, R(0.03f, 0.06f), R(1.0f, 1.6f), R(0f, 360f));
        }

        // ================================================================== ses
        AudioClip Pick(AudioClip[] list) { return list == null || list.Length == 0 ? null : list[rng.Next(list.Length)]; }

        void Play(AudioClip clip, Vector3 pos, float vol, float pitch)
        {
            if (clip == null || volume <= 0f) return;
            if (voices == null)
            {
                voices = new AudioSource[4];
                for (int i = 0; i < voices.Length; i++)
                {
                    var go = new GameObject("KoliSes" + i);
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(transform, false);
                    var a = go.AddComponent<AudioSource>();
                    a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
                    a.minDistance = 1.2f; a.maxDistance = 22f; a.dopplerLevel = 0f;
                    voices[i] = a;
                }
            }
            var v = voices[voiceIndex]; voiceIndex = (voiceIndex + 1) % voices.Length;
            v.transform.position = pos; v.clip = clip; v.volume = Mathf.Clamp01(vol) * volume; v.pitch = pitch; v.Play();
        }

        // ================================================================== insa
        // Dokunun (paketteki BaseColor, yazilar silinmis) icinde: ic yuzler icin duz bir mukavva parcasi ve koyu yarik pikseli.
        static readonly Rect InnerPatch = new Rect(0.749f, 0.428f, 0.068f, 0.076f);
        static readonly Vector2 DarkUV = new Vector2(0.95f, 0.993f);

        // yuz (0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z) uzerindeki yerel noktanin (taban merkezli) doku koordinati
        Vector2 FaceUVAt(int face, Vector3 p)
        {
            Vector3 c = new Vector3(p.x / length, (p.y - height * 0.5f) / height, p.z / width);
            float s, t;
            if (face < 2) { s = c.z; t = c.y; } else if (face < 4) { s = c.x; t = c.z; } else { s = c.x; t = c.y; }
            FaceUV f = V.faces[face];
            return f.c0 + f.c1 * s + f.c2 * t;
        }

        static readonly string[] PartNames = { "Govde", "Kapak uzun A", "Kapak uzun B", "Kapak kisa A", "Kapak kisa B", "Colliders_Closed", "Colliders_Open" };

        static Vector2 Patch(float s, float t) { return new Vector2(InnerPatch.x + InnerPatch.width * Mathf.Clamp01(s), InnerPatch.y + InnerPatch.height * Mathf.Clamp01(t)); }

        /// <summary>Kolinin tum parcalarini (govde, kapaklar, bant, dolgu, collider) secili boyda koddan uretir.</summary>
        public void Build(int v) { Build(v, Vector3.zero); }

        /// <summary>shape: dis olcu (metre); sifirsa secili paket kolisinin kendi olcusu.</summary>
        public void Build(int v, Vector3 shape)
        {
            variant = Mathf.Clamp(v, 0, Variants.Length - 1);
            builtVariant = variant;
            builtShape = shape;
            dims = shape.x > 0f && shape.y > 0f && shape.z > 0f ? shape : Vector3.zero;
            FreeOwned();
            // yalnizca kendi parcalarimizi sil (ses ve toz objelerine dokunma)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i).gameObject;
                if (Array.IndexOf(PartNames, ch.name) < 0) continue;
                if (Application.isPlaying) { ch.SetActive(false); Destroy(ch); } else DestroyImmediate(ch);
            }
            float L = length, W = width, H = height, t = Wall;
            var two = new[] { boxMaterial, insideMaterial != null ? insideMaterial : boxMaterial };

            body = Part("Govde", transform, Vector3.zero, Own(BodyMesh(L, W, H, t)), two).transform;
            Part("Kagit dolgu", body, Vector3.zero, Own(FillerMesh(L, W, t, FillerFor(H))), new[] { fillerMaterial != null ? fillerMaterial : two[1] });

            float g = 0.0015f, ds = Mathf.Min(W, L) * 0.5f * 0.92f;
            Vector3 pa = new Vector3(0f, H, W * 0.5f), pb = new Vector3(0f, H, -W * 0.5f);
            flapLongA = Part("Kapak uzun A", transform, pa, Own(FlapMesh(new Vector3(-L * 0.5f, 0f, -(W * 0.5f - g)), new Vector3(L * 0.5f, t, 0f), pa, true)), two).transform;
            flapLongB = Part("Kapak uzun B", transform, pb, Own(FlapMesh(new Vector3(-L * 0.5f, 0f, 0f), new Vector3(L * 0.5f, t, W * 0.5f - g), pb, true)), two).transform;
            Vector3 pc = new Vector3(L * 0.5f, H - t - 0.0008f, 0f), pd = new Vector3(-L * 0.5f, H - t - 0.0008f, 0f);
            flapShortA = Part("Kapak kisa A", transform, pc, Own(FlapMesh(new Vector3(-ds, 0f, -(W * 0.5f - t)), new Vector3(0f, t, W * 0.5f - t), pc, false)), two).transform;
            flapShortB = Part("Kapak kisa B", transform, pd, Own(FlapMesh(new Vector3(0f, 0f, -(W * 0.5f - t)), new Vector3(ds, t, W * 0.5f - t), pd, false)), two).transform;

            var one = new[] { boxMaterial };
            tapeEnds = Part("Bant", body, Vector3.zero, null, one);
            tapeTopA = Part("Bant", flapLongA, Vector3.zero, null, one);
            tapeTopB = Part("Bant", flapLongB, Vector3.zero, null, one);
            tapeEndsMesh = Own(new Mesh { name = "Koli bant uclar" }); tapeAMesh = Own(new Mesh { name = "Koli bant A" }); tapeBMesh = Own(new Mesh { name = "Koli bant B" });
            if (Application.isPlaying) { tapeEndsMesh.MarkDynamic(); tapeAMesh.MarkDynamic(); tapeBMesh.MarkDynamic(); }
            tapeEnds.sharedMesh = tapeEndsMesh; tapeTopA.sharedMesh = tapeAMesh; tapeTopB.sharedMesh = tapeBMesh;
            builtCut = -2f;
            RebuildTape(0f);

            // collider'lar: kapali tek kutu; acik ici bos (taban + 4 duvar)
            closedColliders = new GameObject("Colliders_Closed");
            closedColliders.transform.SetParent(transform, false);
            var bc = closedColliders.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, (H + t) * 0.5f, 0f); bc.size = new Vector3(L, H + t, W);
            openColliders = new GameObject("Colliders_Open");
            openColliders.transform.SetParent(transform, false);
            float k = 0.012f;
            AddBox(openColliders, new Vector3(0f, t * 0.5f, 0f), new Vector3(L, t + 0.004f, W));
            AddBox(openColliders, new Vector3(0f, H * 0.5f, W * 0.5f - k * 0.5f + t * 0.5f), new Vector3(L, H, k));
            AddBox(openColliders, new Vector3(0f, H * 0.5f, -W * 0.5f + k * 0.5f - t * 0.5f), new Vector3(L, H, k));
            AddBox(openColliders, new Vector3(L * 0.5f - k * 0.5f + t * 0.5f, H * 0.5f, 0f), new Vector3(k, H, W));
            AddBox(openColliders, new Vector3(-L * 0.5f + k * 0.5f - t * 0.5f, H * 0.5f, 0f), new Vector3(k, H, W));
            openColliders.SetActive(false);
            if (Application.isPlaying) { opening = open = false; for (int i = 0; i < 4; i++) { angle[i] = 0f; vel[i] = 0f; } }
        }

        MeshFilter Part(string n, Transform parent, Vector3 localPos, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            return mf;
        }

        static void AddBox(GameObject parent, Vector3 center, Vector3 size)
        {
            var c = parent.AddComponent<BoxCollider>();
            c.center = center; c.size = size;
        }

        // ---------------------------------------------------------------- meshler
        // dis yuz: koselerden doku (paketteki gibi), ic yuz: duz mukavva parcasi (koyu materyal)
        void OuterFace(MeshBits m, int face, Vector3 center, Vector3 normal, Vector3 up, float w, float h)
        {
            Vector3 right = Vector3.Cross(normal, up);
            Vector3 a = center - right * w * 0.5f - up * h * 0.5f, b = center + right * w * 0.5f - up * h * 0.5f;
            Vector3 c = center + right * w * 0.5f + up * h * 0.5f, d = center - right * w * 0.5f + up * h * 0.5f;
            m.Quad(a, b, c, d, normal, FaceUVAt(face, a), FaceUVAt(face, b), FaceUVAt(face, c), FaceUVAt(face, d), 0);
        }

        static void InnerFace(MeshBits m, Vector3 center, Vector3 normal, Vector3 up, float w, float h)
        {
            Vector3 right = Vector3.Cross(normal, up);
            Vector3 a = center - right * w * 0.5f - up * h * 0.5f, b = center + right * w * 0.5f - up * h * 0.5f;
            Vector3 c = center + right * w * 0.5f + up * h * 0.5f, d = center - right * w * 0.5f + up * h * 0.5f;
            float sw = Mathf.Clamp01(w / 0.5f), sh = Mathf.Clamp01(h / 0.5f);
            m.Quad(a, b, c, d, normal, Patch(0, 0), Patch(sw, 0), Patch(sw, sh), Patch(0, sh), 1);
        }

        Mesh BodyMesh(float L, float W, float H, float t)
        {
            var m = new MeshBits();
            OuterFace(m, 4, new Vector3(0f, H * 0.5f, W * 0.5f), Vector3.forward, Vector3.up, L, H);
            OuterFace(m, 5, new Vector3(0f, H * 0.5f, -W * 0.5f), Vector3.back, Vector3.up, L, H);
            OuterFace(m, 0, new Vector3(L * 0.5f, H * 0.5f, 0f), Vector3.right, Vector3.up, W, H);
            OuterFace(m, 1, new Vector3(-L * 0.5f, H * 0.5f, 0f), Vector3.left, Vector3.up, W, H);
            OuterFace(m, 3, new Vector3(0f, 0f, 0f), Vector3.down, Vector3.forward, L, W);
            float ih = H - t, iy = t + ih * 0.5f;
            InnerFace(m, new Vector3(0f, iy, W * 0.5f - t), Vector3.back, Vector3.up, L - 2f * t, ih);
            InnerFace(m, new Vector3(0f, iy, -W * 0.5f + t), Vector3.forward, Vector3.up, L - 2f * t, ih);
            InnerFace(m, new Vector3(L * 0.5f - t, iy, 0f), Vector3.left, Vector3.up, W - 2f * t, ih);
            InnerFace(m, new Vector3(-L * 0.5f + t, iy, 0f), Vector3.right, Vector3.up, W - 2f * t, ih);
            InnerFace(m, new Vector3(0f, t, 0f), Vector3.up, Vector3.forward, L - 2f * t, W - 2f * t);
            InnerFace(m, new Vector3(0f, H, W * 0.5f - t * 0.5f), Vector3.up, Vector3.forward, L, t);
            InnerFace(m, new Vector3(0f, H, -W * 0.5f + t * 0.5f), Vector3.up, Vector3.forward, L, t);
            InnerFace(m, new Vector3(L * 0.5f - t * 0.5f, H, 0f), Vector3.up, Vector3.right, W - 2f * t, t);
            InnerFace(m, new Vector3(-L * 0.5f + t * 0.5f, H, 0f), Vector3.up, Vector3.right, W - 2f * t, t);
            return m.ToMesh("Koli govde " + V.name);
        }

        // Kapak: kapali haldeki yerinde uretilir (pivot = mentese). Uzun kapaklarin ustu kolinin ust yuzu dokusunu tasir.
        Mesh FlapMesh(Vector3 min, Vector3 max, Vector3 pivot, bool printedTop)
        {
            var m = new MeshBits();
            Vector3 c = (min + max) * 0.5f, s = max - min;
            if (printedTop)
            {
                Vector3 top = new Vector3(c.x, max.y, c.z);
                Vector3 right = Vector3.right, fwd = Vector3.forward;
                Vector3 a = top - right * s.x * 0.5f - fwd * s.z * 0.5f, b = top + right * s.x * 0.5f - fwd * s.z * 0.5f;
                Vector3 cc = top + right * s.x * 0.5f + fwd * s.z * 0.5f, d = top - right * s.x * 0.5f + fwd * s.z * 0.5f;
                // yuz +Y: doku kapali kolinin ustundeki yerine gore (pivot eklenerek kolinin uzayina)
                m.QuadAuto(a, b, cc, d, Vector3.up, FaceUVAt(2, a + pivot), FaceUVAt(2, b + pivot), FaceUVAt(2, cc + pivot), FaceUVAt(2, d + pivot), 0);
            }
            else InnerFace(m, new Vector3(c.x, max.y, c.z), Vector3.up, Vector3.forward, s.x, s.z);
            InnerFace(m, new Vector3(c.x, min.y, c.z), Vector3.down, Vector3.forward, s.x, s.z);
            InnerFace(m, new Vector3(c.x, c.y, max.z), Vector3.forward, Vector3.up, s.x, s.y);
            InnerFace(m, new Vector3(c.x, c.y, min.z), Vector3.back, Vector3.up, s.x, s.y);
            InnerFace(m, new Vector3(max.x, c.y, c.z), Vector3.right, Vector3.up, s.z, s.y);
            InnerFace(m, new Vector3(min.x, c.y, c.z), Vector3.left, Vector3.up, s.z, s.y);
            return m.ToMesh("Koli kapak");
        }

        // Kirpik kagit dolgu: tabanda rastgele kivrik seritler (cift yuzlu, kendi materyali).
        static Mesh FillerMesh(float L, float W, float t, float filler)
        {
            var m = new MeshBits();
            var r = new System.Random(1234);
            Func<float, float, float> Rnd = (a, b) => a + (float)r.NextDouble() * (b - a);
            int count = Mathf.RoundToInt(170f * (L * W) / (0.56f * 0.42f));
            for (int i = 0; i < Mathf.Clamp(count, 40, 260); i++)
            {
                Vector3 c = new Vector3(Rnd(-0.5f, 0.5f) * (L - 2f * t - 0.03f), t + Rnd(0.004f, filler), Rnd(-0.5f, 0.5f) * (W - 2f * t - 0.03f));
                float yaw = Rnd(0f, 360f), len = Rnd(0.04f, 0.085f), wid = Rnd(0.005f, 0.009f), bend = Rnd(-35f, 35f), tilt = Rnd(-25f, 25f);
                Quaternion q = Quaternion.Euler(tilt, yaw, Rnd(-60f, 60f));
                Vector3 dir = q * Vector3.forward, side = q * Vector3.right, up = q * Vector3.up;
                Vector3 mid = c, end1 = c - dir * len * 0.5f, end2 = c + Quaternion.AngleAxis(bend, side) * dir * len * 0.5f;
                Vector2 uv = new Vector2(Rnd(0.1f, 0.9f), Rnd(0.1f, 0.9f));
                Vector3 hw = side * wid * 0.5f;
                m.QuadAuto(end1 - hw, mid - hw, mid + hw, end1 + hw, up, uv, uv, uv, uv, 0);
                m.QuadAuto(end1 + hw, mid + hw, mid - hw, end1 - hw, -up, uv, uv, uv, uv, 0);
                m.QuadAuto(mid - hw, end2 - hw, end2 + hw, mid + hw, up, uv, uv, uv, uv, 0);
                m.QuadAuto(mid + hw, end2 + hw, end2 - hw, mid - hw, -up, uv, uv, uv, uv, 0);
            }
            return m.ToMesh("Koli dolgu");
        }

        // Kucuk mesh yardimcisi: dortgenler, 2 alt mesh (0 dis, 1 ic) + kontur icin yumusatilmis normaller (UV3, CelLit kullanir).
        public sealed class MeshBits
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] tri = { new List<int>(), new List<int>() };

            // a,b,c,d disaridan bakinca: sol-alt, sag-alt, sag-ust, sol-ust
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td, int sub)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(ta); uv.Add(tb); uv.Add(tc); uv.Add(td);
                var t = tri[sub];
                t.Add(i); t.Add(i + 3); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 1);
            }

            // sira ne olursa olsun normal tarafindan gorunsun
            public void QuadAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td, int sub)
            {
                Vector3 face = Vector3.Cross(d - a, b - a);
                if (Vector3.Dot(face, normal) >= 0f) Quad(a, b, c, d, normal, ta, tb, tc, td, sub);
                else Quad(b, a, d, c, normal, tb, ta, td, tc, sub);
            }

            public void Apply(Mesh m)
            {
                m.Clear();
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
                int subs = tri[1].Count > 0 ? 2 : 1;
                m.subMeshCount = subs;
                for (int s = 0; s < subs; s++) m.SetTriangles(tri[s], s);
                m.SetUVs(3, Smooth());
                m.RecalculateBounds();
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh(); m.name = name;
                Apply(m);
                return m;
            }

            List<Vector3> Smooth()
            {
                var groups = new Dictionary<Vector3Int, Vector3>();
                var keys = new Vector3Int[v.Count];
                for (int i = 0; i < v.Count; i++)
                {
                    var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 5000f), Mathf.RoundToInt(v[i].y * 5000f), Mathf.RoundToInt(v[i].z * 5000f));
                    keys[i] = k;
                    Vector3 s; groups.TryGetValue(k, out s); groups[k] = s + n[i];
                }
                var list = new List<Vector3>(v.Count);
                for (int i = 0; i < v.Count; i++) { Vector3 s = groups[keys[i]]; list.Add(s.sqrMagnitude > 1e-8f ? s.normalized : n[i]); }
                return list;
            }
        }
    }
}
