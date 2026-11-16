using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Polaris
{
    internal sealed class MainForm : Form
    {
        private float _s = 1f;
        private string _currentPath = "";
        private bool _busy, _scanning, _autoUpdate, _autoRunning;
        private int _shimmer;

                private float _left, _cw, _logTop, _logH;

        private TextBox _txtPath;
        private AccentButton _btnBrowse, _btnDetect, _btnInstall, _btnUninstall, _btnLaunch, _btnGet, _btnOpen, _btnClear;
        private WindowButton _btnClose, _btnMin;
        private Pill _pill;
        private Label _lblVer, _lblPlaceholder;
        private RichTextBox _rtb;
        private System.Windows.Forms.Timer _anim;
        private Image _logo;

        private PclState _pcl;
        private ExeWatcher _watch;
        private TargetRecord _record;
        private DateTime _suppressUntil = DateTime.MinValue;

                private Bitmap _bg;
        private readonly List<Star> _stars = new List<Star>();
        private float _pulse, _logoGlow;
        private float[] _chipA = new float[4];
        private bool[] _chipTarget = new bool[4];
        private Color _pillColor = Theme.Muted;
        private Color _pillTarget = Theme.Muted;
        private RectangleF _switchRect, _qqRect, _sponsorRect;
        private bool _switchHot, _qqHot, _sponsorHot;

        private sealed class Star { public float X, Y, VX, VY, R, Phase, Tw; }

        public MainForm()
        {
            Text = "Polaris修复插件";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg1;
            DoubleBuffered = true;
            KeyPreview = true;
            AllowDrop = true;
            AutoScaleMode = AutoScaleMode.None;
            Opacity = 0;
            _autoUpdate = Settings.AutoGuard;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            using (var g = CreateGraphics()) _s = Math.Max(1f, g.DpiX / 96f);
            Theme.Dpi = _s;
            try
            {
                using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                    if (st != null) using (var tmp = Image.FromStream(st)) _logo = new Bitmap(tmp);
            }
            catch { }

            BuildControls();
            ClientSize = new Size(Scale(940), Scale(680));
            MinimumSize = new Size(Scale(940), Scale(680));
            MaximumSize = new Size(Scale(940), Scale(680));
            MaximizeBox = false;
            Recompute();
            BuildStars();

            _anim = new System.Windows.Forms.Timer { Interval = 33 };
            _anim.Tick += OnTick;
            _anim.Start();

            Shown += (s, e) => { ActiveControl = null; ResetTarget(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Resize += (s, e) => { _bg = null; Recompute(); BuildStars(); };
        }

        private int Scale(float v) => (int)Math.Round(v * _s);

        private void Recompute()
        {
            float w = ClientSize.Width, h = ClientSize.Height;
            float margin = Scale(36);
            _cw = Math.Min(w - 2 * margin, Scale(1180));
            _left = (w - _cw) / 2f;
            _logTop = Scale(408);
            _logH = Math.Max(Scale(160), h - Scale(34) - _logTop);
        }

        private void BuildControls()
        {
            _txtPath = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Theme.Ui(10.5f),
                Text = ""
            };

            _btnDetect = new AccentButton { Text = "自动检测 PCL2", Primary = false, Font = Theme.Ui(9f) };
            _btnDetect.Click += (s, e) => BeginDetect();

            _btnBrowse = new AccentButton { Text = "浏览", Primary = false, Font = Theme.Ui(10f) };
            _btnBrowse.Click += (s, e) => Browse();

            _btnInstall = new AccentButton { Text = "安装插件", SubText = "INSTALL PLUGIN", Primary = true };
            _btnInstall.Click += (s, e) => Run(true);

            _btnUninstall = new AccentButton { Text = "卸载还原", Primary = false };
            _btnUninstall.Click += (s, e) => Run(false);

            _btnLaunch = new AccentButton { Text = "启动 PCL", Primary = false, Font = Theme.Ui(10f) };
            _btnLaunch.Click += (s, e) => Launch();

            _btnGet = new AccentButton { Text = "PCL2 GitHub 发布页", Primary = false, Font = Theme.Ui(9f) };
            _btnGet.Click += (s, e) => OpenReleasePage();

            _btnOpen = new AccentButton { Text = "打开目录", Primary = false, Font = Theme.Ui(10f) };
            _btnOpen.Click += (s, e) => OpenFolder();

            _btnClear = new AccentButton { Text = "清空", Primary = false, Font = Theme.Ui(8.5f) };
            _btnClear.Click += (s, e) => { _rtb.Clear(); };

            _btnClose = new WindowButton { Close = true };
            _btnClose.Click += (s, e) => Close();

            _btnMin = new WindowButton();
            _btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _lblPlaceholder = new Label
            {
                AutoSize = false,
                Font = Theme.Ui(10f),
                ForeColor = Color.FromArgb(96, 90, 116),
                BackColor = Theme.Card,
                Text = "点「自动检测 PCL2」或「浏览」选择主程序",
                Cursor = Cursors.IBeam
            };
            _lblPlaceholder.Click += (s, e) => { _txtPath.Focus(); _lblPlaceholder.Visible = false; };

            _pill = new Pill { Text = "检测中", Tone = Theme.Muted, Width = Scale(116) };

            _lblVer = new Label
            {
                AutoSize = true,
                Font = Theme.Ui(9f),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Text = ""
            };
            _rtb = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(12, 11, 20),
                ForeColor = Color.FromArgb(205, 199, 222),
                Font = Theme.Mono(9.5f),
                ReadOnly = true,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            Controls.AddRange(new Control[] { _txtPath, _lblPlaceholder, _btnDetect, _btnBrowse, _btnInstall, _btnUninstall, _btnLaunch, _btnGet, _btnOpen,
                                              _btnClear, _btnClose, _btnMin, _pill, _lblVer, _rtb });

            _lblPlaceholder.BringToFront();

            _rtb.HandleCreated += (s, e) => { try { SetWindowTheme(_rtb.Handle, "DarkMode_Explorer", null); } catch { } };
            _txtPath.HandleCreated += (s, e) => { try { SetWindowTheme(_txtPath.Handle, "DarkMode_Explorer", null); } catch { } };

            _txtPath.TextChanged += (s, e) => _lblPlaceholder.Visible = _txtPath.Text.Length == 0;
            _txtPath.LostFocus += (s, e) => _lblPlaceholder.Visible = _txtPath.Text.Length == 0;

            _txtPath.Leave += (s, e) => SetPath(_txtPath.Text.Trim(), true);
            _txtPath.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SetPath(_txtPath.Text.Trim(), true); } };
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_txtPath == null) return;

            Recompute();
            int L = (int)_left, CW = (int)_cw;
            int right = L + CW;

            _btnClose.Size = new Size(Scale(40), Scale(30));
            _btnClose.Location = new Point(right - Scale(16) - Scale(40), Scale(20));
            _btnMin.Size = new Size(Scale(40), Scale(30));
            _btnMin.Location = new Point(right - Scale(16) - Scale(40) - Scale(6) - Scale(40), Scale(20));

            _btnBrowse.Size = new Size(Scale(74), Scale(34));
            _btnBrowse.Location = new Point(right - Scale(22) - Scale(74), Scale(158));
            _btnDetect.Size = new Size(Scale(116), Scale(34));
            _btnDetect.Location = new Point(_btnBrowse.Left - Scale(8) - Scale(96), Scale(158));

            _txtPath.Location = new Point(L + Scale(22), Scale(170));
            _txtPath.Width = Math.Max(Scale(120), _btnDetect.Left - Scale(12) - (L + Scale(22)));

            _pill.Location = new Point(L, Scale(232));
            _lblVer.Location = new Point(L + Scale(132), Scale(238));

            _btnInstall.Location = new Point(L, Scale(336));
            _btnInstall.Size = new Size(Scale(236), Scale(62));
            _btnUninstall.Location = new Point(L + Scale(248), Scale(336));
            _btnUninstall.Size = new Size(Scale(136), Scale(62));
            _btnLaunch.Location = new Point(L + Scale(396), Scale(336));
            _btnLaunch.Size = new Size(Scale(120), Scale(62));
            _btnGet.Location = new Point(L + Scale(528), Scale(336));
            _btnGet.Size = new Size(Scale(152), Scale(62));
            _btnOpen.Location = new Point(L + Scale(692), Scale(336));
            _btnOpen.Size = new Size(Scale(112), Scale(62));

            _btnClear.Size = new Size(Scale(62), Scale(26));
            _btnClear.Location = new Point(right - Scale(22) - Scale(62), (int)_logTop + Scale(14));

            _lblPlaceholder.Location = new Point(L + Scale(22), Scale(169));
            _lblPlaceholder.Size = new Size(Math.Max(Scale(120), _btnDetect.Left - Scale(12) - (L + Scale(22))), Scale(22));

            _rtb.Location = new Point(L + Scale(14), (int)_logTop + Scale(48));
            _rtb.Size = new Size(CW - Scale(28), (int)_logH - Scale(60));

        }

        private void BuildStars()
        {
            _stars.Clear();
            var rnd = new Random(20261116);
            for (int i = 0; i < 30; i++)
                _stars.Add(new Star
                {
                    X = (float)rnd.NextDouble() * ClientSize.Width,
                    Y = (float)rnd.NextDouble() * ClientSize.Height,
                    VX = (float)(rnd.NextDouble() - 0.5) * 0.09f,
                    VY = 0.10f + (float)rnd.NextDouble() * 0.22f,
                    R = 0.8f + (float)rnd.NextDouble() * 1.9f,
                    Phase = (float)(rnd.NextDouble() * Math.PI * 2),
                    Tw = 0.4f + (float)rnd.NextDouble() * 0.9f
                });
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (Opacity < 1) Opacity = Math.Min(1, Opacity + 0.085);

            _pulse += 0.045f;
            _logoGlow = 0.5f + 0.5f * (float)Math.Sin(_pulse);

            float w = ClientSize.Width, h = ClientSize.Height;
            foreach (var s in _stars)
            {
                s.X += s.VX;
                s.Y -= s.VY;
                if (s.Y < -4) { s.Y = h + 4; s.X = (float)(new Random(Guid.NewGuid().GetHashCode()).NextDouble() * w); }
                if (s.X < -4) s.X = w + 4;
                if (s.X > w + 4) s.X = -4;
            }

            for (int i = 0; i < 4; i++)
                _chipA[i] += ((_chipTarget[i] ? 1f : 0f) - _chipA[i]) * 0.18f;

            _pillColor = Lerp(_pillColor, _pillTarget, 0.2f);
            if (_pill.Tone != _pillColor) { _pill.Tone = _pillColor; _pill.Invalidate(); }

            Invalidate();
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int w = ClientSize.Width, h = ClientSize.Height;
            Recompute();
            int L = (int)_left, CW = (int)_cw, R = L + CW;

            EnsureBackground(w, h);
            g.DrawImageUnscaled(_bg, 0, 0);
            DrawStars(g);

                        float lx = L, ly = Scale(26), ls = Scale(54);
            if (_logo != null)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(lx - Scale(10), ly - Scale(10), ls + Scale(20), ls + Scale(20));
                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb((int)(14 + 26 * _logoGlow), Theme.Accent);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
                        g.FillPath(pgb, path);
                    }
                }
                g.DrawImage(_logo, new RectangleF(lx, ly, ls, ls));
            }
            using (var f = Theme.Ui(23f, FontStyle.Bold))
                Theme.DrawText(g, "Polaris", f, Theme.Text, L + Scale(68), Scale(24));
            using (var f = Theme.Ui(8.5f))
                Theme.DrawText(g, "PCL2 · 启动器修复插件", f, Theme.Muted, L + Scale(70), Scale(64));
            using (var f = Theme.Ui(8f))
            {
                var v = "v1";
                Theme.DrawText(g, v, f, Theme.Dim, R - Scale(216) - g.MeasureString(v, f).Width, Scale(64));
            }

            DrawAutoSwitch(g, R);

            Theme.HLineFade(g, L, Scale(106), CW, Color.FromArgb(60, Theme.Accent));
            using (var pen = new Pen(Color.FromArgb(26, 22, 38), 1f))
                g.DrawLine(pen, 0, Scale(106), w, Scale(106));

                        using (var f = Theme.Ui(9f, FontStyle.Bold))
                Theme.DrawText(g, "目标程序", f, Theme.Dim, L, Scale(124));

            var card = new RectangleF(L, Scale(146), CW, Scale(66));
            Theme.FillRound(g, card, Scale(11), Theme.Card);
            Theme.StrokeRound(g, card, Scale(11), _scanning ? Color.FromArgb(110, Theme.Accent) : Theme.Line);

            if (_scanning)
            {
                var inner = new RectangleF(card.X + Scale(1), card.Bottom - Scale(2.5f), card.Width - Scale(2), Scale(2));
                float seg = inner.Width * 0.24f;
                float x = inner.X + (_shimmer / 1000f) * (inner.Width + seg) - seg;
                var clip = RectangleF.Intersect(new RectangleF(x, inner.Y, seg, inner.Height), inner);
                if (clip.Width > 1)
                    using (var lg2 = new LinearGradientBrush(new PointF(clip.X, 0), new PointF(clip.Right, 0),
                               Color.FromArgb(0, Theme.Accent), Theme.Accent))
                        g.FillRectangle(lg2, clip);
            }
            _shimmer = (_shimmer + 9) % 1000;

                        using (var fT = Theme.Ui(8f, FontStyle.Bold))
                Theme.DrawText(g, "插件状态", fT, Theme.Dim, L, Scale(288));

            var names = new[] { ("P1", "限制开关"), ("P2", "下载入口"), ("P3", "原版卡片"), ("P4", "整合包") };
            float cx = L + Scale(58);
            for (int i = 0; i < 4; i++)
            {
                float a = _chipA[i];
                var r = new RectangleF(cx, Scale(280), Scale(152), Scale(38));
                Color tone = Lerp(Color.FromArgb(108, 102, 126), Theme.Ok, a);
                Theme.FillRound(g, r, Scale(9), Color.FromArgb((int)(10 + 16 * a), tone));
                Theme.StrokeRound(g, r, Scale(9), Color.FromArgb((int)(38 + 60 * a), tone));
                using (var b = new SolidBrush(tone))
                {
                    float rr = Scale(3) + Scale(0.8f) * a * (float)Math.Sin(_pulse + i);
                    g.FillEllipse(b, r.X + Scale(13), r.Y + r.Height / 2 - rr, rr * 2, rr * 2);
                }
                using (var f1 = Theme.Ui(8.5f, FontStyle.Bold))
                    Theme.DrawText(g, names[i].Item1, f1, tone, r.X + Scale(27), r.Y + Scale(11));
                using (var f2 = Theme.Ui(8.5f))
                    Theme.DrawText(g, names[i].Item2, f2,
                        Lerp(Color.FromArgb(148, 142, 166), Color.FromArgb(190, 235, 218), a),
                        r.X + Scale(52), r.Y + Scale(11));
                cx += Scale(164);
            }

            int done = _chipTarget.Count(x => x);
            using (var f = Theme.Ui(9f, FontStyle.Bold))
            {
                var t = $"{done} / 4";
                float tw = g.MeasureString(t, f).Width;
                Theme.DrawText(g, t, f, done == 4 ? Theme.Ok : Theme.Muted, R - tw, Scale(282));
            }

                        var log = new RectangleF(L, _logTop, CW, _logH);
            Theme.FillRound(g, log, Scale(11), Theme.Card);
            Theme.StrokeRound(g, log, Scale(11), Theme.Line);
            using (var f = Theme.Ui(9f, FontStyle.Bold))
                Theme.DrawText(g, "运行日志", f, Theme.Dim, L + Scale(16), _logTop + Scale(14));
            Theme.HLineFade(g, L + Scale(16), _logTop + Scale(36), CW - Scale(32), Color.FromArgb(34, 28, 52));

            DrawQqEntry(g, L, h);
            DrawCredit(g, R, h);

            using (var path = Theme.Round(new RectangleF(0.5f, 0.5f, w - 1.5f, h - 1.5f), Scale(10)))
            using (var pen = new Pen(Color.FromArgb(34, 29, 50), 1f))
                g.DrawPath(pen, path);

            base.OnPaint(e);
        }

        private const string QqGroupUrl = "https://qm.qq.com/q/zVmb9qbeJW";

                private void DrawQqEntry(Graphics g, int left, int height)
        {
            const string text = "加入 QQ 群";
            using (var f = Theme.Ui(9f, FontStyle.Bold))
            {
                var size = g.MeasureString(text, f);
                float w = size.Width + Scale(34);
                float hgt = Scale(28);
                float x = left + Scale(4);
                float y = height - hgt - Scale(10);
                _qqRect = new RectangleF(x, y, w, hgt);

                var box = _qqRect;
                Theme.FillRound(g, box, hgt / 2, Color.FromArgb(_qqHot ? 34 : 14, Theme.Accent));
                Theme.StrokeRound(g, box, hgt / 2, Color.FromArgb(_qqHot ? 150 : 70, Theme.Accent));

                                using (var b = new SolidBrush(Color.FromArgb(_qqHot ? 255 : 200, Theme.Accent)))
                    g.FillEllipse(b, box.X + Scale(12), box.Y + box.Height / 2 - Scale(3.2f), Scale(6.4f), Scale(6.4f));

                using (var b = new SolidBrush(_qqHot ? Color.White : Color.FromArgb(198, 176, 232)))
                    g.DrawString(text, f, b, box.X + Scale(24), box.Y + (box.Height - size.Height) / 2f);

                const string sp = "赞助作者";
                var ss = g.MeasureString(sp, f);
                float sw = ss.Width + Scale(34);
                var sbox = new RectangleF(box.Right + Scale(8), y, sw, hgt);
                _sponsorRect = sbox;

                Color gold = Color.FromArgb(255, 196, 84);
                Theme.FillRound(g, sbox, hgt / 2, Color.FromArgb(_sponsorHot ? 40 : 16, gold));
                Theme.StrokeRound(g, sbox, hgt / 2, Color.FromArgb(_sponsorHot ? 165 : 80, gold));
                using (var b2 = new SolidBrush(Color.FromArgb(_sponsorHot ? 255 : 210, gold)))
                    g.FillEllipse(b2, sbox.X + Scale(12), sbox.Y + sbox.Height / 2 - Scale(3.2f), Scale(6.4f), Scale(6.4f));
                using (var b3 = new SolidBrush(_sponsorHot ? Color.White : Color.FromArgb(226, 196, 160)))
                    g.DrawString(sp, f, b3, sbox.X + Scale(24), sbox.Y + (sbox.Height - ss.Height) / 2f);
            }
        }

        private void OpenSponsor()
        {
            try
            {
                Image wx = LoadResourceImage("sponsor_wechat_s.jpg");
                Image ali = LoadResourceImage("sponsor_alipay_s.jpg");
                using (var f = new SponsorForm(wx, ali)) f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Log("FAIL", "打开赞助页失败: " + ex.Message);
            }
        }

        private static Image LoadResourceImage(string name)
        {
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (st == null) throw new InvalidOperationException("缺少资源: " + name);
                using (var tmp = Image.FromStream(st))
                    return new Bitmap(tmp);
            }
        }

                private void DrawCredit(Graphics g, int right, int height)
        {
            const string text = "开发者 by：热带带鱼";
            using (var f = Theme.Ui(10f, FontStyle.Bold))
            {
                var size = g.MeasureString(text, f);
                float pad = Scale(8);
                float x = right - size.Width - pad;
                float y = height - size.Height - Scale(6);
                var box = new RectangleF(x, y, size.Width, size.Height);

                                using (var glow = new SolidBrush(Color.FromArgb(46, Theme.Accent)))
                {
                    float d = Scale(1.2f);
                    g.DrawString(text, f, glow, x - d, y);
                    g.DrawString(text, f, glow, x + d, y);
                    g.DrawString(text, f, glow, x, y - d);
                    g.DrawString(text, f, glow, x, y + d);
                }

                                using (var lg = new LinearGradientBrush(new PointF(box.X, box.Y),
                           new PointF(box.Right, box.Bottom),
                           Color.FromArgb(238, 178, 150, 255), Color.FromArgb(238, 130, 220, 250)))
                    g.DrawString(text, f, lg, x, y);

                                double phase = (DateTime.Now.TimeOfDay.TotalMilliseconds % 2600.0) / 2600.0;
                float band = box.Width * 0.7f;
                float bx = box.X - band + (float)phase * (box.Width + band * 1.6f);
                var clip = g.Clip;
                g.SetClip(new RectangleF(box.X - Scale(2), box.Y - Scale(5), box.Width + Scale(4), box.Height + Scale(10)));
                using (var lg = new LinearGradientBrush(new PointF(bx, 0), new PointF(bx + band, 0),
                           Color.Transparent, Color.White))
                {
                    lg.SetBlendTriangularShape(0.5f, 1f);
                    g.DrawString(text, f, lg, x, y);
                }
                g.Clip = clip;

                                float sx = box.Right + Scale(7);
                float sy = box.Y + box.Height / 2f;
                float r0 = Scale(2.6f) + Scale(1.4f) * _logoGlow;
                using (var star = new SolidBrush(Color.FromArgb((int)(130 + 125 * _logoGlow), Theme.Accent)))
                {
                    var pts = new[]
                    {
                        new PointF(sx, sy - r0), new PointF(sx + r0 * 0.26f, sy - r0 * 0.26f),
                        new PointF(sx + r0, sy), new PointF(sx + r0 * 0.26f, sy + r0 * 0.26f),
                        new PointF(sx, sy + r0), new PointF(sx - r0 * 0.26f, sy + r0 * 0.26f),
                        new PointF(sx - r0, sy), new PointF(sx - r0 * 0.26f, sy - r0 * 0.26f)
                    };
                    g.FillPolygon(star, pts);
                }
            }
        }

        private void EnsureBackground(int w, int h)
        {
            if (_bg != null && _bg.Width == w && _bg.Height == h) return;
            if (_bg != null) { _bg.Dispose(); _bg = null; }
            _bg = new Bitmap(Math.Max(1, w), Math.Max(1, h));
            using (var g = Graphics.FromImage(_bg))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var lg = new LinearGradientBrush(new Point(0, 0), new Point(0, h), Theme.Bg1, Theme.Bg2))
                    g.FillRectangle(lg, 0, 0, w, h);

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(new RectangleF(w - Scale(430), -Scale(250), Scale(580), Scale(580)));
                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb(48, Theme.Accent);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) };
                        g.FillPath(pgb, path);
                    }
                }
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(new RectangleF(-Scale(220), h - Scale(260), Scale(460), Scale(460)));
                    using (var pgb = new PathGradientBrush(path))
                    {
                        pgb.CenterColor = Color.FromArgb(22, Theme.Accent2);
                        pgb.SurroundColors = new[] { Color.FromArgb(0, Theme.Accent2) };
                        g.FillPath(pgb, path);
                    }
                }
            }
        }

        private void DrawStars(Graphics g)
        {
            foreach (var s in _stars)
            {
                float tw = 0.45f + 0.55f * (float)Math.Sin(_pulse * s.Tw + s.Phase);
                int a = (int)(8 + 46 * tw);
                using (var b = new SolidBrush(Color.FromArgb(a, 210, 200, 255)))
                    g.FillEllipse(b, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2);
            }
        }

        private void DrawAutoSwitch(Graphics g, int right)
        {
            float sw = Scale(38), sh = Scale(20);
            float x = right - Scale(216), y = Scale(28);
            _switchRect = new RectangleF(x, y, sw + Scale(60), sh);

            using (var f = Theme.Ui(8.5f))
                Theme.DrawText(g, "自动更新", f, _switchHot ? Theme.Text : Theme.Muted, x, y + Scale(2));

            var track = new RectangleF(x + Scale(60), y, sw, sh);
            Theme.FillRound(g, track, sh / 2, _autoUpdate ? Theme.Accent : Color.FromArgb(46, 42, 60));
            float knobX = _autoUpdate ? track.Right - sh + Scale(2) : track.X + Scale(2);
            using (var b = new SolidBrush(_autoUpdate ? Color.White : Color.FromArgb(150, 146, 168)))
                g.FillEllipse(b, knobX, track.Y + Scale(2), sh - Scale(4), sh - Scale(4));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hot = _switchRect.Contains(e.Location);
            bool qq = _qqRect.Contains(e.Location);
            bool sp = _sponsorRect.Contains(e.Location);
            if (hot != _switchHot || qq != _qqHot || sp != _sponsorHot)
            {
                _switchHot = hot;
                _qqHot = qq;
                _sponsorHot = sp;
                Cursor = (qq || sp) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_sponsorRect.Contains(e.Location)) { OpenSponsor(); return; }

            if (_qqRect.Contains(e.Location))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(QqGroupUrl) { UseShellExecute = true });
                                    }
                catch (Exception ex)
                {
                    Log("FAIL", "打开 QQ 群失败: " + ex.Message);
                    Log("INFO", "手动访问：" + QqGroupUrl);
                }
                return;
            }

            if (_switchRect.Contains(e.Location))
            {
                _autoUpdate = !_autoUpdate;
                Settings.AutoGuard = _autoUpdate;
                Log("INFO", _autoUpdate
                    ? "自动更新已开启：检测到 PCL2 换了新版本会自动重新打补丁。"
                    : "自动更新已关闭：PCL2 更新后需要手动点一次「安装插件」。");
                Invalidate();
                return;
            }
            base.OnMouseDown(e);
        }

        private const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDBLCLK = 0xA3;
        private const int HTCLIENT = 1, HTCAPTION = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

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

        protected override void WndProc(ref Message m)
        {
                        if (m.Msg == WM_NCLBUTTONDBLCLK && m.WParam.ToInt32() == HTCAPTION) return;

            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && m.Result.ToInt32() == HTCLIENT)
            {
                var p = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));

                                var hit = _switchRect;
                hit.Inflate(Scale(10), Scale(10));
                if (hit.Contains(p)) return;

                if (p.Y < Scale(106)) m.Result = (IntPtr)HTCAPTION;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

                private void ResetTarget()
        {
            _currentPath = "";
            _pcl = null;
            _txtPath.Text = "";
            if (_lblPlaceholder != null) _lblPlaceholder.Visible = true;
            _lblVer.Text = "";
            _chipTarget = new bool[4];
            SetStatus("未选择", Theme.Muted);
            _btnInstall.Text = "安装插件";
            _btnInstall.SubText = "INSTALL PLUGIN";
            _btnUninstall.Primary = false;
            SetButtons(false);
            _btnOpen.Enabled = false;
            _btnLaunch.Enabled = false;
            _btnGet.Enabled = true;
            Log("INFO", "请点「自动检测」扫描 PCL2，或点「浏览」手动选择 Plain Craft Launcher 2.exe。");
            Invalidate();
        }

        private void BeginDetect()
        {
            _scanning = true;
            SetStatus("检测中", Theme.Muted);
            SetButtons(false);
            Log("INFO", "正在扫描 PCL2（桌面 / 下载 / PCL 目录，自动选版本号最新的一份）…");
            Invalidate();

            var th = new Thread(() =>
            {
                List<Candidate> list = null;
                string err = null;
                try { list = Finder.Scan(); }
                catch (Exception ex) { err = ex.Message; }

                BeginInvoke((Action)(() =>
                {
                    _scanning = false;
                    if (err != null) { Log("FAIL", "检测失败: " + err); SetStatus("检测失败", Theme.Err); Invalidate(); return; }
                    if (list == null || list.Count == 0)
                    {
                        SetStatus("未找到", Theme.Err);
                        Log("WARN", "没有找到 PCL2 —— 点「浏览」选择 Plain Craft Launcher 2.exe，或把 exe 拖进窗口。");
                        Invalidate();
                        return;
                    }

                    Log("INFO", $"扫描到 {list.Count} 份 PCL2：");
                    foreach (var c in list)
                        Log("INFO", $"   code {c.Code} · {c.Display} · {c.FileName}");

                    var best = list[0];
                    if (string.IsNullOrEmpty(best.Display)) best.Display = "已加壳";
                    Log("OK", list.Count > 1
                        ? $"共 {list.Count} 份，选用版本最新的：{best.Display}（code {best.Code}）"
                        : $"已定位：{best.Display}（code {best.Code}）");
                    SetPath(best.Path, true, keepLog: true);
                }));
            }) { IsBackground = true, Name = "polaris-detect" };
            th.Start();
        }

        private static readonly HashSet<string> SkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "node_modules", ".git", ".vs", "obj", "bin", "$RECYCLE.BIN", "System Volume Information" };

        private static IEnumerable<string> Walk(string root, int depth)
        {
            var queue = new Queue<Tuple<string, int>>();
            queue.Enqueue(Tuple.Create(root, 0));
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                yield return cur.Item1;
                if (cur.Item2 >= depth) continue;
                string[] subs;
                try { subs = Directory.GetDirectories(cur.Item1); } catch { continue; }
                foreach (var s in subs)
                    if (!SkipDirs.Contains(Path.GetFileName(s))) queue.Enqueue(Tuple.Create(s, cur.Item2 + 1));
            }
        }

        private void Browse()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "选择 Plain Craft Launcher 2.exe",
                Filter = "PCL2 主程序|Plain Craft Launcher 2.exe|可执行文件|*.exe",
                CheckFileExists = true
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) SetPath(dlg.FileName, true);
            }
        }

                private void Launch()
        {
            if (string.IsNullOrEmpty(_currentPath) || !File.Exists(_currentPath))
            {
                Log("WARN", "还没有选择 PCL2，先点「自动检测 PCL2」或「浏览」。");
                return;
            }

            try
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(_currentPath);
                var running = System.Diagnostics.Process.GetProcessesByName(name).FirstOrDefault();
                if (running != null)
                {
                    Log("INFO", $"PCL 已经在运行（PID {running.Id}），不再重复启动。");
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo(_currentPath)
                {
                    WorkingDirectory = System.IO.Path.GetDirectoryName(_currentPath),
                    UseShellExecute = true
                };
                var p = System.Diagnostics.Process.Start(psi);
                Log("OK", "已启动 PCL2" + (p != null ? $"（PID {p.Id}）" : "") + "。");
            }
            catch (Exception ex)
            {
                Log("FAIL", "启动失败: " + ex.Message);
            }
        }

                private void OpenReleasePage()
        {
            const string url = "https://github.com/Meloong-Git/PCL/releases/latest";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                Log("OK", "已打开官方发布页：" + url);
            }
            catch (Exception ex)
            {
                Log("FAIL", "打开浏览器失败: " + ex.Message);
                Log("INFO", "手动访问：" + url);
            }
        }

        private void OpenFolder()
        {
            try
            {
                var dir = File.Exists(_currentPath) ? Path.GetDirectoryName(_currentPath)
                                                    : Path.GetDirectoryName(Application.ExecutablePath);
                if (!string.IsNullOrEmpty(dir)) System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex) { Log("FAIL", "打开目录失败: " + ex.Message); }
        }

        private void SetPath(string path, bool analyze, bool keepLog = false)
        {
            if (string.IsNullOrEmpty(path)) return;
            _currentPath = path;
            _txtPath.Text = path;
            _lblPlaceholder.Visible = false;
            _btnOpen.Enabled = true;
            _btnLaunch.Enabled = true;
            SaveLast(path);
            Watch(path);
            if (analyze) AnalyzeAsync(keepLog);
        }

        private void Watch(string path)
        {
            try
            {
                if (_watch != null) { _watch.Dispose(); _watch = null; }
                _watch = new ExeWatcher(path);
                _watch.Settled += OnExeSettled;
            }
            catch { }
        }

        private void OnExeSettled(string path)
        {
            if (_busy || _scanning || DateTime.Now < _suppressUntil) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    Log("INFO", "检测到 PCL2 文件变化（可能是自动更新），正在重新检测…");
                    AnalyzeAsync(keepLog: true);
                }));
            }
            catch { }
        }

        private void AnalyzeAsync(bool keepLog = false)
        {
            var path = _currentPath;
            if (!_busy) { SetStatus("检测中", Theme.Muted); _lblVer.Text = ""; }
            _pcl = null;
            _record = GuardStore.Get(path);
            SetButtons(false);
            Invalidate();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                PclState st = null;
                string err = null;
                try { st = Patcher.Analyze(path); }
                catch (Exception ex) { err = ex.Message; }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (!string.Equals(_currentPath, path, StringComparison.Ordinal)) return;
                        if (err != null)
                        {
                            SetStatus("无法识别", Theme.Err);
                            Log("INFO", "目标: " + path);
                            Log("FAIL", err);
                            SetButtons(false);
                            Invalidate();
                            return;
                        }
                        ApplyPcl(st, keepLog);
                    }));
                }
                catch { }
            });
        }

        private void ApplyPcl(PclState st, bool keepLog)
        {
            _pcl = st;
            if (!keepLog) _rtb.Clear();
            if (st.IsPacked)
            {
                SetStatus("已加壳", Theme.Warn);
                Log("WARN", "这个 exe 不是明文 .NET 程序集，无法做 IL 补丁：" + st.PackNote);
                Log("INFO", "官方 PCL2 是 5.98 MB 的明文程序集；加壳版本需要用未加壳的原版替换后再修。");
                if (st.HasBackup) Log("INFO", "此目录有官方原文件备份：" + Path.GetFileName(st.BackupPath) + "（可用「卸载还原」恢复回去）");
                SetButtons(false);
                _btnUninstall.Enabled = st.HasBackup;
                _btnInstall.Text = "无法修补";
                _btnInstall.SubText = "PACKED";
                _btnInstall.Invalidate();
                Invalidate();
                return;
            }
            Log("INFO", "目标: " + st.Path);
            Log("INFO", $"版本 {st.VersionDisplay} · VersionCode {st.VersionCode} · asm {st.AsmVersion}");
            Log("INFO", "强名: " + st.StrongName + (st.StrongName == "(none)" ? "（无强名，可直接修补）" : ""));
            if (st.HasBackup) Log("INFO", "备份: " + Path.GetFileName(st.BackupPath));

            _lblVer.Text = $"   {st.VersionDisplay} ({st.VersionCode})";
            _chipTarget = new[] { st.P1, st.P2, st.P3, st.P4 };

            if (!st.IsSupported)
            {
                if (st.IsCorePatched)
                {
                    SetStatus("无需修复", Theme.Ok);
                    Log("OK", $"该版本（{st.Variant}）没有下载限制：未发现占位下载任务 / 限制文案，不需要打补丁。");
                    _btnInstall.Text = "无需修复";
                    _btnInstall.SubText = "NOT RESTRICTED";
                    _btnInstall.Enabled = false;
                    _btnUninstall.Enabled = st.HasBackup && !_busy;
                    _btnOpen.Enabled = true;
                    _btnLaunch.Enabled = true;
                    _btnInstall.Invalidate();
                    SyncPillLocation();
                    Invalidate();
                    return;
                }

                SetStatus("版本不支持", Theme.Err);
                Log("WARN", "未找到下载限制特征：可能不是受限版本，或结构已变化。");
                SetButtons(false);
                _btnUninstall.Enabled = st.HasBackup;
                Invalidate();
                return;
            }

            if (st.IsPatched)
            {
                SetStatus("已安装", Theme.Ok);
                Log("OK", "插件已生效：4 个补丁点全部就位。");
            }
            else
            {
                SetStatus("未安装", Theme.Warn);
                Log("WARN", $"尚未安装：P1={Flag(st.P1)} P2={Flag(st.P2)} P3={Flag(st.P3)} P4={Flag(st.P4)}");
            }

            _btnInstall.Text = st.IsPatched ? "已安装" : "安装插件";
            _btnInstall.SubText = st.IsPatched ? "ALREADY INSTALLED" : "INSTALL PLUGIN";
            _btnUninstall.Primary = st.IsPatched;
            _btnInstall.Enabled = !st.IsPatched && !_busy;
            _btnUninstall.Enabled = st.HasBackup && !_busy;
            _btnOpen.Enabled = true;
            _btnLaunch.Enabled = true;
            _btnInstall.Invalidate();
            _btnUninstall.Invalidate();
            SyncPillLocation();
            Invalidate();

                        if (_autoUpdate && !st.IsPatched && st.IsSupported && !_autoRunning &&
                _record != null && _record.Patched)
            {
                _autoRunning = true;
                Log("OK", $"自动更新：检测到 PCL2 新版本（{st.VersionDisplay}），插件被覆盖，正在重新安装…");
                BeginInvoke((Action)(() => Run(true, silent: true)));
            }
        }

        private static string Flag(bool b) => b ? "已恢复" : "受限";

        private void SetStatus(string text, Color tone)
        {
            _pill.Text = text;
            _pillTarget = tone;
            _pill.Width = Scale(64 + text.Length * 13);
            SyncPillLocation();
            _pill.Invalidate();
        }

        private void SyncPillLocation()
        {
            _lblVer.Location = new Point(_pill.Right + Scale(16), Scale(238));
        }

        private void SetButtons(bool enabled)
        {
            _btnInstall.Enabled = enabled;
            _btnUninstall.Enabled = enabled;
            _btnBrowse.Enabled = true;
            _btnDetect.Enabled = true;
            _btnClear.Enabled = true;
            _txtPath.Enabled = true;
        }

        private void Run(bool install, bool silent = false)
        {
            if (_busy || string.IsNullOrEmpty(_currentPath)) return;
            if (!File.Exists(_currentPath)) { Log("FAIL", "文件不存在: " + _currentPath); return; }

            _busy = true;
            SetButtons(false);
            _btnInstall.Busy = install;
            Log("INFO", install ? "=== 开始安装 ===" : "=== 开始卸载 ===");

            var path = _currentPath;
            var th = new Thread(() =>
            {
                bool ok = false;
                try
                {
                    if (install)
                    {
                        var before = Patcher.Sha256(path);
                        var refBytes = ReadEmbedded("pcl214.exe");
                        aLog("INFO", "参照版本已就绪，正在写入补丁…");
                        var lines = Patcher.Install(path, refBytes, m => aLog("INFO", StripPrefix(m)));
                        foreach (var l in lines) aLog(l.Level, $"{l.Id}  {l.Text}");
                        aLog("OK", "安装完成 — 原文件已备份，4 个补丁点写入成功。");
                        ok = true;

                        GuardStore.Set(new TargetRecord
                        {
                            Path = path,
                            Sha = Patcher.Sha256(path),
                            SourceSha = before,
                            Version = _pcl?.VersionDisplay ?? "",
                            Patched = true,
                            Kind = "pcl2",
                            When = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    else
                    {
                        Patcher.Uninstall(path, m => aLog("INFO", StripPrefix(m)));
                        aLog("OK", "卸载完成 — 已还原为官方原始字节。");
                        ok = true;
                        var rec = GuardStore.Get(path);
                        if (rec != null) { rec.Patched = false; rec.When = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); GuardStore.Set(rec); }
                    }
                }
                catch (Exception ex) { aLog("FAIL", ex.Message); }
                finally
                {
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            _busy = false;
                            _autoRunning = false;
                            _btnInstall.Busy = false;
                            _suppressUntil = DateTime.Now.AddSeconds(8);
                            AnalyzeAsync(keepLog: true);
                            if (ok && install && !silent)
                                MessageBox.Show(this,
                                    "插件已安装完成。\n\n重启 PCL 即可在「下载」页看到原版 / Mojang 官方源。",
                                    "Polaris修复插件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                    catch { }
                }
            }) { IsBackground = true, Name = "polaris-install" };
            th.Start();
        }

        private static string StripPrefix(string m)
        {
            foreach (var p in new[] { "INFO ", "OK ", "WARN ", "FAIL ", "SKIP " })
                if (m.StartsWith(p, StringComparison.Ordinal)) return m.Substring(p.Length);
            return m;
        }

        private void aLog(string level, string msg)
        {
            try { BeginInvoke((Action)(() => Log(level, msg))); } catch { }
        }

        private void Log(string level, string msg)
        {
            if (_rtb == null) return;
            if (_rtb.InvokeRequired) { try { _rtb.BeginInvoke((Action)(() => Log(level, msg))); } catch { } return; }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
                File.AppendAllText(LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + msg + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }

            Color c;
            string tag;
            switch (level)
            {
                case "OK": c = Theme.Ok; tag = "  OK  "; break;
                case "WARN": c = Theme.Warn; tag = " WARN "; break;
                case "FAIL": c = Theme.Err; tag = " FAIL "; break;
                case "SKIP": c = Theme.Muted; tag = " SKIP "; break;
                default: c = Color.FromArgb(148, 132, 212); tag = " INFO "; break;
            }

            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionLength = 0;
            _rtb.SelectionColor = Theme.Dim;
            _rtb.AppendText(DateTime.Now.ToString("HH:mm:ss") + " ");
            _rtb.SelectionColor = c;
            _rtb.AppendText(tag + " ");
            _rtb.SelectionColor = level == "FAIL" ? Theme.Err : Color.FromArgb(208, 202, 226);
            _rtb.AppendText(msg + Environment.NewLine);
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.ScrollToCaret();
        }

        public static byte[] ReadEmbedded(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("内嵌资源缺失: " + name);
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        private static string LogFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Polaris", "polaris.log");

        private static string StateFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Polaris", "last.txt");

        private static string LoadLast()
        {
            try { return File.Exists(StateFile) ? File.ReadAllText(StateFile, Encoding.UTF8).Trim() : ""; }
            catch { return ""; }
        }

        private static void SaveLast(string p)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile));
                File.WriteAllText(StateFile, p, Encoding.UTF8);
            }
            catch { }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                e.Effect = DragDropEffects.Copy;
            base.OnDragEnter(e);
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var exe = files?.FirstOrDefault(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (exe != null) SetPath(exe, true);
            base.OnDragDrop(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_watch != null) _watch.Dispose(); } catch { }
                try { if (_anim != null) { _anim.Stop(); _anim.Dispose(); } } catch { }
                try { if (_bg != null) _bg.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
