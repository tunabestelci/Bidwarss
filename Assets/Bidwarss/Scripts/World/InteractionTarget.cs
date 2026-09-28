using UnityEngine;

namespace Bidwarss
{
    public enum TargetKind : byte { Crate, Item, Slot }

    public sealed class InteractionTarget : MonoBehaviour
    {
        public TargetKind kind;
        public int id;
    }
}
