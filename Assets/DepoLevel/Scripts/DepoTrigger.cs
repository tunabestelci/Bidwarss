// Oyun tetikleri: SAT (Sell), COP (Trash), SAKLA (Keep) kutulari/alanlari ve etkilesim noktalari (Interact).
using System;
using UnityEngine;
using UnityEngine.Events;

namespace DepoLevel
{
    public enum DepoTriggerType { Sell, Trash, Keep, Interact }

    [Serializable] public class DepoGameObjectEvent : UnityEvent<GameObject> { }

    [RequireComponent(typeof(BoxCollider))]
    public class DepoTrigger : MonoBehaviour
    {
        public DepoTriggerType type;
        public string zoneId;
        [Tooltip("Ornek: hurda ayristirma turu (kagit, plastik, cam, metal)")]
        public string subKind;
        [Tooltip("Interact icin: envanter, satis_bilgisayari, envanter_panosu ...")]
        public string interactId;
        [Tooltip("Sadece Rigidbody'li nesneler (esyalar) tetikler.")]
        public bool onlyRigidbodies = true;

        public DepoGameObjectEvent onItemEnter = new DepoGameObjectEvent();
        public DepoGameObjectEvent onItemExit = new DepoGameObjectEvent();
        public DepoGameObjectEvent onInteract = new DepoGameObjectEvent();

        /// Tum tetikler icin tek noktadan dinleme (ornek: GameManager).
        public static event Action<DepoTrigger, GameObject> AnyItemEnter;
        public static event Action<DepoTrigger, GameObject> AnyInteract;

        public string Prompt
        {
            get
            {
                switch (type)
                {
                    case DepoTriggerType.Sell: return "SAT";
                    case DepoTriggerType.Trash: return "AT";
                    case DepoTriggerType.Keep: return "SAKLA";
                    default: return string.IsNullOrEmpty(interactId) ? "KULLAN" : interactId.Replace('_', ' ').ToUpperInvariant();
                }
            }
        }

        void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        GameObject Resolve(Collider other)
        {
            if (other.attachedRigidbody != null) return other.attachedRigidbody.gameObject;
            return onlyRigidbodies ? null : other.gameObject;
        }

        void OnTriggerEnter(Collider other)
        {
            var go = Resolve(other);
            if (go == null) return;
            onItemEnter.Invoke(go);
            AnyItemEnter?.Invoke(this, go);
        }

        void OnTriggerExit(Collider other)
        {
            var go = Resolve(other);
            if (go != null) onItemExit.Invoke(go);
        }

        public void Interact(GameObject who)
        {
            onInteract.Invoke(who);
            AnyInteract?.Invoke(this, who);
        }

        void OnDrawGizmos()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc == null) return;
            Color c;
            switch (type)
            {
                case DepoTriggerType.Sell: c = new Color(0.2f, 0.5f, 1f); break;
                case DepoTriggerType.Trash: c = new Color(0.9f, 0.25f, 0.2f); break;
                case DepoTriggerType.Keep: c = new Color(0.2f, 0.8f, 0.3f); break;
                default: c = new Color(1f, 0.85f, 0.2f); break;
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.18f);
            Gizmos.DrawCube(bc.center, bc.size);
            Gizmos.color = c;
            Gizmos.DrawWireCube(bc.center, bc.size);
        }
    }
}
