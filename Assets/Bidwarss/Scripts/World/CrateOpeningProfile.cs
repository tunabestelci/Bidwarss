using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    [DisallowMultipleComponent]
    public sealed class CrateOpeningProfile : MonoBehaviour
    {
        [Tooltip("Hands: doors; BoxCutter: taped cardboard; PryBar: nailed wood; CutThenPry: sealed wooden crate.")]
        public OpeningMode mode = OpeningMode.CutThenPry;
        [Min(.5f)] public float duration = 3.2f;
    }
}
