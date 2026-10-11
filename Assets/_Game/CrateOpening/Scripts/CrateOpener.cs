// =====================================================================================
//  CrateOpener  -  Bidwars kasa acma animasyonu (vidalar cirt cirt sokulur, kapak
//  kanirtilir ve yana devrilir, kutudan cel-shaded toz cikar, kutu acik kalir).
//
//  Kullanim:
//      crate.Open();                 // animasyonlu ac
//      crate.OpenInstant();          // animasyonsuz, direkt acik hale getir (kayit yukleme vb.)
//      crate.ResetClosed();          // kapali hale geri al (test icin)
//      crate.onOpened / crate.Opened // acilis bitince
//      crate.onLidOff                // kapak firladigi an (esya cikarmaya baslamak icin ideal)
//
//  Oyun (adim adim) modu:
//      crate.SetSteppedWork(w, false); // w = sokulmus vida sayisi (kesirli). Vidalar SIRAYLA tek tek cikar,
//                                      // w artmazsa yarida kalan vida oldugu yerde bekler.
//      crate.BeginLid(false);          // tum vidalar cikinca kapagi kanirtip devir
//
//  Prefab'lar Tools > Bidwars > Crate Opening menusu ile otomatik olusturulur.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Bidwars.Crates
{
    public enum CrateCueType
    {
        ScrewStroke, ScrewPop, ScrewBounce, LidJerk, LidBreak, OpenBurst, LidEdgeHit, LidSlam, LidWallHit
    }

    [Serializable]
    public struct CrateCue
    {
        public float time;
        public CrateCueType type;
        public int index;
        public float strength;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Bidwars/Crate Opener")]
    public class CrateOpener : MonoBehaviour
    {
        // ------------------------------------------------------------------ animasyon
        [Header("Animasyon")]
        [Tooltip("Modelin (FBX) uzerindeki Legacy Animation bileseni. Bos ise otomatik bulunur.")]
        public Animation anim;
        public string clipName = "Open";
        [Range(0.25f, 3f)] public float speed = 1f;
        [Tooltip("Sahne baslar baslamaz ac (test icin).")]
        public bool openOnStart;

        // ------------------------------------------------------------------ parcalar
        [Header("Parcalar (kurulum otomatik doldurur)")]
        public Transform lid;
        public Transform body;
        public Transform[] screws = new Transform[4];
        public Transform fxGapA0, fxGapA1, fxOpening0, fxOpening1, fxFloor;
        public Transform[] fxScrews = new Transform[4];
        [Tooltip("Kapali kasa icin tek kutu collider")] public GameObject closedColliders;
        [Tooltip("Acik kasa icin ici bos (zemin + 4 duvar) colliderlar")] public GameObject openColliders;

        // ------------------------------------------------------------------ zamanlama
        [Header("Zamanlama (Blender'dan gelir, elle degistirme)")]
        public CrateCue[] cues = new CrateCue[0];

        // ------------------------------------------------------------------ toz
        [Header("Toz / Kiymik (cel-shaded)")]
        public Material dustMaterial;
        public Color dustTint = new Color(1f, 0.97f, 0.92f, 1f);
        public Color chipColor = new Color(0.56f, 0.41f, 0.27f, 1f);
        [Range(0f, 3f)] public float dustAmount = 1f;
        [Range(0.3f, 3f)] public float dustSize = 1f;
        public bool woodChips = true;

        // ------------------------------------------------------------------ ses
        [Header("Ses")]
        public AudioClip[] ratchetClips = new AudioClip[0];
        public AudioClip[] popClips = new AudioClip[0];
        public AudioClip[] tinkClips = new AudioClip[0];
        public AudioClip[] creakClips = new AudioClip[0];
        public AudioClip lidBreakClip, lidSlamClip, lidKnockClip, dustClip;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        public float hearingDistance = 30f;
        public UnityEngine.Audio.AudioMixerGroup mixerGroup;

        // ------------------------------------------------------------------ olaylar
        [Header("Olaylar")]
        public UnityEvent onOpenStarted = new UnityEvent();
        public UnityEvent onLidOff = new UnityEvent();
        public UnityEvent onOpened = new UnityEvent();
        public event Action<CrateOpener> Opened;

        public bool IsOpen { get; private set; }
        public bool IsOpening { get; private set; }
        public float Progress { get { if (IsOpen) return 1f; return IsOpening && state != null && state.length > 0f ? Mathf.Clamp01(state.time / state.length) : 0f; } }

        AnimationState state;
        int nextCue;
        bool initialized;
        bool lidOffFired;
        CrateDust dust;
        readonly List<AudioSource> voices = new List<AudioSource>();
        int voiceIndex;
        System.Random rng = new System.Random(12345);
        readonly List<PendingBurst> bursts = new List<PendingBurst>();

        struct PendingBurst { public float at; public int count; public bool linger; }

        // =================================================================== yasam dongusu
        void Awake() { Init(); }

        // =================================================================== adim adim acilis (oyun)
        // Klipte vidalar ust uste biner (biri bitmeden digeri baslar). Oyunda her vida kendi zamaniyla oynatilir:
        // vida i, klibin [baslangic_i .. firlama_i] araligini oyuncunun E'ye basili tuttugu kadar ilerletir, sonra
        // yere dusup sekmesi (firlama_i .. bitis_i) gercek zamanda oynar. Kapak ve govde ayri bir zamanla (lidT) oynar.
        bool stepped;
        float[] screwStart, screwPop, screwEnd, screwT;
        int[] screwOrder;
        float lidStart, lidT = -1f, stepWork;
        bool posePending;
        Vector3[] cachePos; Quaternion[] cacheRot; Vector3[] cacheScale;

        // Kasa esyaya gore yeniden boyutlaninca vidalar ayni boyda kalsin (CrateStage ayarlar).
        float screwScale = 1f;
        Vector3[] screwBaseScale;

        public void SetScrewScale(float k)
        {
            Init();
            screwScale = Mathf.Clamp(k, 0.3f, 3f);
            ApplyScrewScale();
        }

        void ApplyScrewScale()
        {
            if (screws == null) return;
            if (screwBaseScale == null || screwBaseScale.Length != screws.Length)
            {
                screwBaseScale = new Vector3[screws.Length];
                for (int i = 0; i < screws.Length; i++) screwBaseScale[i] = screws[i] != null ? screws[i].localScale : Vector3.one;
            }
            if (Mathf.Approximately(screwScale, 1f) && screwBaseScale != null)
            {
                for (int i = 0; i < screws.Length; i++) if (screws[i] != null) screws[i].localScale = screwBaseScale[i];
                return;
            }
            for (int i = 0; i < screws.Length; i++) if (screws[i] != null) screws[i].localScale = screwBaseScale[i] * screwScale;
        }

        static bool IsScrewCue(CrateCueType t) { return t == CrateCueType.ScrewStroke || t == CrateCueType.ScrewPop || t == CrateCueType.ScrewBounce; }

        /// <summary>Sirayla sokulecek vida sayisi.</summary>
        public int ScrewCount { get { Init(); BuildSteps(); return screwOrder.Length; } }

        /// <summary>Oyun modu: su an sokulen vidanin dunya konumu (work = kesirli vida sayisi). Vida yoksa false.</summary>
        public bool CurrentScrew(float work, out Vector3 position)
        {
            position = transform.position;
            Init(); BuildSteps();
            if (screwOrder == null || screwOrder.Length == 0) return false;
            int k = Mathf.Clamp(Mathf.FloorToInt(work), 0, screwOrder.Length - 1);
            var s = screws[screwOrder[k]];
            if (s == null) return false;
            position = s.position;
            return true;
        }

        void BuildSteps()
        {
            if (screwOrder != null) return;
            int n = screws != null ? screws.Length : 0;
            screwStart = new float[n]; screwPop = new float[n]; screwEnd = new float[n]; screwT = new float[n];
            cachePos = new Vector3[n]; cacheRot = new Quaternion[n]; cacheScale = new Vector3[n];
            var has = new bool[n];
            for (int i = 0; i < n; i++) { screwStart[i] = float.MaxValue; screwPop[i] = -1f; screwEnd[i] = -1f; }
            float firstLid = float.MaxValue;
            foreach (CrateCue c in cues)
            {
                if (!IsScrewCue(c.type)) { firstLid = Mathf.Min(firstLid, c.time); continue; }
                int i = c.index;
                if (i < 0 || i >= n) continue;
                if (c.type == CrateCueType.ScrewStroke) { has[i] = true; screwStart[i] = Mathf.Min(screwStart[i], c.time); }
                else if (c.type == CrateCueType.ScrewPop) { has[i] = true; screwPop[i] = Mathf.Max(screwPop[i], c.time); }
                screwEnd[i] = Mathf.Max(screwEnd[i], c.time);
            }
            float length = state != null ? state.length : 5f;
            var order = new List<int>();
            float lastPop = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!has[i] || screws[i] == null) continue;
                if (screwStart[i] == float.MaxValue) screwStart[i] = Mathf.Max(0f, screwPop[i] - 0.6f);
                screwStart[i] = Mathf.Max(0f, screwStart[i] - 0.12f);
                if (screwPop[i] < screwStart[i] + 0.05f) screwPop[i] = Mathf.Max(screwEnd[i], screwStart[i] + 0.5f);
                screwEnd[i] = Mathf.Min(length, Mathf.Max(screwEnd[i] + 0.35f, screwPop[i] + 0.6f));
                lastPop = Mathf.Max(lastPop, screwPop[i]);
                order.Add(i);
            }
            order.Sort((a, b) => screwPop[a].CompareTo(screwPop[b]));
            screwOrder = order.ToArray();
            lidStart = Mathf.Min(lastPop + 0.02f, firstLid < float.MaxValue ? Mathf.Max(0f, firstLid - 0.05f) : lastPop + 0.02f);
        }

        /// <summary>Oyun modu: work = sokulmus vida sayisi (0..ScrewCount, kesirli). instant: animasyonsuz o hale gel.</summary>
        public void SetSteppedWork(float work, bool instant)
        {
            Init();
            if (state == null || IsOpen) return;
            BuildSteps();
            stepped = true;
            stepWork = Mathf.Clamp(work, 0f, screwOrder.Length);
            if (instant)
            {
                for (int k = 0; k < screwOrder.Length; k++) { int i = screwOrder[k]; screwT[i] = Mathf.Max(screwT[i], ScrewTarget(k, i)); }
                posePending = true;
            }
        }

        /// <summary>Oyun modu: tum vidalar cikti, kapagi kanirtip devir (instant: direkt acik hale gel).</summary>
        public void BeginLid(bool instant)
        {
            Init();
            if (state == null || IsOpen) return;
            BuildSteps();
            if (instant) { OpenInstant(); return; }
            if (stepped && lidT >= 0f) return;
            stepped = true;
            stepWork = screwOrder.Length;
            lidT = lidStart;
            IsOpening = true;
            lidOffFired = false;
            rng = new System.Random(Environment.TickCount ^ transform.position.GetHashCode());
            anim.Stop();
            onOpenStarted.Invoke();
        }

        float ScrewTarget(int k, int i)
        {
            if (stepWork >= k + 1) return screwEnd[i];
            if (stepWork > k) return Mathf.Lerp(screwStart[i], screwPop[i], stepWork - k);
            return 0f;
        }

        void StepUpdate()
        {
            float dt = Time.deltaTime;
            bool changed = posePending; posePending = false;
            for (int k = 0; k < screwOrder.Length; k++)
            {
                int i = screwOrder[k];
                float target = ScrewTarget(k, i);
                if (target <= 0f) continue;
                // sira bu vidaya geldi: hareketsiz bekledigi bas kismi atla
                if (screwT[i] < screwStart[i]) { screwT[i] = screwStart[i]; changed = true; }
                if (screwT[i] < target)
                {
                    // sokulurken oyuncuya yetis (hizli), firladiktan sonra gercek zamanda dus/sek
                    float rate = screwT[i] >= screwPop[i] ? 1f : 4f;
                    float nt = Mathf.Min(target, screwT[i] + dt * rate);
                    FireRange(i, screwT[i], nt);
                    screwT[i] = nt;
                    changed = true;
                }
            }
            if (lidT >= 0f && IsOpening)
            {
                float nt = Mathf.Min(state.length, lidT + dt * speed);
                FireRange(-1, lidT, nt);
                lidT = nt;
                changed = true;
                if (lidT >= state.length)
                {
                    IsOpening = false;
                    IsOpen = true;
                    SetColliders(true);
                    if (!lidOffFired) { lidOffFired = true; onLidOff.Invoke(); }
                    onOpened.Invoke();
                    if (Opened != null) Opened(this);
                }
            }
            if (changed) PoseStepped();
        }

        // screw >= 0: o vidanin olaylari; -1: vida disi olaylar (kapak, toz)
        void FireRange(int screw, float from, float to)
        {
            for (int c = 0; c < cues.Length; c++)
            {
                CrateCue cue = cues[c];
                if (cue.time <= from || cue.time > to) continue;
                bool isScrew = IsScrewCue(cue.type);
                if (screw >= 0 ? (isScrew && cue.index == screw) : !isScrew) Fire(cue);
            }
        }

        void PoseStepped()
        {
            for (int k = 0; k < screwOrder.Length; k++)
            {
                int i = screwOrder[k];
                SampleAt(screwT[i]);
                Transform sc = screws[i];
                cachePos[i] = sc.localPosition; cacheRot[i] = sc.localRotation; cacheScale[i] = sc.localScale;
            }
            SampleAt(lidT >= 0f ? lidT : lidStart);
            for (int k = 0; k < screwOrder.Length; k++)
            {
                int i = screwOrder[k];
                Transform sc = screws[i];
                sc.localPosition = cachePos[i]; sc.localRotation = cacheRot[i];
            }
        }

        void Start()
        {
            // parcacik sistemlerini onceden kur (acilis aninda takilma olmasin)
            if (Application.isPlaying && dust == null) dust = CrateDust.Create(transform, dustMaterial, dustTint, chipColor);
            if (openOnStart) Open();
        }

        bool finishOnEnable;

        void OnDisable()
        {
            // yari kalan acilis: obje kapanirsa tekrar acildiginda kasa acik halde olsun
            if (IsOpening) { IsOpening = false; finishOnEnable = true; }
        }

        void OnEnable()
        {
            if (finishOnEnable && state != null)
            {
                finishOnEnable = false;
                FinishInstant();
                if (!lidOffFired) { lidOffFired = true; onLidOff.Invoke(); }
                onOpened.Invoke();
                if (Opened != null) Opened(this);
            }
        }

        public void Init()
        {
            if (initialized) return;
            initialized = true;
            if (anim == null) anim = GetComponentInChildren<Animation>(true);
            AutoFindParts();
            if (anim != null)
            {
                anim.playAutomatically = false;
                state = GetState();
                if (state != null)
                {
                    state.wrapMode = WrapMode.ClampForever;
                    SampleAt(0f);
                }
            }
            if (state == null)
                Debug.LogWarning("[CrateOpener] Animasyon klibi bulunamadi: " + name + " (Tools > Bidwars > Crate Opening > Kurulumu Yeniden Yap)", this);
            SetColliders(false);
        }

        AnimationState GetState()
        {
            if (anim == null) return null;
            AnimationState s = null;
            if (!string.IsNullOrEmpty(clipName)) s = anim[clipName];
            if (s == null)
            {
                foreach (AnimationState st in anim) { s = st; break; }
            }
            return s;
        }

        public void AutoFindParts()
        {
            Transform root = anim != null ? anim.transform : transform;
            if (lid == null) lid = FindDeep(root, "Lid");
            if (body == null) body = FindDeep(root, "Body");
            if (screws == null || screws.Length != 4) screws = new Transform[4];
            if (fxScrews == null || fxScrews.Length != 4) fxScrews = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                if (screws[i] == null) screws[i] = FindDeep(root, "Screw_" + i);
                if (fxScrews[i] == null) fxScrews[i] = FindDeep(root, "FX_Screw_" + i);
            }
            if (fxGapA0 == null) fxGapA0 = FindDeep(root, "FX_GapA_0");
            if (fxGapA1 == null) fxGapA1 = FindDeep(root, "FX_GapA_1");
            if (fxOpening0 == null) fxOpening0 = FindDeep(root, "FX_Opening_0");
            if (fxOpening1 == null) fxOpening1 = FindDeep(root, "FX_Opening_1");
            if (fxFloor == null) fxFloor = FindDeep(root, "FX_Floor");
        }

        public static Transform FindDeep(Transform t, string n)
        {
            if (t == null) return null;
            if (t.name == n) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform r = FindDeep(t.GetChild(i), n);
                if (r != null) return r;
            }
            return null;
        }

        // =================================================================== API
        /// <summary>Animasyonlu acilis. Zaten acik/aciliyorsa bir sey yapmaz.</summary>
        public void Open()
        {
            Init();
            if (IsOpen || IsOpening || state == null) return;
            IsOpening = true;
            lidOffFired = false;
            nextCue = 0;
            bursts.Clear();
            rng = new System.Random(Environment.TickCount ^ transform.position.GetHashCode());
            anim.Stop();
            state.wrapMode = WrapMode.ClampForever;
            state.time = 0f;
            state.speed = speed;
            anim.Play(state.name);
            onOpenStarted.Invoke();
        }

        /// <summary>Animasyonsuz acik hale getirir (efekt/ses yok).</summary>
        public void OpenInstant()
        {
            Init();
            if (state == null || IsOpen) return;
            bool wasOpening = IsOpening;
            bursts.Clear();
            FinishInstant();
            if (wasOpening)
            {
                // yarida kesilen acilis: bekleyen olaylari kacirma
                if (!lidOffFired) { lidOffFired = true; onLidOff.Invoke(); }
                onOpened.Invoke();
                if (Opened != null) Opened(this);
            }
        }

        /// <summary>Kapali hale geri alir.</summary>
        public void ResetClosed()
        {
            Init();
            if (state == null) return;
            IsOpening = false; IsOpen = false;
            bursts.Clear();
            anim.Stop();
            stepped = false; lidT = -1f; stepWork = 0f; posePending = false;
            if (screwT != null) for (int i = 0; i < screwT.Length; i++) screwT[i] = 0f;
            SampleAt(0f);
            if (dust != null) dust.Clear();
            SetColliders(false);
        }

        void FinishInstant()
        {
            IsOpening = false;
            stepped = false;
            anim.Stop();
            state.wrapMode = WrapMode.ClampForever;
            anim.Play(state.name);
            state.time = state.length;
            state.speed = 0f;
            anim.Sample();
            anim.Stop();   // poz kalir (Stop yeniden ornekleme yapmaz)
            ApplyScrewScale();
            IsOpen = true;
            SetColliders(true);
        }

        void SampleAt(float t)
        {
            state.enabled = true;
            state.weight = 1f;
            state.time = t;
            anim.Sample();
            state.enabled = false;
            ApplyScrewScale();
        }

        void SetColliders(bool open)
        {
            if (closedColliders != null) closedColliders.SetActive(!open);
            if (openColliders != null) openColliders.SetActive(open);
        }

        // =================================================================== oynatma
        // Legacy Animation, Update'den sonra uygulanir; konumlar LateUpdate'te dogru olur.
        void LateUpdate()
        {
            if (bursts.Count > 0) RunBursts();
            if (stepped && state != null && !IsOpen) { StepUpdate(); return; }
            if (!IsOpening || state == null) return;
            state.speed = speed;
            float t = state.time;
            while (nextCue < cues.Length && cues[nextCue].time <= t)
            {
                Fire(cues[nextCue]);
                nextCue++;
            }
            if (t >= state.length)
            {
                while (nextCue < cues.Length) { Fire(cues[nextCue]); nextCue++; }
                IsOpening = false;
                IsOpen = true;
                anim.Stop();   // son poz kalir, acik kasa her karede yeniden hesaplanmasin
                onOpened.Invoke();
                if (Opened != null) Opened(this);
            }
        }

        // =================================================================== olay -> efekt + ses
        float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        Vector3 Up { get { return transform.up; } }

        float Scale { get { Vector3 s = transform.lossyScale; return (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f; } }

        float GroundY(Vector3 p)
        {
            // kasanin tabani (prefab koku taban ortasinda)
            return Vector3.Dot(transform.position, Up);
        }

        Vector3 OnGround(Vector3 p, float lift)
        {
            float d = Vector3.Dot(p, Up) - GroundY(p);
            return p - Up * d + Up * lift;
        }

        Vector3 CrateCenter
        {
            get
            {
                if (fxFloor != null) return fxFloor.position;
                return transform.position;
            }
        }

        Vector3 Horizontal(Vector3 v) { return v - Up * Vector3.Dot(v, Up); }

        Vector3 RandomHorizontal()
        {
            Vector3 v = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f));
            v = Quaternion.FromToRotation(Vector3.up, Up) * v;
            return v.sqrMagnitude > 1e-4f ? v.normalized : transform.right;
        }

        CrateDust Dust
        {
            get
            {
                if (dust == null)
                {
                    dust = CrateDust.Create(transform, dustMaterial, dustTint, chipColor);
                }
                return dust;
            }
        }

        void Fire(CrateCue c)
        {
            float s = Mathf.Max(0.05f, c.strength);
            float k = Scale * dustSize;
            float amount = dustAmount;
            switch (c.type)
            {
                case CrateCueType.ScrewStroke:
                {
                    Vector3 p = ScrewHeadPos(c.index);
                    Play(Pick(ratchetClips), p, 0.75f + 0.25f * s, R(0.94f, 1.08f));
                    int n = Mathf.RoundToInt(R(2f, 4f) * amount);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 v = RandomHorizontal() * R(0.05f, 0.16f) + Up * R(0.06f, 0.16f);
                        Dust.Puff(p + RandomHorizontal() * 0.008f * k, v * k, R(0.018f, 0.032f) * k, R(0.35f, 0.6f), R(0f, 360f));
                    }
                    if (woodChips && rng.NextDouble() < 0.6 * amount)
                        Dust.Chip(p, (RandomHorizontal() * R(0.2f, 0.5f) + Up * R(0.6f, 1.1f)) * k, R(0.006f, 0.011f) * k, R(0.6f, 1.1f));
                    break;
                }
                case CrateCueType.ScrewPop:
                {
                    Vector3 p = ScrewBasePos(c.index);
                    Play(Pick(popClips), p, 0.9f, R(0.95f, 1.06f));
                    int n = Mathf.RoundToInt(7f * amount);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 d = RandomHorizontal();
                        Dust.Puff(p + d * 0.01f * k + Up * 0.004f * k, (d * R(0.25f, 0.5f) + Up * R(0.1f, 0.35f)) * k, R(0.025f, 0.045f) * k, R(0.45f, 0.8f), R(0f, 360f));
                    }
                    if (woodChips)
                        for (int i = 0; i < Mathf.RoundToInt(3f * amount); i++)
                            Dust.Chip(p, (RandomHorizontal() * R(0.3f, 0.8f) + Up * R(0.8f, 1.6f)) * k, R(0.007f, 0.013f) * k, R(0.8f, 1.3f));
                    break;
                }
                case CrateCueType.ScrewBounce:
                {
                    Transform sc = Screw(c.index);
                    Vector3 p = sc != null ? sc.position : transform.position;
                    Play(Pick(tinkClips), p, 0.25f + 0.7f * s, R(0.92f, 1.1f));
                    if (s > 0.3f)
                    {
                        Vector3 g = OnGround(p, 0.004f * k);
                        for (int i = 0; i < Mathf.RoundToInt(3f * amount * s); i++)
                        {
                            Vector3 d = RandomHorizontal();
                            Dust.Puff(g + d * 0.01f * k, (d * R(0.12f, 0.3f) + Up * R(0.03f, 0.1f)) * k, R(0.018f, 0.03f) * k, R(0.4f, 0.65f), R(0f, 360f));
                        }
                    }
                    break;
                }
                case CrateCueType.LidJerk:
                {
                    Vector3 a = fxGapA0 != null ? fxGapA0.position : transform.position;
                    Vector3 b = fxGapA1 != null ? fxGapA1.position : transform.position;
                    Play(Pick(creakClips), (a + b) * 0.5f, 0.55f + 0.45f * s, R(0.9f, 1.1f));
                    GapDust(a, b, s, k, amount);
                    break;
                }
                case CrateCueType.LidBreak:
                {
                    Vector3 a = fxGapA0 != null ? fxGapA0.position : transform.position;
                    Vector3 b = fxGapA1 != null ? fxGapA1.position : transform.position;
                    Play(lidBreakClip, (a + b) * 0.5f, 1f, R(0.95f, 1.05f));
                    GapDust(a, b, 1.3f, k, amount);
                    SetColliders(true);
                    lidOffFired = true;
                    onLidOff.Invoke();
                    break;
                }
                case CrateCueType.OpenBurst:
                {
                    Play(dustClip, CrateCenter + Up * 0.3f * k, 0.8f, R(0.95f, 1.05f));
                    float now = Time.time;
                    int total = Mathf.RoundToInt(52f * amount);
                    // ana patlama birkac kareye yayilir, sonra 1.4 sn hafif toz tutsun
                    bursts.Add(new PendingBurst { at = now, count = total / 2 });
                    bursts.Add(new PendingBurst { at = now + 0.08f, count = total / 4 });
                    bursts.Add(new PendingBurst { at = now + 0.18f, count = total / 4 });
                    for (int i = 0; i < 9; i++)
                        bursts.Add(new PendingBurst { at = now + 0.35f + i * 0.13f, count = Mathf.Max(1, Mathf.RoundToInt(2f * amount)), linger = true });
                    break;
                }
                case CrateCueType.LidEdgeHit:
                {
                    Vector3 p = lid != null ? lid.position : transform.position;
                    Play(lidKnockClip, p, 0.6f + 0.4f * s, R(0.95f, 1.05f));
                    LidRing(s * 0.6f, k, amount);
                    break;
                }
                case CrateCueType.LidSlam:
                {
                    Vector3 p = lid != null ? lid.position : transform.position;
                    Play(lidSlamClip, p, 0.7f + 0.3f * s, R(0.96f, 1.04f));
                    LidRing(Mathf.Max(0.7f, s), k, amount);
                    break;
                }
                case CrateCueType.LidWallHit:
                {
                    Vector3 p = lid != null ? lid.position : transform.position;
                    Play(lidKnockClip, p, 0.4f + 0.4f * s, R(1.05f, 1.15f));
                    break;
                }
            }
        }

        Transform Screw(int i) { return screws != null && i >= 0 && i < screws.Length ? screws[i] : null; }

        Vector3 ScrewHeadPos(int i)
        {
            Transform sc = Screw(i);
            if (sc != null) return sc.position;
            return fxScrews != null && i < fxScrews.Length && fxScrews[i] != null ? fxScrews[i].position : transform.position;
        }

        Vector3 ScrewBasePos(int i)
        {
            if (fxScrews != null && i >= 0 && i < fxScrews.Length && fxScrews[i] != null) return fxScrews[i].position;
            return ScrewHeadPos(i);
        }

        void GapDust(Vector3 a, Vector3 b, float s, float k, float amount)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 outward = Horizontal(mid - CrateCenter);
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : -transform.right;
            int n = Mathf.RoundToInt((5f + 9f * s) * amount);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, R(0f, 1f)) + Up * 0.006f * k;
                Vector3 v = (outward * R(0.18f, 0.45f) * s + Up * R(0.05f, 0.25f) + RandomHorizontal() * 0.05f) * k;
                Dust.Puff(p, v, R(0.03f, 0.05f + 0.025f * s) * k, R(0.5f, 0.85f), R(0f, 360f));
            }
            if (woodChips)
                for (int i = 0; i < Mathf.RoundToInt(2f * s * amount); i++)
                    Dust.Chip(Vector3.Lerp(a, b, R(0f, 1f)), (outward * R(0.3f, 0.7f) + Up * R(0.6f, 1.3f)) * k, R(0.008f, 0.016f) * k, R(0.8f, 1.4f));
        }

        void RunBursts()
        {
            float now = Time.time;
            for (int bi = bursts.Count - 1; bi >= 0; bi--)
            {
                PendingBurst b = bursts[bi];
                if (now < b.at) continue;
                bursts.RemoveAt(bi);
                OpeningPuffs(b.count, b.linger);
            }
        }

        /// <summary>Acilan yuzun disari bakan normali (ust kapak -> yukari, yan panel -> yana).</summary>
        public Vector3 OpeningNormal
        {
            get
            {
                if (fxOpening0 == null || fxOpening1 == null || fxFloor == null) return Up;
                Vector3 n = (fxOpening0.position + fxOpening1.position) * 0.5f - fxFloor.position;
                return n.sqrMagnitude > 1e-8f ? n.normalized : Up;
            }
        }

        void OpeningPuffs(int count, bool linger)
        {
            if (fxOpening0 == null || fxOpening1 == null) return;
            Transform space = fxOpening0.parent != null ? fxOpening0.parent : transform;
            Vector3 l0 = space.InverseTransformPoint(fxOpening0.position);
            Vector3 l1 = space.InverseTransformPoint(fxOpening1.position);
            Vector3 c = (fxOpening0.position + fxOpening1.position) * 0.5f;
            Vector3 n = OpeningNormal;
            bool sideOpening = Vector3.Dot(n, Up) < 0.7f;
            float k;
            if (!sideOpening)
            {
                float width = Vector3.Distance(fxOpening0.position, fxOpening1.position);
                k = dustSize * width / 0.38f;   // ~1 for these crates, scales with opening
            }
            else
            {
                // yan acilis: dikdortgenin kenar uzunluklari (normal disindaki iki eksen)
                Vector3 d = fxOpening1.position - fxOpening0.position;
                d -= n * Vector3.Dot(d, n);
                float a = Mathf.Abs(Vector3.Dot(d, Up));
                float b = Horizontal(d).magnitude;
                k = dustSize * Mathf.Sqrt(Mathf.Max(a * b, 1e-4f)) / 0.36f;
                count = Mathf.RoundToInt(count * 1.3f);
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 lp = new Vector3(Mathf.Lerp(l0.x, l1.x, R(0.05f, 0.95f)), Mathf.Lerp(l0.y, l1.y, R(0.05f, 0.95f)), Mathf.Lerp(l0.z, l1.z, R(0.05f, 0.95f)));
                Vector3 p = space.TransformPoint(lp);
                if (!sideOpening)
                {
                    Vector3 outward = Horizontal(p - c);
                    if (linger)
                    {
                        Vector3 v = (Up * R(0.12f, 0.3f) + outward * R(0.2f, 0.6f)) * k;
                        Dust.Puff(p, v, R(0.05f, 0.09f) * k, R(1.0f, 1.6f), R(0f, 360f));
                    }
                    else
                    {
                        Vector3 v = (Up * R(0.7f, 1.6f) + outward * R(0.6f, 1.6f) + RandomHorizontal() * 0.1f) * k;
                        Dust.Puff(p - Up * R(0f, 0.05f) * k, v, R(0.045f, 0.11f) * k, R(0.8f, 1.5f), R(0f, 360f));
                    }
                }
                else
                {
                    Vector3 spread = p - c; spread -= n * Vector3.Dot(spread, n);
                    if (linger)
                    {
                        Vector3 v = (n * R(0.12f, 0.3f) + spread * R(0.2f, 0.5f) + Up * R(0.1f, 0.25f)) * k;
                        Dust.Puff(p, v, R(0.05f, 0.09f) * k, R(1.0f, 1.6f), R(0f, 360f));
                    }
                    else
                    {
                        Vector3 v = (n * R(0.7f, 1.6f) + spread * R(0.5f, 1.3f) + Up * R(0.15f, 0.45f) + RandomHorizontal() * 0.08f) * k;
                        Dust.Puff(p - n * R(0f, 0.05f) * k, v, R(0.045f, 0.11f) * k, R(0.8f, 1.5f), R(0f, 360f));
                    }
                }
            }
            if (!linger && woodChips)
                for (int i = 0; i < Mathf.RoundToInt(4f * dustAmount); i++)
                {
                    Vector3 cv = sideOpening ? n * R(0.6f, 1.2f) + RandomHorizontal() * R(0.1f, 0.3f) + Up * R(0.8f, 1.4f)
                                             : RandomHorizontal() * R(0.3f, 0.9f) + Up * R(1.2f, 2.0f);
                    Dust.Chip(c, cv * k, R(0.01f, 0.018f) * k, R(1.0f, 1.6f));
                }
        }

        void LidRing(float s, float k, float amount)
        {
            if (lid == null) return;
            Renderer[] rs = lid.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            // kapagin tabandaki ayak izi (kasa ekseninde)
            Vector3 ax = transform.right, az = transform.forward;
            Vector3 c = OnGround(b.center, 0.006f * k);
            float ex = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z))));
            float ez = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(az.x), Mathf.Abs(az.y), Mathf.Abs(az.z))));
            int n = Mathf.RoundToInt((10f + 22f * s) * amount);
            for (int i = 0; i < n; i++)
            {
                float a = (i + R(-0.3f, 0.3f)) / n * Mathf.PI * 2f;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                // dikdortgen cevresi
                float m = Mathf.Max(Mathf.Abs(cx) / Mathf.Max(ex, 1e-3f), Mathf.Abs(cz) / Mathf.Max(ez, 1e-3f));
                if (rng.NextDouble() < 0.15) continue;
                Vector3 edge = c + (ax * cx + az * cz) / m;
                Vector3 dir = Horizontal(edge - c).normalized;
                Vector3 v = (dir * R(0.35f, 0.85f) * s + Up * R(0.04f, 0.18f)) * k;
                Dust.Puff(edge + dir * R(0f, 0.03f) * k, v, R(0.03f, 0.07f + 0.02f * s) * k, R(0.55f, 0.95f), R(0f, 360f));
            }
            if (woodChips)
                for (int i = 0; i < Mathf.RoundToInt(3f * s * amount); i++)
                    Dust.Chip(c, (RandomHorizontal() * R(0.4f, 0.9f) + Up * R(0.5f, 1.0f)) * k, R(0.008f, 0.014f) * k, R(0.8f, 1.2f));
        }

        // =================================================================== ses
        AudioClip Pick(AudioClip[] list)
        {
            if (list == null || list.Length == 0) return null;
            return list[rng.Next(list.Length)];
        }

        void Play(AudioClip clip, Vector3 pos, float vol, float pitch)
        {
            if (clip == null || volume <= 0f) return;
            if (voices.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    var go = new GameObject("CrateVoice" + i);
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(transform, false);
                    var a = go.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = spatialBlend;
                    a.rolloffMode = AudioRolloffMode.Linear;
                    a.minDistance = 1.5f;
                    a.maxDistance = hearingDistance;
                    a.dopplerLevel = 0f;
                    a.outputAudioMixerGroup = mixerGroup;
                    voices.Add(a);
                }
            }
            AudioSource v = voices[voiceIndex];
            voiceIndex = (voiceIndex + 1) % voices.Count;
            v.transform.position = pos;
            v.clip = clip;
            v.volume = Mathf.Clamp01(vol) * volume;
            v.pitch = pitch;
            v.Play();
        }
    }
}
