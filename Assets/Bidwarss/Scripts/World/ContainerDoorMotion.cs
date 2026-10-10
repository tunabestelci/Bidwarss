using UnityEngine;
namespace Bidwarss
{
    public sealed class ContainerDoorMotion : MonoBehaviour
    {
        public WarehouseWorld world;
        public int crateIndex;
        public Transform left,right;
        public float duration=1.05f;
        void LateUpdate()
        {
            if(world==null||!world.IsSpawned||crateIndex>=world.Crates.Count)return;
            var state=world.Crates[crateIndex];
            float angle,shake=0;
            if(state.opened)
            {
                float t=Mathf.Clamp01((float)(world.NetworkManager.ServerTime.Time-state.openedAt)/duration);
                // Ease out with a small overshoot: the doors are flung wide, bounce back and settle.
                float u=t-1;
                angle=180*(1+2.2f*u*u*u+1.2f*u*u);
                shake=(1-t)*Mathf.Sin(Time.time*38)*1.4f;
            }
            else
            {
                // While somebody holds E the doors strain against the latch: a growing crack and a rattle.
                float effort=state.opener!=ItemState.Nobody?state.progress:0;
                // Cutting tape or prying a lid only makes the container tremble; bare hands crack the doors open.
                if(state.openingMode!=Bidwarss.Domain.OpeningMode.Hands)effort*=.3f;
                angle=effort*effort*10;
                shake=Mathf.Sin(Time.time*57)*effort*1.8f+Mathf.Sin(Time.time*23)*effort*effort*1.2f;
            }
            // Both leaves sweep into the container, keeping the central corridor clear.
            left.localRotation=Quaternion.Euler(0,180+angle+shake,0);
            right.localRotation=Quaternion.Euler(0,180-angle-shake,0);
        }
    }
}
