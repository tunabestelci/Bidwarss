// Bir bolgenin (veya konteyner icinin) hacmi. Esya girip ciktiginda olay yayinlar.
using System;
using UnityEngine;

namespace DepoLevel
{
    [RequireComponent(typeof(BoxCollider))]
    public class DepoZone : MonoBehaviour
    {
        public string zoneId;
        public string category;
        public Color gizmoColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("Sadece Rigidbody'li nesneler (esyalar) icin olay yayinla. Oyuncu CharacterController'i sayilmaz.")]
        public bool onlyRigidbodies = true;

        public static event Action<DepoZone, GameObject> ItemEntered;
        public static event Action<DepoZone, GameObject> ItemExited;

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        static GameObject Resolve(Collider other, bool onlyRb)
        {
            if (other.attachedRigidbody != null) return other.attachedRigidbody.gameObject;
            return onlyRb ? null : other.gameObject;
        }

        void OnTriggerEnter(Collider other)
        {
            var go = Resolve(other, onlyRigidbodies);
            if (go != null) ItemEntered?.Invoke(this, go);
        }

        void OnTriggerExit(Collider other)
        {
            var go = Resolve(other, onlyRigidbodies);
            if (go != null) ItemExited?.Invoke(this, go);
        }

        void OnDrawGizmosSelected()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.12f);
            Gizmos.DrawCube(bc.center, bc.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(bc.center, bc.size);
        }
    }
}
