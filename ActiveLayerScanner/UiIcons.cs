using System.Drawing;
using System.Drawing.Drawing2D;

namespace ActiveLayerScanner
{
    /// <summary>Toolbar icons drawn with GDI+ (no external image resources needed).</summary>
    internal static class UiIcons
    {
        private const int S = 20;
        private static readonly Color Blue = Color.FromArgb(0, 102, 204);
        private static readonly Color Red = Color.FromArgb(200, 40, 40);
        private static readonly Color Green = Color.FromArgb(30, 140, 70);
        private static readonly Color Gray = Color.FromArgb(90, 90, 90);

        private static Bitmap Make(System.Action<Graphics> draw)
        {
            var bmp = new Bitmap(S, S);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g);
            }
            return bmp;
        }

        private static Pen P(Color c, float w = 2f) => new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        public static Bitmap Load() => Make(g =>
        {
            using (var p = P(Blue, 2f)) g.DrawArc(p, 3, 3, 13, 13, 40, 280);
            using (var b = new SolidBrush(Blue)) g.FillPolygon(b, new[] { new Point(14, 1), new Point(18, 8), new Point(10, 7) });
        });

        public static Bitmap Scan() => Make(g =>
        {
            using (var p = P(Blue, 2f)) { g.DrawEllipse(p, 2, 2, 11, 11); g.DrawLine(p, 11.5f, 11.5f, 17.5f, 17.5f); }
        });

        public static Bitmap Cancel() => Make(g =>
        {
            using (var b = new SolidBrush(Red)) g.FillEllipse(b, 2, 2, 16, 16);
            using (var p = P(Color.White, 2f)) { g.DrawLine(p, 7, 7, 13, 13); g.DrawLine(p, 13, 7, 7, 13); }
        });

        public static Bitmap Remove() => Make(g =>
        {
            using (var p = P(Red, 1.8f))
            {
                g.DrawLine(p, 3, 5, 17, 5);
                g.DrawRectangle(p, 8, 2, 4, 3);
                g.DrawPolygon(p, new[] { new Point(5, 5), new Point(15, 5), new Point(14, 18), new Point(6, 18) });
                g.DrawLine(p, 8.5f, 8, 8.5f, 15); g.DrawLine(p, 11.5f, 8, 11.5f, 15);
            }
        });

        public static Bitmap Export() => Make(g =>
        {
            using (var p = P(Green, 2f))
            {
                g.DrawLine(p, 10, 2, 10, 11);
                g.DrawLine(p, 6, 8, 10, 12); g.DrawLine(p, 14, 8, 10, 12);
                g.DrawLine(p, 3, 14, 3, 17); g.DrawLine(p, 3, 17, 17, 17); g.DrawLine(p, 17, 17, 17, 14);
            }
        });

        public static Bitmap SelectAll() => Make(g =>
        {
            using (var p = P(Gray, 1.8f)) g.DrawRectangle(p, 3, 3, 14, 14);
            using (var p = P(Green, 2.2f)) { g.DrawLine(p, 6, 10, 9, 13.5f); g.DrawLine(p, 9, 13.5f, 14.5f, 6); }
        });

        public static Bitmap SelectNone() => Make(g =>
        {
            using (var p = P(Gray, 1.8f)) g.DrawRectangle(p, 3, 3, 14, 14);
        });
    }
}
