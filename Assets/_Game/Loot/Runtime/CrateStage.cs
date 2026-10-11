// =====================================================================================
//  CrateStage  -  konteynerin icindeki ahsap kasa (animasyonlu acilis) ile depo oyununu baglar.
//
//  Oyuncu kasa aciciyla E'ye basili tuttukca sunucu WarehouseWorld.Crates[i].progress'i artirir.
//  Vidalar SIRAYLA tek tek sokulur; E birakilirsa sokulmekte olan vida yarida bekler, tekrar basilinca
//  ayni vidadan devam eder. Tum vidalar cikinca kapak kanirtilip devrilir (toz + ses).
//  Esyalar disari firlamaz: kasanin icinde, tabaninda durur; oyuncular kendileri alir.
//  Gec katilan oyuncu / yeni tur: kasa animasyonsuz hemen dogru haline gelir.
// =====================================================================================
using Bidwars.Crates;
using Bidwarss;
using Bidwarss.Domain;
using UnityEngine;

namespace Bidwars.Loot
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Bidwars/Crate Stage")]
    public sealed class CrateStage : MonoBehaviour, ICrateStage
    {
        [Tooltip("Sahnedeki WarehouseWorld (bos ise otomatik bulunur).")] public WarehouseWorld world;
        [Tooltip("Bu kasanin depodaki numarasi (WarehouseWorld.boxes sirasi).")] public int crateIndex;
        [Tooltip("Kasanin turu: hangi esyalarin cikabilecegini WarehouseWorld.crateKinds belirler, bu sadece bilgi.")] public CrateKind kind;
        public CrateOpener crate;
        [Tooltip("Bir vidayi sokmek icin kac saniye E basili tutulmali.")] [Range(0.2f, 3f)] public float screwSeconds = 0.45f;
        [Tooltip("Kapagin kanirtilip devrilme hizi (1 = klip hizi).")] [Range(0.5f, 3f)] public float animationSpeed = 2.4f;
        [Tooltip("Eski surumden kalma (artik kullanilmiyor): esyalarin dustugu nokta.")] public Transform drop;

        bool released = true;
        bool knownOpen;
        string knownRun;
        float shownWork = -1f;

        public bool ItemsReleased { get { return crate == null || released; } }
        public float WorkSeconds { get { return Mathf.Max(1, crate != null ? crate.ScrewCount : 4) * screwSeconds; } }

        // Kasanin ic hacmi: acilis agzi (FX_Opening_0/1 koseleri) ile ic taban/arka duvar (FX_Floor) arasindaki kutu.
        public bool Interior(out Vector3 floorCenter, out Vector3 longAxis, out float length, out float width, out float height)
        {
            floorCenter = transform.position; longAxis = transform.right; length = width = height = 0f;
            if (crate == null || crate.fxOpening0 == null || crate.fxOpening1 == null || crate.fxFloor == null) return false;
            Transform t = transform;
            Vector3 o0 = t.InverseTransformPoint(crate.fxOpening0.position), o1 = t.InverseTransformPoint(crate.fxOpening1.position);
            Vector3 f = t.InverseTransformPoint(crate.fxFloor.position), oc = (o0 + o1) * 0.5f, n = oc - f;
            int ax = Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z) ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);
            Vector3 min = Vector3.Min(o0, o1), max = Vector3.Max(o0, o1);
            min[ax] = Mathf.Min(f[ax], oc[ax]); max[ax] = Mathf.Max(f[ax], oc[ax]);
            Vector3 size = Vector3.Scale(max - min, t.lossyScale);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Vector3 c = (min + max) * 0.5f;
            floorCenter = t.TransformPoint(new Vector3(c.x, min.y, c.z));
            bool xLong = size.x >= size.z;
            longAxis = (xLong ? t.right : t.forward); longAxis.y = 0f; longAxis.Normalize();
            length = xLong ? size.x : size.z; width = xLong ? size.z : size.x; height = size.y;
            return true;
        }

        // ------------------------------------------------------------------ esyaya gore kasa boyu
        // Kasa her tur icindeki esyaya gore yeniden boyutlanir (eksen basina, 0.4x - 1.45x). Vidalar neredeyse ayni boyda kalir.
        [Tooltip("Kasanin esyaya gore kucultulup buyutulebilecegi sinirlar (eksen basina carpan).")] public Vector2 shapeLimits = new Vector2(0.12f, 5f);
        Vector3 baseScale = Vector3.one, baseInterior = Vector3.one;
        float baseBottom;
        bool baseCaptured;
        Vector3 appliedShape = Vector3.one;

        void CaptureBase()
        {
            if (baseCaptured) return;
            baseCaptured = true;
            baseScale = transform.localScale;
            baseBottom = BodyBottom();
            baseInterior = InteriorLocalMetres();
        }

        // kasanin ic hacmi, kasanin kendi eksenlerinde (x, y, z), metre
        Vector3 InteriorLocalMetres()
        {
            if (crate == null || crate.fxOpening0 == null || crate.fxOpening1 == null || crate.fxFloor == null) return Vector3.one;
            Transform t = transform;
            Vector3 o0 = t.InverseTransformPoint(crate.fxOpening0.position), o1 = t.InverseTransformPoint(crate.fxOpening1.position);
            Vector3 f = t.InverseTransformPoint(crate.fxFloor.position), oc = (o0 + o1) * 0.5f, n = oc - f;
            int ax = Mathf.Abs(n.x) >= Mathf.Abs(n.y) && Mathf.Abs(n.x) >= Mathf.Abs(n.z) ? 0 : (Mathf.Abs(n.y) >= Mathf.Abs(n.z) ? 1 : 2);
            Vector3 min = Vector3.Min(o0, o1), max = Vector3.Max(o0, o1);
            min[ax] = Mathf.Min(f[ax], oc[ax]); max[ax] = Mathf.Max(f[ax], oc[ax]);
            Vector3 s = Vector3.Scale(max - min, t.localScale);
            return new Vector3(Mathf.Max(0.05f, Mathf.Abs(s.x)), Mathf.Max(0.05f, Mathf.Abs(s.y)), Mathf.Max(0.05f, Mathf.Abs(s.z)));
        }

        float BodyBottom()
        {
            Transform b = crate != null && crate.body != null ? crate.body : transform;
            var rs = b.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return transform.position.y;
            float y = float.MaxValue;
            foreach (var r in rs) y = Mathf.Min(y, r.bounds.min.y);
            return y;
        }

        public int ChooseShape(Vector3[] cells, int seed, out Vector3 shape)
        {
            CaptureBase();
            shape = Vector3.one;
            if (cells == null || cells.Length == 0) return 0;
            // one cell size that holds every item (largest side per axis), so mixed cells still fit
            Vector3 cell = cells[0];
            foreach (var c in cells) cell = Vector3.Max(cell, c);
            Vector3 need = CratePacking.CrateInterior(cell, cells.Length);   // x uzun kenar, y yukari, z capraz
            // cok kucuk esyada da kasa gibi gorunsun (tepsiye donmesin)
            need = new Vector3(Mathf.Max(need.x, 0.26f), Mathf.Max(need.y, 0.2f), Mathf.Max(need.z, 0.24f));
            bool xLong = baseInterior.x >= baseInterior.z;
            Vector3 s = new Vector3((xLong ? need.x : need.z) / baseInterior.x, need.y / baseInterior.y, (xLong ? need.z : need.x) / baseInterior.z);
            s.x = Mathf.Clamp(s.x, shapeLimits.x, shapeLimits.y);
            s.y = Mathf.Clamp(s.y, shapeLimits.x, shapeLimits.y);
            s.z = Mathf.Clamp(s.z, shapeLimits.x, shapeLimits.y);
            shape = s;
            return 0;
        }

        void ApplyShape(Vector3 s)
        {
            CaptureBase();
            if (s.x <= 0f || s.y <= 0f || s.z <= 0f) s = Vector3.one;
            appliedShape = s;
            transform.localScale = Vector3.Scale(baseScale, s);
            transform.position += Vector3.up * (baseBottom - BodyBottom());
            // vidalar: kasa ne kadar uzayip kisalirsa kissin vida AYNI kalir (CrateScrewViews: bozulmamis kopyalar cizilir)
            if (crate != null) crate.SetScrewScale(1f);
        }

        public int WorkSteps { get { return crate != null ? Mathf.Max(1, crate.ScrewCount) : 1; } }

        // The crate opener works on the screw that is being unscrewed right now; it presses in from the open side.
        public bool WorkPoint(out Vector3 point, out Vector3 along, out Vector3 normal)
        {
            point = transform.position; along = transform.right; normal = Vector3.up;
            if (crate == null || crate.IsOpen || released) return false;
            if (!crate.CurrentScrew(Mathf.Max(0f, shownWork), out point)) return false;
            normal = crate.OpeningNormal;
            along = Vector3.ProjectOnPlane(transform.right, normal);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(transform.forward, normal);
            along.Normalize();
            return true;
        }

        void Awake()
        {
            if (crate == null) crate = GetComponentInChildren<CrateOpener>(true);
            if (crate != null)
            {
                crate.speed = animationSpeed;
                crate.onLidOff.AddListener(OnLidOff);
                released = false;
            }
            CaptureBase();
            // vidalarin gorunumu kasanin olcegine baglanmasin (kasa eksen basina olceklenince vida yamulmasin)
            if (crate != null && Application.isPlaying)
            {
                var views = GetComponent<CrateScrewViews>();
                if (views == null) views = gameObject.AddComponent<CrateScrewViews>();
                views.Build(crate.screws);
            }
        }

        void OnDestroy()
        {
            if (crate != null) crate.onLidOff.RemoveListener(OnLidOff);
        }

        void OnLidOff() { released = true; }

        void LateUpdate()
        {
            if (crate == null) return;
            if (world == null)
            {
                world = WarehouseWorld.Instance;
                if (world == null) return;
            }
            if (!world.IsSpawned || crateIndex < 0 || crateIndex >= world.Crates.Count) return;

            string run = world.RunId.Value.ToString();
            var state = world.Crates[crateIndex];
            int screws = Mathf.Max(1, crate.ScrewCount);
            if (run != knownRun)
            {
                // Yeni tur (ya da katildiktan sonraki ilk kare): sunucu ne diyorsa animasyonsuz o hale gel.
                knownRun = run;
                crate.ResetClosed();
                ApplyShape(state.shape);
                crate.speed = animationSpeed;
                released = false;
                knownOpen = state.opened;
                shownWork = state.progress * screws;
                if (state.opened) { crate.OpenInstant(); released = true; }
                else if (shownWork > 0f) crate.SetSteppedWork(shownWork, true);
                return;
            }
            if (state.opened && !knownOpen)
            {
                knownOpen = true;
                double age = world.NetworkManager.ServerTime.Time - state.openedAt;
                if (age > 2.5) { crate.OpenInstant(); released = true; }   // cok once acilmis (gec katilma)
                else crate.BeginLid(false);
                return;
            }
            if (!state.opened)
            {
                // Sunucu %20 Hz gonderir; arada oyuncunun basili tuttugu hizla tahmin et ki vida akici donsun.
                float server = state.progress * screws, target = server;
                // (vidayi firlatma anini asla tahmin etme: o an sunucudan gelsin)
                if (state.opener != ItemState.Nobody && shownWork >= server - 0.5f)
                    target = Mathf.Min(Mathf.Max(server, shownWork + Time.deltaTime / Mathf.Max(0.05f, screwSeconds)),
                                       Mathf.Min(server + 0.08f, Mathf.Ceil(server + 0.0001f) - 0.001f));
                target = Mathf.Max(target, server);
                if (!Mathf.Approximately(target, shownWork)) { shownWork = Mathf.Max(shownWork, target); crate.SetSteppedWork(shownWork, false); }
            }
        }
    }

    // Kasa eksen basina farkli olceklenince (esyaya gore uzayinca) cocuk vidalar da yamulur. Bu bilesen her vidanin
    // bozulmamis bir kopyasini kasanin olceginden bagimsiz cizer: konum ve donus animasyondaki vidadan, boy kasanin
    // ilk (olceklenmemis) halinden. Asil vida gorunmez yapilir (animasyon ve sesler onun uzerinden calismaya devam eder).
    [DefaultExecutionOrder(32000)]
    public sealed class CrateScrewViews : MonoBehaviour
    {
        sealed class View { public Renderer src; public Transform dst; public Vector3 worldScale, localScale; }
        readonly System.Collections.Generic.List<View> views = new System.Collections.Generic.List<View>();
        Transform root;

        public void Build(Transform[] screws)
        {
            if (root != null || screws == null) return;
            var go = new GameObject("Vidalar (" + name + ")");
            root = go.transform;
            Transform parent = transform.parent;
            if (parent != null)
            {
                Vector3 p = parent.lossyScale;
                if (Mathf.Abs(p.x - p.y) < 1e-3f && Mathf.Abs(p.x - p.z) < 1e-3f) root.SetParent(parent, false);
            }
            root.position = Vector3.zero; root.rotation = Quaternion.identity;
            foreach (var screw in screws)
            {
                if (screw == null) continue;
                foreach (var r in screw.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var v = new GameObject(r.name);
                    v.transform.SetParent(root, false);
                    v.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var mr = v.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = r.sharedMaterials;
                    mr.shadowCastingMode = r.shadowCastingMode; mr.receiveShadows = r.receiveShadows;
                    views.Add(new View { src = r, dst = v.transform, worldScale = r.transform.lossyScale, localScale = r.transform.localScale });
                    r.forceRenderingOff = true;
                }
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (root == null) return;
            float parentScale = root.parent != null ? Mathf.Max(1e-5f, root.parent.lossyScale.x) : 1f;
            foreach (var v in views)
            {
                if (v.src == null) { if (v.dst != null) v.dst.gameObject.SetActive(false); continue; }
                bool show = v.src.enabled && v.src.gameObject.activeInHierarchy;
                if (v.dst.gameObject.activeSelf != show) v.dst.gameObject.SetActive(show);
                if (!show) continue;
                // animasyon vidayi buyutup kucultuyorsa (firlama) onu da izle
                Vector3 now = v.src.transform.localScale;
                float anim = Mathf.Abs(v.localScale.x) > 1e-6f ? now.x / v.localScale.x : 1f;
                v.dst.SetPositionAndRotation(v.src.transform.position, v.src.transform.rotation);
                v.dst.localScale = v.worldScale * (anim / parentScale);
            }
        }

        void OnEnable() { if (root != null) root.gameObject.SetActive(true); }
        void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
        void OnDestroy() { if (root != null) Destroy(root.gameObject); }
    }
}
