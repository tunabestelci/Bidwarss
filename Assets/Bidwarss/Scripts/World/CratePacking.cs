using UnityEngine;

namespace Bidwarss
{
    // How items sit inside crates, shared by the server (item positions) and the crates (their shape).
    // A "cell" is the space one item takes: x = along the crate's long side, y = up, z = across.
    public static class CratePacking
    {
        public const float Gap = .015f;

        // Cardboard parcel: tall things (candle holder, pepper mill) are packed lying on their back.
        public static bool LiesInParcel(Vector3 item) { return item.y > .26f; }

        // Pose of an item resting in a crate: 1 as modelled (y up), 2 on its back (model z up), 3 on its side (model x up).
        // Laid down (lying crate, tall things in parcels) the item rests on its largest face: its thinnest axis points up,
        // so a katana lies flat along the crate, a guitar lies on its back, a mirror lies face up.
        public static byte Pose(Vector3 item, bool layDown)
        {
            if (!layDown || (item.y <= item.x && item.y <= item.z)) return 1;
            return item.z <= item.x ? (byte)2 : (byte)3;
        }

        // Cell for a pose: x = along the crate's long side (the item's longest horizontal side), y = up, z = across.
        public static Vector3 CellFor(Vector3 item, byte pose)
        {
            switch (pose)
            {
                case 2: return new Vector3(Mathf.Max(item.y, item.x), item.z, Mathf.Min(item.y, item.x));
                case 3: return new Vector3(Mathf.Max(item.y, item.z), item.x, Mathf.Min(item.y, item.z));
                default: return new Vector3(Mathf.Max(item.x, item.z), item.y, Mathf.Min(item.x, item.z));
            }
        }

        // Rotation of the model for a pose (applied after the yaw).
        public static Quaternion PoseRotation(byte pose)
        {
            return pose == 2 ? Quaternion.Euler(-90f, 0f, 0f) : pose == 3 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        }

        // Is the model axis that ends up along the crate's long side the yaw "forward" (model z after the pose) or "right"?
        public static bool AlongForward(Vector3 item, byte pose)
        {
            switch (pose)
            {
                case 2: return item.y >= item.x;     // model y -> -forward, model x -> right
                case 3: return item.z >= item.y;     // model z -> forward, model y -> -right
                default: return item.z > item.x;     // model z -> forward, model x -> right
            }
        }

        // Wooden crates: only the lying crate lays things down. (lie = lay down)
        public static Vector3 Cell(Vector3 item, bool lie) { return CellFor(item, Pose(item, lie)); }

        // Outer size of a parcel for `count` items of one cell side by side (x length, y height, z width), metres.
        public static Vector3 ParcelSize(Vector3 cell, int count)
        {
            count = Mathf.Max(1, count);
            float l = cell.x + .06f, w = count * cell.z + (count - 1) * Gap + .05f, h = cell.y + .075f;
            return new Vector3(Mathf.Max(l, .2f), Mathf.Max(h, .11f), Mathf.Max(w, .16f));
        }

        // Offset of item n (0..count-1) across the parcel.
        public static float ParcelOffset(Vector3 cell, int n, int count) { return (n - (count - 1) * .5f) * (cell.z + Gap); }

        public const float CellGap = .04f, WallPad = .06f;

        // Wooden crate: the items stand in a grid on the inner floor (cols along the long side, rows across), as close to a
        // square footprint as possible, so a crate with 8 small things is not 3 metres long. Columns are rounded up so the
        // long side stays the long side.
        public static void Grid(Vector3 cell, int count, out int cols, out int rows)
        {
            count = Mathf.Max(1, count);
            float ratio = (cell.z + CellGap) / Mathf.Max(.01f, cell.x + CellGap);
            cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(count * Mathf.Min(1f, ratio))), 1, count);
            rows = (count + cols - 1) / cols;
        }

        // Offset of item n from the centre of the inner floor: x along the long side, z across (metres).
        public static Vector2 GridOffset(Vector3 cell, int n, int count)
        {
            int cols, rows; Grid(cell, count, out cols, out rows);
            int row = n / cols, inRow = Mathf.Min(cols, Mathf.Max(1, count - row * cols));
            return new Vector2((n % cols - (inRow - 1) * .5f) * (cell.x + CellGap), (row - (rows - 1) * .5f) * (cell.z + CellGap));
        }

        // Inner space a wooden crate should have for its contents: the grid plus a thin margin, so the items sit snugly.
        public static Vector3 CrateInterior(Vector3 cell, int count)
        {
            int cols, rows; Grid(cell, count, out cols, out rows);
            return new Vector3(cols * cell.x + (cols - 1) * CellGap + 2f * WallPad, cell.y + .06f, rows * cell.z + (rows - 1) * CellGap + 2f * WallPad);
        }
    }
}
