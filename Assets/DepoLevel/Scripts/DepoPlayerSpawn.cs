// Oyuncu baslangic noktasi (konteyner koridorunun guney ucu, kuzeye bakar).
using UnityEngine;

namespace DepoLevel
{
    public class DepoPlayerSpawn : MonoBehaviour
    {
        public static DepoPlayerSpawn Current { get; private set; }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        /// Oyuncuyu buraya tasir (CharacterController varsa gecici kapatilir).
        public void Place(Transform player)
        {
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (had) cc.enabled = false;
            player.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            if (had) cc.enabled = true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.9f);
            Vector3 p = transform.position;
            Gizmos.DrawWireCube(p + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
            Gizmos.DrawLine(p + Vector3.up * 1.65f, p + Vector3.up * 1.65f + transform.forward * 0.8f);
        }
    }
}
