using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Polaris
{
    internal sealed class SponsorForm : Form
    {
        private readonly Image _wx, _ali;
        private readonly float _s;
        private RectangleF _closeRect;
        private bool _closeHot;

        public SponsorForm(Image wx, Image ali)
        {
            _wx = wx;
            _ali = ali;

            Text = "赞助作者";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg1;
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            using (var g = CreateGraphics()) _s = Math.Max(1f, g.DpiX / 96f);
            Theme.Dpi = _s;
            ClientSize = new Size(S(720), S(520));

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private int S(float v) => (int)Math.Round(v * _s);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int w = ClientSize.Width, h = ClientSize.Height;

            using (var lg = new LinearGradientBrush(new Point(0, 0), new Point(0, h), Theme.Bg1, Theme.Bg2))
                g.FillRectangle(lg, 0, 0, w, h);

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(new RectangleF(w - S(360), -S(220), S(480), S(480)));
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(44, Theme.Accent);
                    pgb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
                    g.FillPath(pgb, path);
                }
            }

            using (var f = Theme.Ui(15f, FontStyle.Bold))
                Theme.DrawText(g, "赞助作者", f, Theme.Text, S(32), S(24));
            using (var f = Theme.Ui(8.5f))
                Theme.DrawText(g, "扫码请作者喝杯奶茶，感谢支持", f, Theme.Muted, S(34), S(58));

            _closeRect = new RectangleF(w - S(56), S(22), S(32), S(28));
            using (var b = new SolidBrush(_closeHot ? Color.FromArgb(200, 232, 17, 35) : Color.FromArgb(0, 0, 0, 0)))
                g.FillPath(b, Theme.Round(_closeRect, S(7)));
            using (var pen = new Pen(_closeHot ? Color.White : Color.FromArgb(170, 160, 186), 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float cx = _closeRect.X + _closeRect.Width / 2, cy = _closeRect.Y + _closeRect.Height / 2;
                g.DrawLine(pen, cx - S(5), cy - S(5), cx + S(5), cy + S(5));
                g.DrawLine(pen, cx - S(5), cy + S(5), cx + S(5), cy - S(5));
            }

            Theme.HLineFade(g, S(32), S(92), w - S(64), Color.FromArgb(60, Theme.Accent));

            var left = new RectangleF(S(32), S(112), S(316), S(360));
            var right = new RectangleF(S(372), S(112), S(316), S(360));
            DrawCard(g, left, _wx, "微信赞赏", "长按识别 / 扫一扫");
            DrawCard(g, right, _ali, "支付宝", "打开支付宝「扫一扫」");

            using (var f = Theme.Ui(8f))
            {
                var tip = "赞助完全自愿，不影响任何功能  ·  交流群见左下角「加入 QQ 群」";
                Theme.DrawText(g, tip, f, Theme.Dim, S(32), h - S(34));
            }

            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, w - 1.5f, h - 1.5f), S(10)))
            using (var pen = new Pen(Color.FromArgb(34, 29, 50), 1f))
                g.DrawPath(pen, path);
        }

        private void DrawCard(Graphics g, RectangleF card, Image img, string title, string hint)
        {
            Theme.FillRound(g, card, S(12), Theme.Card);
            Theme.StrokeRound(g, card, S(12), Theme.Line);

            using (var f = Theme.Ui(10f, FontStyle.Bold))
            {
                float tw = g.MeasureString(title, f).Width;
                Theme.DrawText(g, title, f, Theme.Text, card.X + (card.Width - tw) / 2f, card.Y + S(14));
            }

            var box = new RectangleF(card.X + S(18), card.Y + S(46), card.Width - S(36), card.Height - S(96));
            if (img != null)
            {
                float scale = Math.Min(box.Width / img.Width, box.Height / img.Height);
                float iw = img.Width * scale, ih = img.Height * scale;
                var dst = new RectangleF(box.X + (box.Width - iw) / 2f, box.Y + (box.Height - ih) / 2f, iw, ih);
                using (var bg = new SolidBrush(Color.White))
                    g.FillRectangle(bg, dst);
                g.DrawImage(img, dst);
            }

            using (var f = Theme.Ui(8f))
            {
                float tw = g.MeasureString(hint, f).Width;
                Theme.DrawText(g, hint, f, Theme.Dim, card.X + (card.Width - tw) / 2f, card.Bottom - S(28));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = _closeRect.Contains(e.Location);
            if (hot != _closeHot)
            {
                _closeHot = hot;
                Cursor = hot ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location)) { Close(); return; }
            base.OnMouseDown(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.Enter || keyData == Keys.Space)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private const int WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && m.Result.ToInt32() == HTCLIENT)
            {
                var p = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));

                var hit = _closeRect;
                hit.Inflate(S(10), S(10));
                if (hit.Contains(p)) return;

                if (p.Y < S(92)) m.Result = (IntPtr)HTCAPTION;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_wx != null) _wx.Dispose(); } catch { }
                try { if (_ali != null) _ali.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
