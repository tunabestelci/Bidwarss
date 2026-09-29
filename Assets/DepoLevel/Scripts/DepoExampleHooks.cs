// ORNEK: Kendi oyun mantigini tetiklere nasil baglayacagini gosterir.
// Sahnede bos bir objeye ekle, Console'da olaylari gor. Kendi GameManager'ina tasiyinca silebilirsin.
using UnityEngine;

namespace DepoLevel
{
    public class DepoExampleHooks : MonoBehaviour
    {
        void OnEnable()
        {
            DepoTrigger.AnyItemEnter += OnItem;
            DepoTrigger.AnyInteract += OnInteract;
            DepoZone.ItemEntered += OnZone;
        }

        void OnDisable()
        {
            DepoTrigger.AnyItemEnter -= OnItem;
            DepoTrigger.AnyInteract -= OnInteract;
            DepoZone.ItemEntered -= OnZone;
        }

        void OnItem(DepoTrigger t, GameObject item)
        {
            switch (t.type)
            {
                case DepoTriggerType.Sell: Debug.Log($"[Depo] SAT: {item.name} ({t.zoneId})"); break;
                case DepoTriggerType.Trash: Debug.Log($"[Depo] AT: {item.name} {t.subKind}"); break;
                case DepoTriggerType.Keep: Debug.Log($"[Depo] SAKLA: {item.name}"); break;
            }
        }

        void OnInteract(DepoTrigger t, GameObject who)
        {
            Debug.Log($"[Depo] Etkilesim: {t.interactId}");
        }

        void OnZone(DepoZone z, GameObject item)
        {
            Debug.Log($"[Depo] {item.name} -> bolge {z.zoneId}");
        }
    }
}
