// Raf / masa / zemin uzerinde bir esyanin konabilecegi yer.
// Transform = slot hacminin MERKEZI, size = hacim (metre). Esyalar tabana (BottomCenter) oturtulur.
// Not: onceki projedeki ShelfSlot ile cakismasin diye adi DepoShelfSlot.
using UnityEngine;

namespace DepoLevel
{
    public class DepoShelfSlot : MonoBehaviour
    {
        public string zoneId;
        [Tooltip("raf, palet, masa, zemin, aski, vitrin, kaide, pano, lastik, kutu_sakla, kutu_sat, gelen, sevk, degerleme")]
        public string slotType;
        [Tooltip("0 = en alt kat")]
        public int level;
        public Vector3 size = Vector3.one;
        public Color gizmoColor = Color.white;
        public bool showGizmo;

        [Header("Calisma zamani")]
        public GameObject occupant;

        public bool IsFree => occupant == null;
        public Vector3 BottomCenter => transform.TransformPoint(new Vector3(0f, -size.y * 0.5f, 0f));

        /// Esya boyutu slota sigiyor mu (Y ekseninde 90 derece donmus hali de denenir).
        public bool CanFit(Vector3 itemSize)
        {
            if (itemSize.y > size.y) return false;
            return (itemSize.x <= size.x && itemSize.z <= size.z) || (itemSize.z <= size.x && itemSize.x <= size.z);
        }

        public bool TryPlace(GameObject item, bool snap = true)
        {
            if (item == null || !IsFree) return false;
            occupant = item;
            if (snap)
            {
                item.transform.SetPositionAndRotation(BottomCenter, transform.rotation);
                var rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
#endif
                    rb.angularVelocity = Vector3.zero;
                }
            }
            return true;
        }

        public GameObject Release()
        {
            var o = occupant;
            occupant = null;
            return o;
        }

        void OnDrawGizmos()
        {
            if(!showGizmo)return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, IsFree ? 0.55f : 0.15f);
            Gizmos.DrawWireCube(Vector3.zero, size * 0.98f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
            Gizmos.DrawCube(Vector3.zero, size * 0.98f);
        }
    }
}
