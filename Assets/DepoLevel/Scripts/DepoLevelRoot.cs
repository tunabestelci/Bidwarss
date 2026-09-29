// Seviyenin kok bileseni: bolge listesi + hizli erisim (slotlar, konteynerler, tetikler).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DepoLevel
{
    [Serializable]
    public class DepoZoneInfo
    {
        public string id;
        public string displayName;
        public Color color = Color.white;
        public List<Rect> rects = new List<Rect>(); // kok nesnenin yerel XZ duzleminde
        public Vector3 teleportPoint;
        public float teleportYaw;
    }

    [DisallowMultipleComponent]
    public class DepoLevelRoot : MonoBehaviour
    {
        public static DepoLevelRoot Instance { get; private set; }

        [Tooltip("Builder tarafindan doldurulur.")]
        public List<DepoZoneInfo> zones = new List<DepoZoneInfo>();
        public string layoutVersion;

        [NonSerialized] public List<DepoShelfSlot> slots = new List<DepoShelfSlot>();
        [NonSerialized] public List<DepoContainer> containers = new List<DepoContainer>();
        [NonSerialized] public List<DepoTrigger> triggers = new List<DepoTrigger>();

        void Awake()
        {
            Instance = this;
            Refresh();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Refresh()
        {
            slots.Clear(); containers.Clear(); triggers.Clear();
            GetComponentsInChildren(true, slots);
            GetComponentsInChildren(true, containers);
            GetComponentsInChildren(true, triggers);
            containers.Sort((a, b) => a.containerNo.CompareTo(b.containerNo));
        }

        public DepoZoneInfo GetZone(string id)
        {
            return zones.Find(z => z.id == id);
        }

        public Color GetZoneColor(string id)
        {
            var z = GetZone(id);
            return z != null ? z.color : Color.white;
        }

        /// Dunya konumunun hangi bolgede oldugunu dondurur (yoksa null).
        public DepoZoneInfo GetZoneAt(Vector3 worldPos)
        {
            Vector3 p = transform.InverseTransformPoint(worldPos);
            var xz = new Vector2(p.x, p.z);
            foreach (var z in zones)
                foreach (var r in z.rects)
                    if (r.Contains(xz)) return z;
            return null;
        }

        public DepoContainer GetContainer(int no)
        {
            return containers.Find(c => c.containerNo == no);
        }

        public IEnumerable<DepoShelfSlot> GetSlots(string zoneId, bool onlyFree = false)
        {
            foreach (var s in slots)
                if (s.zoneId == zoneId && (!onlyFree || s.IsFree)) yield return s;
        }

        /// Bolgedeki en yakin bos slot (esya boyutu verilirse sigan slotlar arasindan).
        public DepoShelfSlot FindNearestFreeSlot(string zoneId, Vector3 from, Vector3? itemSize = null)
        {
            DepoShelfSlot best = null; float bestD = float.MaxValue;
            foreach (var s in slots)
            {
                if (!s.IsFree || (zoneId != null && s.zoneId != zoneId)) continue;
                if (itemSize.HasValue && !s.CanFit(itemSize.Value)) continue;
                float d = (s.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }
    }
}
