// Konteyner icinde rastgele esya dogma noktasi. Pozisyon = zemin, Z+ = esyanin on yonu.
using UnityEngine;

namespace DepoLevel
{
    public class DepoLootSpawn : MonoBehaviour
    {
        public int containerNo;
        public int index;
        [Tooltip("Bu noktaya konulabilecek en buyuk esya (metre). 0 = sinirsiz.")]
        public Vector3 maxSize = new Vector3(0.9f, 1.2f, 0.9f);

        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            return Instantiate(prefab, transform.position, transform.rotation);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.3f);
        }
    }
}
