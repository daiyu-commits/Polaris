using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Polaris
{
        internal static class Theme
    {
        public static readonly Color Bg1 = Color.FromArgb(9, 8, 14);
        public static readonly Color Bg2 = Color.FromArgb(18, 15, 27);
        public static readonly Color Card = Color.FromArgb(20, 18, 31);
        public static readonly Color CardHi = Color.FromArgb(27, 24, 41);
        public static readonly Color Line = Color.FromArgb(40, 36, 58);
        public static readonly Color Text = Color.FromArgb(237, 233, 248);
        public static readonly Color Muted = Color.FromArgb(138, 131, 158);
        public static readonly Color Dim = Color.FromArgb(96, 90, 114);
        public static readonly Color Accent = Color.FromArgb(146, 98, 255);
        public static readonly Color Accent2 = Color.FromArgb(88, 66, 230);
        public static readonly Color Ok = Color.FromArgb(52, 211, 153);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Err = Color.FromArgb(248, 113, 113);

                public static float Dpi = 1f;

        public static Font Ui(float size, FontStyle style = FontStyle.Regular) =>
            new Font("Microsoft YaHei UI", size * Dpi, style, GraphicsUnit.Point);

        public static Font Mono(float size) => new Font("Consolas", size * Dpi, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color fill)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width = 1f)
        {
            using (var path = Round(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, path);
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (var b = new SolidBrush(c))
                g.DrawString(s, f, b, x, y);
        }

                public static void AccentBar(Graphics g, float x, float y, float h)
        {
            using (var lg = new LinearGradientBrush(new PointF(x, y), new PointF(x, y + h), Accent, Accent2))
            using (var path = Round(new RectangleF(x, y, 5, h), 2.5f))
                g.FillPath(lg, path);
        }

        public static void HLineFade(Graphics g, float x, float y, float w, Color c)
        {
            using (var lg = new LinearGradientBrush(new PointF(x, y), new PointF(x + w, y), c, Color.FromArgb(0, c)))
            using (var pen = new Pen(lg, 1f))
                g.DrawLine(pen, x, y, x + w, y);
        }
    }

        internal sealed class AccentButton : Control
    {
        public bool Primary = true;
        public string SubText = "";
        public bool Busy;
        private bool _hover, _down;
        private float _hoverT;
        private float _angle;
        private readonly System.Windows.Forms.Timer _anim;

        public AccentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Ui(12f, FontStyle.Bold);
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;

            _anim = new System.Windows.Forms.Timer { Interval = 16 };
            _anim.Tick += (s, e) =>
            {
                bool dirty = false;
                float target = (_hover && Enabled) ? 1f : 0f;
                if (Math.Abs(_hoverT - target) > 0.01f)
                {
                    _hoverT += (target - _hoverT) * 0.22f;
                    dirty = true;
                }
                else if (_hoverT != target) { _hoverT = target; dirty = true; }

                if (Busy) { _angle = (_angle + 9f) % 360f; dirty = true; }
                if (dirty) Invalidate();
                if (!Busy && _hoverT == 0f) _anim.Stop();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; _anim.Start(); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; _anim.Start(); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { _anim.Start(); Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            if (Primary)
            {
                Color a = Theme.Accent, b = Theme.Accent2;
                if (!Enabled) { a = Color.FromArgb(60, 54, 74); b = Color.FromArgb(48, 44, 62); }
                else if (_down) { a = Darken(a, 40); b = Darken(b, 40); }
                else if (_hover) { a = Lighten(a, 40); b = Lighten(b, 40); }

                using (var path = Theme.Round(r, 9f))
                {
                    if (Enabled && _hoverT > 0.02f)
                        using (var glow = new Pen(Color.FromArgb((int)(70 * _hoverT), Theme.Accent), 6f))
                        using (var gp = Theme.Round(new RectangleF(3, 3, Width - 7, Height - 7), 10f))
                            g.DrawPath(glow, gp);

                    using (var lg = new LinearGradientBrush(new PointF(0, 0), new PointF(Width, Height), a, b))
                        g.FillPath(lg, path);

                                        if (Enabled && _hoverT > 0.02f)
                    {
                        var st = g.Save();
                        g.SetClip(path);
                        float x = -Width + _hoverT * Width * 2.2f;
                        using (var sheen = new LinearGradientBrush(
                                   new PointF(x, 0), new PointF(x + Width * 0.55f, Height * 0.9f),
                                   Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255)))
                        {
                            sheen.SetBlendTriangularShape(0.5f, 1f);
                            g.FillRectangle(sheen, 0, 0, Width, Height);
                        }
                        g.Restore(st);
                    }
                }

                                if (Busy)
                {
                    float rad = Math.Min(Width, Height) * 0.16f;
                    var c = new RectangleF(Width - rad * 2 - 16, Height / 2f - rad, rad * 2, rad * 2);
                    using (var bgp = new Pen(Color.FromArgb(70, 255, 255, 255), 2.2f)) g.DrawEllipse(bgp, c);
                    using (var pen = new Pen(Color.White, 2.2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, c, _angle, 110f);
                    }
                }

                TextOn(g, r, Enabled ? Color.FromArgb(8, 6, 16) : Color.FromArgb(120, 112, 140));
            }
            else
            {
                Color fill = Theme.Card;
                if (!Enabled) fill = Color.FromArgb(15, 13, 22);
                else if (_down) fill = Color.FromArgb(30, 26, 46);
                else if (_hover) fill = Color.FromArgb(24, 21, 38);

                using (var path = Theme.Round(r, 9f))
                using (var br = new SolidBrush(fill))
                    g.FillPath(br, path);

                int edge = Enabled
                    ? (int)(Theme.Line.R + (70 - Theme.Line.R) * _hoverT)
                    : 26;
                Theme.StrokeRound(g, r, 9f, Color.FromArgb(edge, 60, 52, 90));

                TextOn(g, r, Enabled ? Theme.Text : Theme.Dim);
            }
        }

        private void TextOn(Graphics g, RectangleF r, Color c)
        {
            if (string.IsNullOrEmpty(SubText))
            {
                TextRenderer.DrawText(g, Text, Font, Rectangle.Round(r), c,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                var top = new RectangleF(r.X, r.Y + 9, r.Width, r.Height / 2);
                var bot = new RectangleF(r.X, r.Y + r.Height / 2 + 2, r.Width, r.Height / 2);
                TextRenderer.DrawText(g, Text, Font, Rectangle.Round(top), c,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                using (var f2 = Theme.Ui(8f))
                    TextRenderer.DrawText(g, SubText, f2, Rectangle.Round(bot), Color.FromArgb(150, c),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }
        }

        private static Color Lighten(Color c, int d) =>
            Color.FromArgb(c.A, Math.Min(255, c.R + d), Math.Min(255, c.G + d), Math.Min(255, c.B + d));

        private static Color Darken(Color c, int d) =>
            Color.FromArgb(c.A, Math.Max(0, c.R - d), Math.Max(0, c.G - d), Math.Max(0, c.B - d));

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _anim.Stop(); _anim.Dispose(); } catch { } }
            base.Dispose(disposing);
        }
    }

        internal sealed class Pill : Control
    {
        public Color Tone = Theme.Muted;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Ui(9f, FontStyle.Bold);
            Height = 26;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRound(g, r, r.Height / 2, Color.FromArgb(28, Tone));
            Theme.StrokeRound(g, r, r.Height / 2, Color.FromArgb(90, Tone));
            using (var b = new SolidBrush(Tone))
                g.FillEllipse(b, 11f, r.Height / 2 - 2.5f, 5f, 5f);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(22, 0, Width - 26, Height), Tone,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

        internal sealed class WindowButton : Control
    {
        public bool Close;
        private bool _hot, _down;

        public WindowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(40, 30);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hot = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_hot)
            {
                Color bg = Close
                    ? Color.FromArgb(_down ? 190 : 215, 232, 17, 35)
                    : Color.FromArgb(_down ? 26 : 18, 255, 255, 255);
                using (var path = Theme.Round(new RectangleF(2, 2, Width - 4, Height - 4), 7f))
                using (var b = new SolidBrush(bg))
                    g.FillPath(b, path);
            }

            Color fg = Close && _hot ? Color.White : Color.FromArgb(_hot ? 235 : 168, 210, 205, 232);
            float cx = Width / 2f, cy = Height / 2f;
            using (var pen = new Pen(fg, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (Close)
                {
                    float d = 4.6f;
                    g.DrawLine(pen, cx - d, cy - d, cx + d, cy + d);
                    g.DrawLine(pen, cx - d, cy + d, cx + d, cy - d);
                }
                else
                {
                    g.DrawLine(pen, cx - 5f, cy, cx + 5f, cy);
                }
            }
        }
    }
}
