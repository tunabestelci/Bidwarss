using UnityEngine;

namespace Bidwarss
{
    // The Bruno cel shader draws its ink line as an inverted hull (the mesh pushed out along its normals). On thin parts
    // (crowbar, blades) and on hard-edged meshes the hull tears apart and shows up as stray black lines / black shards.
    // This caps the ink width per renderer without touching the shared materials: tools get no hull at all, hands and the
    // character a much thinner one. Eyes / iris / ink materials (width 0) stay 0.
    public static class BrunoOutline
    {
        public const float ToolWidth = 0f, HandWidth = .0008f, BodyWidth = .0010f;
        static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");

        public static void Cap(GameObject root, float maxWidth)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty(WidthId)) continue;
                    float own = m.GetFloat(WidthId);
                    if (own <= maxWidth) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    block.SetFloat(WidthId, maxWidth);
                    r.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
