using UnityEngine;
namespace Bidwarss
{
    public sealed class ContainerDoorMotion : MonoBehaviour
    {
        public WarehouseWorld world;
        public int crateIndex;
        public Transform left,right;
        public float duration=.9f;
        void LateUpdate()
        {
            if(world==null||!world.IsSpawned||crateIndex>=world.Crates.Count)return;
            var state=world.Crates[crateIndex];
            float t=state.opened?Mathf.Clamp01((float)(world.NetworkManager.ServerTime.Time-state.openedAt)/duration):0;
            t=t*t*(3-2*t);
            // Both leaves sweep into the container, keeping the central corridor clear.
            left.localRotation=Quaternion.Euler(0,180+180*t,0);
            right.localRotation=Quaternion.Euler(0,180-180*t,0);
        }
    }
}
