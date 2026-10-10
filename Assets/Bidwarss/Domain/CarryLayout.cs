using System;

namespace Bidwarss.Domain
{
    // A bounded 3D bundle, never ten mutually intersecting full-size objects.
    // Item art is normalized by ItemVisual into .44 x .42 x .40 metres.
    public readonly struct CarryLayout
    {
        public readonly int Columns, Rows;
        public readonly float Scale, Width, Depth;
        public CarryLayout(int count)
        {
            if (count < 1 || count > GameRules.StackSize) throw new ArgumentOutOfRangeException(nameof(count));
            Columns = count == 1 ? 1 : count <= 4 ? 2 : 3;
            Rows = count <= 2 ? 1 : 2;
            Scale = count == 1 ? 1 : count == 2 ? .65f : count <= 4 ? .52f : .40f;
            Width = Columns * .46f * Scale;
            Depth = Rows * .42f * Scale;
        }
        public void Position(int index, out float x, out float y, out float z)
        {
            x = (index % Columns - (Columns-1)*.5f) * .46f * Scale;
            z = ((index / Columns) % Rows - (Rows-1)*.5f) * .42f * Scale;
            y = index / (Columns * Rows) * .44f * Scale;
        }
    }

    public enum OpeningMode { Hands, BoxCutter, PryBar, CutThenPry }
    public enum OpeningTool { None, BoxCutter, PryBar }
    public static class OpeningSequence
    {
        public static OpeningTool Sample(OpeningMode mode, float progress, out float phase)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            if (mode == OpeningMode.CutThenPry)
            {
                bool cutting = progress < .45f;
                phase = cutting ? progress / .45f : (progress-.45f)/.55f;
                return cutting ? OpeningTool.BoxCutter : OpeningTool.PryBar;
            }
            phase = progress;
            return mode == OpeningMode.BoxCutter ? OpeningTool.BoxCutter : mode == OpeningMode.PryBar ? OpeningTool.PryBar : OpeningTool.None;
        }
        public static float MinimumDuration(OpeningMode mode) => mode == OpeningMode.CutThenPry ? 3.2f : mode == OpeningMode.Hands ? .5f : 1.6f;
    }
}
