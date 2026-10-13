using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SerialPortTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ================= Liquid Glass 配色（iOS 系统色） =================
    internal static class Theme
    {
        // 扁平深色基底（简约风格）
        public static readonly Color BG = Color.FromArgb(16, 18, 22);           // 窗口底色
        public static readonly Color GLASS_IN = Color.FromArgb(0, 255, 255, 255);   // 已弃用（保留兼容）
        public static readonly Color GLASS_IN2 = Color.FromArgb(0, 255, 255, 255);  // 已弃用（保留兼容）
        public static readonly Color EDGE_HI = Color.FromArgb(0, 255, 255, 255);    // 已弃用
        public static readonly Color EDGE_LO = Color.FromArgb(0, 255, 255, 255);    // 已弃用
        public static readonly Color WELL = Color.FromArgb(12, 14, 18);        // 编辑区井底

        // 文字
        public static readonly Color TEXT = Color.FromArgb(230, 234, 240);
        public static readonly Color MUTED = Color.FromArgb(148, 156, 168);
        public static readonly Color DIM = Color.FromArgb(108, 116, 128);

        // 强调色（单一蓝色点缀）
        public static readonly Color BLUE = Color.FromArgb(70, 128, 255);
        public static readonly Color BLUE_HI = Color.FromArgb(112, 158, 255);
        public static readonly Color GREEN = Color.FromArgb(52, 199, 89);
        public static readonly Color RED = Color.FromArgb(255, 82, 82);
        public static readonly Color RED_HI = Color.FromArgb(255, 120, 120);
        public static readonly Color ORANGE = Color.FromArgb(255, 169, 32);

        // 扁平卡片与控件
        public static readonly Color CARD = Color.FromArgb(24, 27, 33);
        public static readonly Color BORDER = Color.FromArgb(45, 50, 59);
        public static readonly Color INPUT = Color.FromArgb(12, 14, 18);
        public static readonly Color CTRL = Color.FromArgb(36, 41, 50);
        public static readonly Color CTRL_H = Color.FromArgb(47, 53, 64);
        public static readonly Color ACCENT = BLUE;
        public static readonly Color ACCENT_H = BLUE_HI;
        public static readonly Color NEUTRAL = Color.FromArgb(36, 41, 50);
        public static readonly Color NEUTRAL_H = Color.FromArgb(47, 53, 64);
        public static readonly Color DANGER = RED;
        public static readonly Color DANGER_H = RED_HI;
        public static readonly Color THUMB = Color.FromArgb(64, 71, 82);
        public static readonly Color THUMB_H = Color.FromArgb(102, 110, 124);

        public static readonly Font UI = new Font("Segoe UI", 9F);
        public static readonly Font UI_BOLD = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font TITLE = new Font("Segoe UI", 12F, FontStyle.Bold);
        public static readonly Font MONO = new Font("Consolas", 10.5F);

        private static Icon appIcon;
        public static Icon AppIcon
        {
            get
            {
                if (appIcon == null)
                {
                    Bitmap bmp = new Bitmap(32, 32);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (GraphicsPath p = SerialPortTool.UI.RoundRectF(1f, 1f, 30f, 30f, 9f))
                        {
                            using (LinearGradientBrush lb = new LinearGradientBrush(
                                new Rectangle(0, 0, 32, 32), BLUE_HI, Color.FromArgb(30, 110, 240), 60f))
                                g.FillPath(lb, p);
                        }
                        using (Pen pen = new Pen(Color.White, 2.6f))
                        {
                            pen.StartCap = LineCap.Round;
                            pen.EndCap = LineCap.Round;
                            pen.LineJoin = LineJoin.Round;
                            g.DrawLine(pen, 7, 21, 11, 21);
                            g.DrawLine(pen, 11, 21, 11, 11);
                            g.DrawLine(pen, 11, 11, 17, 11);
                            g.DrawLine(pen, 17, 11, 17, 21);
                            g.DrawLine(pen, 17, 21, 23, 21);
                            g.DrawLine(pen, 23, 21, 23, 14);
                        }
                    }
                    appIcon = Icon.FromHandle(bmp.GetHicon());
                }
                return appIcon;
            }
        }
    }

    // ================= 绘制工具 =================
    internal static class UI
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public const int EM_GETLINECOUNT = 0x00BA;
        public const int EM_LINESCROLL = 0x00B6;
        public const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        public static void Dark(Control c)
        {
            try { int v = 1; DwmSetWindowAttribute(c.Handle, 20, ref v, 4); } catch { }
        }

        public static void HiQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static GraphicsPath RoundRectF(float x, float y, float w, float h, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (w <= 0f || h <= 0f) { p.AddRectangle(new RectangleF(x, y, Math.Max(0f, w), Math.Max(0f, h))); return p; }
            float d = radius * 2f;
            if (d <= 0f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
            if (d > w) d = w;
            if (d > h) d = h;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static RectangleF StrokeRect(int w, int h, float strokeWidth)
        {
            float inset = strokeWidth / 2f;
            float ww = w - strokeWidth;
            float hh = h - strokeWidth;
            if (ww < 0f) ww = 0f;
            if (hh < 0f) hh = 0f;
            return new RectangleF(inset, inset, ww, hh);
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Darken(Color c, float f)
        {
            return Color.FromArgb(c.A, (int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));
        }

        public static Color Brighten(Color c, float f)
        {
            return Color.FromArgb(c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * f)),
                Math.Min(255, (int)(c.G + (255 - c.G) * f)),
                Math.Min(255, (int)(c.B + (255 - c.B) * f)));
        }

        /// 柔和光斑（径向渐变）
        public static void DrawBlob(Graphics g, float cx, float cy, float r, Color color)
        {
            if (r <= 1f) return;
            RectangleF rect = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(rect);
                using (PathGradientBrush pgb = new PathGradientBrush(p))
                {
                    pgb.CenterPoint = new PointF(cx, cy);
                    pgb.CenterColor = color;
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, color.R, color.G, color.B) };
                    g.FillPath(pgb, p);
                }
            }
        }

        /// 玻璃顶部内高光：从上往下递减的白色渐变
        public static void DrawInnerTopLight(Graphics g, GraphicsPath clip, RectangleF r, int lightHeight, byte topAlpha)
        {
            if (lightHeight <= 0 || r.Height <= 0) return;
            int lh = Math.Min(lightHeight, (int)r.Height);
            GraphicsState st = g.Save();
            g.SetClip(clip, CombineMode.Intersect);
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(r.X, r.Y, r.Width, lh),
                Color.FromArgb(topAlpha, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255), 90f))
            {
                g.FillRectangle(b, r.X, r.Y, r.Width, lh);
            }
            g.Restore(st);
        }

        /// 玻璃边缘渐变描边（上亮下暗）
        public static void DrawGlassEdge(Graphics g, GraphicsPath path, RectangleF r, byte topAlpha, byte bottomAlpha, float width)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(r.X, r.Y, r.Width, Math.Max(1f, r.Height)),
                Color.FromArgb(topAlpha, 255, 255, 255),
                Color.FromArgb(bottomAlpha, 255, 255, 255), 90f))
            using (Pen pen = new Pen(b, width))
            {
                g.DrawPath(pen, path);
            }
        }

        /// 快速 box blur（原地，32bppArgb）
        public static void BoxBlur(Bitmap bmp, int radius)
        {
            if (radius < 1 || bmp.Width < 2 || bmp.Height < 2) return;
            int w = bmp.Width, h = bmp.Height;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int bytes = stride * h;
                byte[] src = new byte[bytes];
                byte[] tmp = new byte[bytes];
                Marshal.Copy(data.Scan0, src, 0, bytes);
                int span = radius * 2 + 1;

                // 水平
                for (int y = 0; y < h; y++)
                {
                    int ro = y * stride;
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = 0;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int x = k < 0 ? 0 : (k >= w ? w - 1 : k);
                            sum += src[ro + x * 4 + c];
                        }
                        for (int x = 0; x < w; x++)
                        {
                            tmp[ro + x * 4 + c] = (byte)(sum / span);
                            int xa = x + radius + 1; if (xa > w - 1) xa = w - 1;
                            int xs = x - radius; if (xs < 0) xs = 0;
                            sum += src[ro + xa * 4 + c] - src[ro + xs * 4 + c];
                        }
                    }
                    // alpha 直通
                    for (int x = 0; x < w; x++) tmp[ro + x * 4 + 3] = src[ro + x * 4 + 3];
                }
                // 垂直
                for (int x = 0; x < w; x++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = 0;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int y = k < 0 ? 0 : (k >= h ? h - 1 : k);
                            sum += tmp[y * stride + x * 4 + c];
                        }
                        for (int y = 0; y < h; y++)
                        {
                            src[y * stride + x * 4 + c] = (byte)(sum / span);
                            int ya = y + radius + 1; if (ya > h - 1) ya = h - 1;
                            int ys = y - radius; if (ys < 0) ys = 0;
                            sum += tmp[ya * stride + x * 4 + c] - tmp[ys * stride + x * 4 + c];
                        }
                    }
                    for (int y = 0; y < h; y++) src[y * stride + x * 4 + 3] = tmp[y * stride + x * 4 + 3];
                }
                Marshal.Copy(src, 0, data.Scan0, bytes);
            }
            finally { bmp.UnlockBits(data); }
        }

        public static void AnimateIn(Form f, int ms, int slide)
        {
            f.Opacity = 0;
            bool started = false;
            int origTop = 0;
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 15;
            double elapsed = 0;
            t.Tick += (s, e) =>
            {
                elapsed += 15;
                double p = elapsed / ms;
                if (p >= 1) { p = 1; t.Stop(); t.Dispose(); }
                double ease = 1 - Math.Pow(1 - p, 3);
                try
                {
                    f.Opacity = ease;
                    if (slide > 0) f.Top = origTop + (int)(slide * (1 - ease));
                }
                catch { t.Stop(); t.Dispose(); }
            };
            f.Shown += (s, e) =>
            {
                if (started) return;
                started = true;
                origTop = f.Top;
                if (slide > 0) f.Top = origTop + slide;
                t.Start();
            };
        }

        public static int LineHeight(Font f)
        {
            return TextRenderer.MeasureText("Ay", f).Height;
        }
    }

    // ================= 玻璃背景（模糊缓存） =================
    internal class GlassBackdrop
    {
        public static readonly GlassBackdrop Instance = new GlassBackdrop();

        private Bitmap raw;
        private Bitmap blur;      // 1/2 尺寸的模糊图
        private Size size;
        public Point Origin;      // 窗口客户区左上角的屏幕坐标
        public Size ViewSize;     // 当前客户区尺寸（resize 期间用于比例映射）

        public Bitmap Blur { get { return blur; } }
        public Color Fallback = Theme.BG;

        public void Render(Size clientSize)
        {
            if (clientSize.Width < 8 || clientSize.Height < 8) return;
            if (size == clientSize && raw != null) { ViewSize = clientSize; return; }
            size = clientSize;
            ViewSize = clientSize;

            Bitmap newRaw = new Bitmap(clientSize.Width, clientSize.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(newRaw))
            {
                // 扁平深色：纯色背景
                using (SolidBrush b = new SolidBrush(Theme.BG))
                    g.FillRectangle(b, 0, 0, clientSize.Width, clientSize.Height);
            }

            lock (this)
            {
                raw = newRaw;
                blur = null;   // 不再使用模糊折射
            }
        }

        public Bitmap Raw { get { lock (this) { return raw; } } }
        private Bitmap BlurRef { get { lock (this) { return blur; } } }

        /// 已移除动态折射：仅填充实色背景
        public void PaintBlurred(Graphics g, Rectangle destRect, int ox, int oy)
        {
            if (destRect.Width <= 0 || destRect.Height <= 0) return;
            using (SolidBrush b = new SolidBrush(Fallback)) g.FillRectangle(b, destRect);
        }
    }

    // ================= 玻璃层基类 =================
    internal abstract class GlassBase : Panel
    {
        public int Radius = 16;
        public int ShadowMargin = 11;
        public bool DrawShadow = true;
        public bool DrawEdge = true;

        protected GlassBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        /// 玻璃区域（已扣除阴影留白）
        protected RectangleF GlassRect()
        {
            int m = ShadowMargin;
            return new RectangleF(m + 0.5f, m + 0.5f,
                Math.Max(1f, Width - m * 2 - 1f), Math.Max(1f, Height - m * 2 - 1f));
        }

        protected void PaintShadow(Graphics g, RectangleF r)
        {
            // 简约风格：不再绘制多层投影
        }

        protected void PaintGlass(Graphics g, RectangleF r, Color tint, int innerLightH, byte lightAlpha,
                                  byte edgeTop, byte edgeBottom)
        {
            // 简约风格：纯色卡片 + 细边框，去除玻璃折射/高光/渐变描边
            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
            {
                using (SolidBrush b = new SolidBrush(Theme.CARD)) g.FillPath(b, path);
                if (DrawEdge)
                {
                    using (GraphicsPath p2 = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
                    using (Pen pen = new Pen(Theme.BORDER, 1f)) g.DrawPath(pen, p2);
                }
            }
        }
    }

    // ================= 玻璃卡片 =================
    internal class GlassCard : GlassBase
    {
        public Color Tint = Theme.CARD;
        public int InnerLightH = 40;
        public byte LightAlpha = 13;
        public byte EdgeTop = 26;
        public byte EdgeBottom = 5;

        public GlassCard()
        {
            Radius = 10;
            ShadowMargin = 11;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            RectangleF r = GlassRect();
            PaintShadow(g, r);
            PaintGlass(g, r, Tint, InnerLightH, LightAlpha, EdgeTop, EdgeBottom);
        }
    }

    // ================= 玻璃按钮 =================
    internal class GlassButton : Control, IButtonControl
    {
        public enum Style { Neutral, Accent, Danger, Success }

        public Style Kind = Style.Neutral;
        public int Radius = 8;
        public bool Bold = false;
        public bool UseBackdrop = true;

        private float glow = 0f;         // 悬停/按下光晕 0..1
        private bool hover = false;
        private bool pressed = false;
        private readonly System.Windows.Forms.Timer anim;

        public GlassButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.StandardDoubleClick |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = Theme.UI;
            Cursor = Cursors.Hand;
            anim = new System.Windows.Forms.Timer();
            anim.Interval = 15;
            anim.Tick += (s, e) =>
            {
                float target = pressed ? 1f : (hover ? 0.55f : 0f);
                glow += (target - glow) * 0.3f;
                if (Math.Abs(target - glow) < 0.01f) { glow = target; anim.Stop(); }
                Invalidate();
            };
        }

        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) { }
        public void PerformClick() { OnClick(EventArgs.Empty); }

        private void Kick() { if (!anim.Enabled) anim.Start(); }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Kick(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Kick(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (Enabled) { pressed = true; Kick(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Kick(); }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled) return;
            base.OnClick(e);
            if (DialogResult != DialogResult.None)
            {
                Form f = FindForm();
                if (f != null) f.DialogResult = DialogResult;
            }
        }

        private Color BaseTint()
        {
            switch (Kind)
            {
                case Style.Accent: return Theme.BLUE;
                case Style.Danger: return Theme.RED;
                case Style.Success: return Theme.GREEN;
                default: return Theme.CTRL;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color tint = BaseTint();
            if (!Enabled)
            {
                tint = Color.FromArgb(64, 72, 82);
            }
            else if (glow > 0.01f)
            {
                // 简约风格：悬停微亮、按下微暗（纯色，无光晕）
                if (pressed) tint = UI.Lerp(tint, UI.Darken(tint, 0.22f), glow);
                else tint = UI.Lerp(tint, UI.Brighten(tint, 0.16f), glow);
            }

            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
            {
                using (SolidBrush b = new SolidBrush(tint)) g.FillPath(b, path);
                // 细边框（1px）
                Color edge = Kind == Style.Neutral ? Theme.BORDER
                    : Color.FromArgb(Math.Min(255, tint.R + 45), Math.Min(255, tint.G + 45), Math.Min(255, tint.B + 45));
                using (GraphicsPath p2 = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
                using (Pen pen = new Pen(edge, 1f)) g.DrawPath(pen, p2);
            }

            Color fg = Enabled ? ForeColor : Color.FromArgb(120, 130, 145);
            TextRenderer.DrawText(g, Text, Bold ? Theme.UI_BOLD : Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // ================= 玻璃复选框 =================
    internal class GlassCheck : Control
    {
        private bool checkedState;
        private float t = 0f;
        private bool hover = false;
        private readonly System.Windows.Forms.Timer anim;

        public event EventHandler CheckedChanged;

        public GlassCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.TEXT;
            Font = Theme.UI;
            Cursor = Cursors.Hand;
            Height = 26;
            anim = new System.Windows.Forms.Timer();
            anim.Interval = 15;
            anim.Tick += (s, e) =>
            {
                float target = checkedState ? 1f : 0f;
                t += (target - t) * 0.32f;
                if (Math.Abs(target - t) < 0.015f) { t = target; anim.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return checkedState; }
            set
            {
                if (checkedState == value) return;
                checkedState = value;
                if (!anim.Enabled) anim.Start();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void FitWidth()
        {
            Width = 32 + TextRenderer.MeasureText(Text, Font).Width + 4;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float box = 18f;
            float y = (Height - box) / 2f;
            RectangleF r = new RectangleF(2f, y + 0.5f, box, box);

            // 简约风格：纯色方块 + 细边框，选中时填充蓝色
            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, 4.5f))
            {
                Color fill = UI.Lerp(Theme.INPUT, Theme.BLUE, t);
                if (t < 0.02f && hover) fill = Theme.CTRL;
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                Color edge = t > 0.5f ? UI.Lerp(Theme.BLUE, Theme.BLUE_HI, 0.3f) : (hover ? Theme.MUTED : Theme.BORDER);
                using (Pen pen = new Pen(edge, 1.2f)) g.DrawPath(pen, path);
            }

            if (t > 0.02f)
            {
                using (Pen pen = new Pen(Color.FromArgb((int)(255 * Math.Min(1f, t * 1.4f)), 255, 255, 255), 2.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    float x0 = r.X + 3.4f, y0 = r.Y + r.Height / 2f;
                    g.DrawLine(pen, x0, y0, x0 + 3.0f, y0 + 3.4f);
                    g.DrawLine(pen, x0 + 3.0f, y0 + 3.4f, x0 + 8.8f, y0 - 3.4f);
                }
            }

            TextRenderer.DrawText(g, Text, Font,
                new Rectangle((int)box + 8, 0, Width - (int)box - 8, Height),
                ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    // ================= 玻璃下拉框 =================
    internal class GlassCombo : Control
    {
        public readonly List<string> Items = new List<string>();
        public bool Editable = false;
        private int selIndex = -1;
        private TextBox inner;
        private bool hover = false;
        private bool focused = false;
        private ToolStripDropDown drop;

        public event EventHandler SelectedIndexChanged;
        public event EventHandler TextChangedX;

        public GlassCombo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.TEXT;
            Font = Theme.UI;
            Height = 30;
            Cursor = Cursors.Hand;
        }

        public new string Text
        {
            get { return inner != null ? inner.Text : (selIndex >= 0 && selIndex < Items.Count ? Items[selIndex] : ""); }
            set
            {
                if (inner != null) inner.Text = value;
                else { selIndex = Items.IndexOf(value); Invalidate(); }
            }
        }

        public int SelectedIndex
        {
            get { return selIndex; }
            set
            {
                if (selIndex == value) return;
                selIndex = value;
                if (inner != null && value >= 0 && value < Items.Count) inner.Text = Items[value];
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public void EnsureInner()
        {
            if (!Editable || inner != null) return;
            inner = new TextBox();
            inner.BorderStyle = BorderStyle.None;
            inner.BackColor = Theme.INPUT;
            inner.ForeColor = Theme.TEXT;
            inner.Font = Font;
            inner.Location = new Point(10, 7);
            inner.TextChanged += (s, e) => { if (TextChangedX != null) TextChangedX(this, EventArgs.Empty); };
            inner.Enter += (s, e) => { focused = true; Invalidate(); };
            inner.Leave += (s, e) => { focused = false; Invalidate(); };
            Controls.Add(inner);
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (inner != null) inner.Width = Math.Max(8, Width - 36);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (inner != null && inner.Bounds.Contains(e.Location)) return;
            ShowDrop();
        }

        private void ShowDrop()
        {
            if (Items.Count == 0) return;
            ListBox lb = new ListBox();
            lb.BorderStyle = BorderStyle.None;
            lb.BackColor = Theme.CARD;
            lb.ForeColor = Theme.TEXT;
            lb.Font = Font;
            lb.ItemHeight = 24;
            lb.IntegralHeight = true;
            lb.DrawMode = DrawMode.OwnerDrawFixed;
            lb.DrawItem += (s, ev) =>
            {
                if (ev.Index < 0 || ev.Index >= lb.Items.Count) return;
                bool sel = (ev.State & DrawItemState.Selected) == DrawItemState.Selected;
                // 简约风格：选中行纯蓝填充
                using (SolidBrush b = new SolidBrush(sel ? Theme.BLUE : Theme.CARD))
                    ev.Graphics.FillRectangle(b, ev.Bounds);
                TextRenderer.DrawText(ev.Graphics, lb.Items[ev.Index].ToString(), Font,
                    new Rectangle(ev.Bounds.X + 10, ev.Bounds.Y, ev.Bounds.Width - 12, ev.Bounds.Height),
                    sel ? Color.White : Theme.TEXT,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            lb.Width = Math.Max(Width, 110);
            lb.Items.AddRange(Items.ToArray());
            if (selIndex >= 0 && selIndex < Items.Count) lb.SelectedIndex = selIndex;
            lb.Height = Math.Min(lb.PreferredHeight, 264);
            lb.MouseUp += (s, e) =>
            {
                int idx = lb.IndexFromPoint(e.Location);
                if (idx >= 0) SelectedIndex = idx;
                CloseDrop();
            };
            lb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && lb.SelectedIndex >= 0) { SelectedIndex = lb.SelectedIndex; CloseDrop(); }
                else if (e.KeyCode == Keys.Escape) CloseDrop();
            };

            drop = new ToolStripDropDown();
            drop.BackColor = Theme.CARD;
            drop.Margin = Padding.Empty;
            drop.Padding = Padding.Empty;
            drop.DropShadowEnabled = true;
            drop.AutoSize = false;
            drop.Renderer = new DarkDropRenderer();
            ToolStripControlHost host = new ToolStripControlHost(lb);
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            host.AutoSize = false;
            host.Size = new Size(lb.Width, lb.Height);
            drop.Items.Add(host);
            drop.Size = new Size(lb.Width + 2, lb.Height + 2);
            drop.Closed += (s, e) => { drop = null; };
            drop.Show(this, -1, Height + 4);
            lb.Focus();
        }

        private void CloseDrop()
        {
            if (drop != null) { drop.Close(); drop = null; }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (inner != null) inner.Enabled = Enabled;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, 8f))
            {
                // 简约风格：纯色填充 + 细边框，焦点为蓝色
                using (SolidBrush b = new SolidBrush(focused ? Theme.INPUT : (hover ? Theme.CTRL_H : Theme.CTRL)))
                    g.FillPath(b, path);
                Color edge = !Enabled ? Theme.BORDER
                    : (focused ? Theme.BLUE : (hover ? Theme.MUTED : Theme.BORDER));
                using (GraphicsPath p4 = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, 8f))
                using (Pen pen = new Pen(edge, focused ? 1.5f : 1f)) g.DrawPath(pen, p4);
            }

            if (inner == null)
            {
                string tt = selIndex >= 0 && selIndex < Items.Count ? Items[selIndex] : "";
                TextRenderer.DrawText(g, tt, Font, new Rectangle(11, 0, Width - 36, Height),
                    Enabled ? ForeColor : Theme.DIM,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            Color ac = Enabled ? (hover ? Color.White : Theme.MUTED) : Theme.DIM;
            float cx = Width - 16f, cy = Height / 2f;
            using (SolidBrush b = new SolidBrush(ac))
            {
                PointF[] tri = { new PointF(cx - 3.6f, cy - 1.6f), new PointF(cx + 3.6f, cy - 1.6f), new PointF(cx, cy + 2.8f) };
                g.FillPolygon(b, tri);
            }
        }

        private class DarkDropRenderer : ToolStripProfessionalRenderer
        {
            public DarkDropRenderer() : base(new DarkDropColors()) { RoundedEdges = false; }
        }

        private class DarkDropColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.CARD; } }
            public override Color MenuBorder { get { return Theme.BORDER; } }
            public override Color ImageMarginGradientBegin { get { return Theme.CARD; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.CARD; } }
            public override Color ImageMarginGradientEnd { get { return Theme.CARD; } }
        }
    }

    // ================= 深色多行编辑框 =================
    internal class DarkTextBox : TextBox
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        public DarkTextBox()
        {
            Multiline = true;
            WordWrap = false;
            BorderStyle = BorderStyle.None;
            ScrollBars = ScrollBars.None;
            BackColor = Theme.INPUT;
            ForeColor = Theme.TEXT;
            Font = Theme.MONO;
        }

        public int VisibleLines
        {
            get { int lh = UI.LineHeight(Font); return lh > 0 ? ClientSize.Height / lh : 1; }
        }

        public int TotalLines
        {
            get { return UI.SendMessage(Handle, UI.EM_GETLINECOUNT, 0, 0); }
        }

        public int MaxTop
        {
            get { return Math.Max(0, TotalLines - VisibleLines); }
        }

        public int FirstVisibleLine
        {
            get { return UI.SendMessage(Handle, UI.EM_GETFIRSTVISIBLELINE, 0, 0); }
        }

        public void ScrollToLine(int line)
        {
            int cur = FirstVisibleLine;
            if (line != cur) UI.SendMessage(Handle, UI.EM_LINESCROLL, 0, line - cur);
        }

        public void ScrollBy(int lines)
        {
            ScrollToLine(Math.Min(Math.Max(0, FirstVisibleLine + lines), MaxTop));
        }

        public void ScrollToEnd()
        {
            SelectionStart = TextLength;
            SelectionLength = 0;
            ScrollToLine(MaxTop);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                ScrollBy(-(delta / 120) * 3);
                return;
            }
            base.WndProc(ref m);
        }
    }

    /// 悬浮细滚动条（iOS 风格）
    internal class ThumbBar : Panel
    {
        private DarkTextBox target;
        private bool hover = false;
        private bool dragging = false;
        private int dragStartY;
        private int dragStartTop;
        private readonly System.Windows.Forms.Timer poll;

        public ThumbBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Width = 11;
            poll = new System.Windows.Forms.Timer();
            poll.Interval = 90;
            poll.Tick += (s, e) => Invalidate();
            poll.Start();
        }

        public void Bind(DarkTextBox tb) { target = tb; }

        private RectangleF ThumbRect()
        {
            if (target == null) return RectangleF.Empty;
            int total = target.TotalLines;
            int vis = target.VisibleLines;
            if (total <= vis) return RectangleF.Empty;
            float trackTop = 5f, trackH = Height - 10f;
            float th = Math.Max(28f, trackH * vis / total);
            if (th > trackH) th = trackH;
            int maxTop = target.MaxTop;
            double ratio = maxTop > 0 ? (double)target.FirstVisibleLine / maxTop : 0;
            float top = trackTop + (float)((trackH - th) * ratio);
            return new RectangleF(3f, top, 5f, th);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; if (!dragging) Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (target == null) return;
            RectangleF tr = ThumbRect();
            if (tr.Contains(e.Location))
            {
                dragging = true;
                dragStartY = e.Y;
                dragStartTop = target.FirstVisibleLine;
                Capture = true;
            }
            else
            {
                int dir = e.Y < tr.Y ? -1 : 1;
                target.ScrollBy(dir * Math.Max(1, target.VisibleLines - 1));
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging || target == null) return;
            int total = target.TotalLines;
            int vis = target.VisibleLines;
            float trackH = Height - 10f;
            float th = Math.Max(28f, trackH * vis / total);
            int maxTop = target.MaxTop;
            float span = trackH - th;
            if (span <= 0 || maxTop <= 0) return;
            int dTop = (int)Math.Round((double)(e.Y - dragStartY) * maxTop / span);
            target.ScrollToLine(Math.Min(Math.Max(0, dragStartTop + dTop), maxTop));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            RectangleF tr = ThumbRect();
            if (tr.IsEmpty) return;
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            Color c = (hover || dragging) ? Theme.THUMB_H : Theme.THUMB;
            using (GraphicsPath p = UI.RoundRectF(tr.X, tr.Y, tr.Width, tr.Height, 2.5f))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }
    }

    /// 扁平内凹井（编辑区容器）
    internal class GlassWell : Panel
    {
        public int Radius = 8;

        public GlassWell()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Padding = new Padding(9, 7, 2, 7);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
            {
                // 简约风格：纯色井底 + 细边框
                using (SolidBrush b = new SolidBrush(Theme.INPUT)) g.FillPath(b, path);
                using (GraphicsPath p2 = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, Radius))
                using (Pen pen = new Pen(Theme.BORDER, 1f)) g.DrawPath(pen, p2);
            }
        }
    }

    // ================= 玻璃消息弹窗 =================
    internal static class Dlg
    {
        public static void Msg(IWin32Window owner, string text, string title, bool error = false, int width = 460)
        {
            Form f = new Form();
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.StartPosition = owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            f.ShowInTaskbar = false;
            f.BackColor = Theme.BG;
            f.Font = Theme.UI;
            f.Icon = Theme.AppIcon;

            int pad = 24;
            int textW = width - pad * 2;
            Size ms = TextRenderer.MeasureText(text, Theme.UI, new Size(textW, 10000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = 26 + ms.Height + 28 + 36 + 18;
            f.ClientSize = new Size(width, h);

            f.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Theme.BG))
                    g.FillRectangle(b, 0, 0, width, h);
            };

            Label lblIcon = new Label();
            lblIcon.Text = "●";
            lblIcon.Font = new Font("Segoe UI", 10F);
            lblIcon.ForeColor = error ? Theme.RED : Theme.BLUE;
            lblIcon.AutoSize = true;
            lblIcon.BackColor = Color.Transparent;
            lblIcon.Location = new Point(pad, 26);
            f.Controls.Add(lblIcon);

            Label lblText = new Label();
            lblText.Text = text;
            lblText.BackColor = Color.Transparent;
            lblText.ForeColor = Theme.TEXT;
            lblText.Location = new Point(pad + 22, 24);
            lblText.Size = new Size(textW - 22, ms.Height + 4);
            f.Controls.Add(lblText);

            GlassButton ok = new GlassButton();
            ok.Text = "好";
            ok.Kind = GlassButton.Style.Accent;
            ok.Size = new Size(96, 36);
            ok.Radius = 8;
            ok.Bold = true;
            ok.Location = new Point(width - pad - 96, h - 36 - 16);
            ok.DialogResult = DialogResult.OK;
            f.Controls.Add(ok);
            f.AcceptButton = ok;
            f.CancelButton = ok;

            f.Load += (s, e) => UI.Dark(f);
            UI.AnimateIn(f, 200, 10);
            f.ShowDialog(owner);
            f.Dispose();
        }
    }

    public class MainForm : Form
    {
        private static readonly Color C_TEXT = Theme.TEXT;
        private static readonly Color C_MUTED = Theme.MUTED;
        private static readonly Color C_ACCENT = Theme.ACCENT;
        private static readonly Color C_GREEN = Theme.GREEN;

        private static readonly Font F_UI = Theme.UI;
        private static readonly Font F_UI_BOLD = Theme.UI_BOLD;

        // ===== 界面控件 =====
        private GlassCombo cbPort;
        private GlassCombo cbBaud;
        private GlassButton btnOpen;
        private GlassButton btnMore;
        private GlassButton btnRefresh;
        private Panel dotHost;
        private Label lblStatus;
        private Label lblCount;

        private DarkTextBox rxBox;
        private DarkTextBox txBox;

        private GlassCheck ckHexShow;
        private GlassCheck ckTimestamp;
        private TextBox tbTimeout;
        private GlassCheck ckSaveFile;
        private GlassButton btnSaveRx;
        private GlassButton btnClearRx;

        private GlassCheck ckHexSend;
        private GlassCheck ckNewline;
        private GlassCheck ckTimer;
        private TextBox tbTimerMs;
        private TextBox tbFromByte;
        private GlassCombo cbToPos;
        private GlassCombo cbChecksum;

        private GlassButton btnOpenFile;
        private GlassButton btnSendFile;
        private GlassButton btnStopFile;
        private Label lblFileStatus;
        private GlassButton btnSend;

        // ===== 运行状态 =====
        private SerialPort port;
        private System.Windows.Forms.Timer rxIdleTimer;
        private System.Windows.Forms.Timer txTimer;
        private System.Windows.Forms.Timer fileTimer;
        private System.Windows.Forms.Timer uiApplyTimer;
        private readonly List<byte> rxBuf = new List<byte>();
        private readonly object rxLock = new object();
        private long rxTotal;
        private long txTotal;
        private FileStream rxFileStream;
        private FileStream sendStream;
        private string sendFilePath = "";
        private bool realClose = false;

        private int moreDataIdx = 0;
        private int moreParityIdx = 0;
        private int moreStopIdx = 0;

        private static readonly int[] DataBitsTable = { 8, 7, 6, 5 };
        private static readonly Parity[] ParityTable = { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space };
        private static readonly StopBits[] StopBitsTable = { StopBits.One, StopBits.OnePointFive, StopBits.Two };

        public MainForm()
        {
            Text = "串口调试助手";
            Icon = Theme.AppIcon;
            Size = new Size(1080, 760);
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = F_UI;
            BackColor = Theme.BG;
            ForeColor = C_TEXT;
            DoubleBuffered = true;
            Padding = new Padding(2);

            Panel pRx = BuildRxArea();
            Panel pSend = BuildSendArea();
            Panel pTop = BuildTopBar();
            Controls.Add(pRx);
            Controls.Add(pSend);
            Controls.Add(pTop);

            rxIdleTimer = new System.Windows.Forms.Timer();
            rxIdleTimer.Tick += (s, e) => FlushRxPacket();
            txTimer = new System.Windows.Forms.Timer();
            txTimer.Tick += (s, e) => DoSend();
            fileTimer = new System.Windows.Forms.Timer();
            fileTimer.Tick += (s, e) => FileSendTick();
            uiApplyTimer = new System.Windows.Forms.Timer();
            uiApplyTimer.Interval = 150;
            uiApplyTimer.Tick += (s, e) => ApplyPendingFriendly();
            uiApplyTimer.Start();

            RenderBackdrop();
            Load += (s, e) => { UI.Dark(this); RenderBackdrop(); };
            // 静态背景：无需 resize 重渲染
            Move += (s, e) => UpdateOrigin();
            LocationChanged += (s, e) => UpdateOrigin();
            UI.AnimateIn(this, 320, 26);

            RefreshPorts();
            LoadSettings();
            FormClosing += (s, e) =>
            {
                if (realClose) return;
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    realClose = true;
                    Cleanup();
                    System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                    t.Interval = 15;
                    double el = 0;
                    t.Tick += (s2, e2) =>
                    {
                        el += 15;
                        double p2 = el / 140;
                        if (p2 >= 1) { t.Stop(); t.Dispose(); try { Close(); } catch { Application.Exit(); } return; }
                        try { Opacity = 1 - p2 * p2; } catch { t.Stop(); try { Close(); } catch { } }
                    };
                    t.Start();
                }
                else
                {
                    realClose = true;
                    Cleanup();
                }
            };
        }

        // ================= 玻璃背景 =================

        private void RenderBackdrop()
        {
            GlassBackdrop.Instance.Render(ClientSize);
            UpdateOrigin();
        }

        private bool backdropBusy = false;

        /// 异步重渲染背景（resize 结束后调用，UI 不阻塞）
        private void RenderBackdropAsync()
        {
            if (backdropBusy) return;
            backdropBusy = true;
            Size sz = ClientSize;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { GlassBackdrop.Instance.Render(sz); }
                catch { }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        backdropBusy = false;
                        UpdateOrigin();
                        RefreshGlass();
                    }));
                }
                catch { backdropBusy = false; }
            });
        }

        private void UpdateOrigin()
        {
            try { GlassBackdrop.Instance.Origin = PointToScreen(Point.Empty); } catch { }
        }

        private void RefreshGlass()
        {
            UpdateOrigin();
            try { Invalidate(true); } catch { }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 简约风格：纯色背景，零开销
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            Graphics g = e.Graphics;
            using (SolidBrush b = new SolidBrush(Theme.BG))
                g.FillRectangle(b, ClientRectangle);
        }

        // ================= 控件工厂 =================

        private GlassButton MkBtn(string text, GlassButton.Style kind, int w)
        {
            GlassButton b = new GlassButton();
            b.Text = text;
            b.Kind = kind;
            b.Width = w;
            b.Height = 30;
            b.Margin = new Padding(4, 0, 4, 0);
            return b;
        }

        private Label MkLabel(string text, Color fg, bool bold)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = fg;
            l.Font = bold ? F_UI_BOLD : F_UI;
            l.AutoSize = true;
            l.BackColor = Color.Transparent;
            l.Margin = new Padding(6, 9, 2, 3);
            return l;
        }

        private GlassCheck MkChk(string text, bool checkedInit)
        {
            GlassCheck c = new GlassCheck();
            c.Text = text;
            c.Checked = checkedInit;
            c.ForeColor = C_TEXT;
            c.Margin = new Padding(8, 6, 8, 3);
            c.FitWidth();
            return c;
        }

        private GlassCombo MkCombo(int w, bool editable)
        {
            GlassCombo c = new GlassCombo();
            c.Editable = editable;
            c.Width = w;
            c.Margin = new Padding(4, 4, 4, 3);
            if (editable) c.EnsureInner();
            return c;
        }

        /// 玻璃小输入框
        private TextBox MkInput(string text, int w)
        {
            TextBox t = new TextBox();
            t.Text = text;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = Theme.INPUT;
            t.ForeColor = C_TEXT;
            t.Font = F_UI;
            t.Width = w - 18;
            t.Height = 18;

            InputShell wrap = new InputShell();
            wrap.Size = new Size(w, 28);
            wrap.Margin = new Padding(4, 5, 4, 3);
            wrap.Inner = t;
            t.Location = new Point(9, (28 - 18) / 2);
            wrap.Controls.Add(t);
            t.Enter += (s, e) => wrap.Invalidate();
            t.Leave += (s, e) => wrap.Invalidate();
            wrap.MouseDown += (s, e) => t.Focus();

            t.Tag = wrap;
            return t;
        }

        private void AddInput(FlowLayoutPanel f, TextBox t)
        {
            f.Controls.Add((Control)t.Tag);
        }

        private Panel MkEditor(DarkTextBox box)
        {
            GlassWell well = new GlassWell();
            well.Margin = new Padding(0);
            box.Dock = DockStyle.Fill;
            ThumbBar tb = new ThumbBar();
            tb.Dock = DockStyle.Right;
            tb.Bind(box);
            well.Controls.Add(box);
            well.Controls.Add(tb);
            return well;
        }

        private FlowLayoutPanel MkRow(int height)
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.BackColor = Color.Transparent;
            f.Dock = DockStyle.Top;
            f.Height = height;
            f.WrapContents = false;
            f.Padding = new Padding(12, 0, 8, 0);
            return f;
        }

        private Panel WrapCard(Control card, DockStyle dock, int glassHeight, int bottomGap)
        {
            Panel w = new Panel();
            w.Dock = dock;
            GlassCard gc = card as GlassCard;
            int sm = gc != null ? gc.ShadowMargin : 0;
            if (glassHeight > 0) w.Height = glassHeight + sm * 2;
            w.BackColor = Color.Transparent;
            w.Padding = new Padding(0, 0, 0, bottomGap);
            card.Dock = DockStyle.Fill;
            w.Controls.Add(card);
            return w;
        }

        // ================= 界面构建 =================

        private Panel BuildTopBar()
        {
            GlassCard bar = new GlassCard();
            bar.ShadowMargin = 11;
            bar.Padding = new Padding(11 + 14, 11 + 11, 11 + 14, 11 + 11);

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.WrapContents = false;
            left.BackColor = Color.Transparent;

            left.Controls.Add(MkLabel("端口", C_MUTED, false));
            cbPort = MkCombo(224, true);
            left.Controls.Add(cbPort);
            btnRefresh = MkBtn("刷新", GlassButton.Style.Neutral, 56);
            btnRefresh.Click += (s, e) => RefreshPorts();
            left.Controls.Add(btnRefresh);

            left.Controls.Add(MkLabel("波特率", C_MUTED, false));
            cbBaud = MkCombo(100, true);
            cbBaud.Items.AddRange(new string[] { "9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600" });
            cbBaud.Text = "115200";
            left.Controls.Add(cbBaud);

            btnMore = MkBtn("参数", GlassButton.Style.Neutral, 56);
            btnMore.Click += (s, e) => ShowMoreSettings();
            left.Controls.Add(btnMore);

            btnOpen = MkBtn("打开串口", GlassButton.Style.Accent, 104);
            btnOpen.Bold = true;
            btnOpen.Click += (s, e) => TogglePort();
            left.Controls.Add(btnOpen);

            // 状态指示灯（自绘玻璃圆点）
            dotHost = new Panel();
            dotHost.Width = 22;
            dotHost.Height = 22;
            dotHost.BackColor = Color.Transparent;
            dotHost.Margin = new Padding(14, 8, 0, 3);
            dotHost.Paint += (s, e) => PaintDot(e.Graphics, dotConnected ? Theme.GREEN : Color.FromArgb(90, 100, 114));
            left.Controls.Add(dotHost);
            lblStatus = MkLabel("未连接", C_MUTED, false);
            left.Controls.Add(lblStatus);

            FlowLayoutPanel right = new FlowLayoutPanel();
            right.Dock = DockStyle.Right;
            right.Width = 252;
            right.WrapContents = false;
            right.BackColor = Color.Transparent;
            right.FlowDirection = FlowDirection.RightToLeft;
            right.Padding = new Padding(0, 2, 0, 0);

            GlassButton btnChecksumTool = MkBtn("校验工具", GlassButton.Style.Neutral, 82);
            btnChecksumTool.Click += (s, e) => new ChecksumForm().ShowDialog(this);
            right.Controls.Add(btnChecksumTool);
            GlassButton btnHelp = MkBtn("帮助", GlassButton.Style.Neutral, 56);
            btnHelp.Click += (s, e) => ShowHelp();
            right.Controls.Add(btnHelp);
            GlassButton btnSaveParams = MkBtn("保存参数", GlassButton.Style.Neutral, 82);
            btnSaveParams.Click += (s, e) => SaveSettings();
            right.Controls.Add(btnSaveParams);

            bar.Controls.Add(left);
            bar.Controls.Add(right);
            return WrapCard(bar, DockStyle.Top, 58, 0);
        }

        private bool dotConnected = false;

        private void PaintDot(Graphics g, Color c)
        {
            UI.HiQuality(g);
            // 简约风格：纯色圆点 + 细描边，去掉外发光
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(new RectangleF(6f, 6f, 10f, 10f));
                using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
                using (GraphicsPath ring = new GraphicsPath())
                {
                    ring.AddEllipse(new RectangleF(5.5f, 5.5f, 11f, 11f));
                    using (Pen pen = new Pen(Color.FromArgb(dotConnected ? 140 : 60, c.R, c.G, c.B), 1.2f))
                        g.DrawPath(pen, ring);
                }
            }
        }

        private Panel BuildRxArea()
        {
            GlassCard rxCard = new GlassCard();
            rxCard.ShadowMargin = 11;

            Panel headWrap = new Panel();
            headWrap.Dock = DockStyle.Top;
            headWrap.Height = 46;
            headWrap.BackColor = Color.Transparent;

            FlowLayoutPanel headRight = new FlowLayoutPanel();
            headRight.Dock = DockStyle.Right;
            headRight.Width = 132;
            headRight.WrapContents = false;
            headRight.FlowDirection = FlowDirection.RightToLeft;
            headRight.BackColor = Color.Transparent;
            headRight.Padding = new Padding(0, 10, 12, 0);
            lblCount = MkLabel("RX 0 · TX 0", C_MUTED, false);
            lblCount.Margin = new Padding(2, 2, 2, 3);
            headRight.Controls.Add(lblCount);

            FlowLayoutPanel head = MkRow(46);
            head.Dock = DockStyle.Fill;
            head.Padding = new Padding(14, 5, 8, 0);
            Label cap = MkLabel("接收区", Theme.BLUE_HI, true);
            cap.Margin = new Padding(4, 11, 12, 3);
            head.Controls.Add(cap);
            ckHexShow = MkChk("HEX 显示", false);
            head.Controls.Add(ckHexShow);
            ckTimestamp = MkChk("时间戳分包", true);
            head.Controls.Add(ckTimestamp);
            head.Controls.Add(MkLabel("超时", C_MUTED, false));
            tbTimeout = MkInput("20", 48);
            tbTimeout.TextChanged += (s, e) => SyncRxIdleInterval();
            AddInput(head, tbTimeout);
            head.Controls.Add(MkLabel("ms", C_MUTED, false));
            ckSaveFile = MkChk("接收存文件", false);
            ckSaveFile.CheckedChanged += (s, e) => ToggleRxFile();
            head.Controls.Add(ckSaveFile);
            btnSaveRx = MkBtn("保存数据", GlassButton.Style.Neutral, 82);
            btnSaveRx.Margin = new Padding(8, 7, 4, 3);
            btnSaveRx.Click += (s, e) => SaveRxData();
            head.Controls.Add(btnSaveRx);
            btnClearRx = MkBtn("清空", GlassButton.Style.Neutral, 56);
            btnClearRx.Margin = new Padding(4, 7, 4, 3);
            btnClearRx.Click += (s, e) => rxBox.Clear();
            head.Controls.Add(btnClearRx);

            rxBox = new DarkTextBox();
            rxBox.ReadOnly = true;
            Panel editor = MkEditor(rxBox);
            editor.Dock = DockStyle.Fill;

            rxCard.Padding = new Padding(11 + 6, 0, 11 + 6, 11 + 8);
            rxCard.Controls.Add(editor);
            headWrap.Controls.Add(head);
            headWrap.Controls.Add(headRight);
            rxCard.Controls.Add(headWrap);
            return WrapCard(rxCard, DockStyle.Fill, 0, 0);
        }

        private Panel BuildSendArea()
        {
            GlassCard sendCard = new GlassCard();
            sendCard.ShadowMargin = 11;

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 46;
            head.BackColor = Color.Transparent;

            FlowLayoutPanel headLeft = new FlowLayoutPanel();
            headLeft.Dock = DockStyle.Fill;
            headLeft.WrapContents = false;
            headLeft.BackColor = Color.Transparent;
            headLeft.Padding = new Padding(14, 5, 0, 0);
            Label cap = MkLabel("发送区", Theme.BLUE_HI, true);
            cap.Margin = new Padding(4, 11, 12, 3);
            headLeft.Controls.Add(cap);
            lblFileStatus = MkLabel("文件发送: 未开始", C_MUTED, false);
            lblFileStatus.Margin = new Padding(6, 12, 2, 3);
            headLeft.Controls.Add(lblFileStatus);

            FlowLayoutPanel headRight = new FlowLayoutPanel();
            headRight.Dock = DockStyle.Right;
            headRight.Width = 372;
            headRight.WrapContents = false;
            headRight.FlowDirection = FlowDirection.RightToLeft;
            headRight.BackColor = Color.Transparent;
            headRight.Padding = new Padding(0, 7, 12, 0);
            btnSend = MkBtn("发 送", GlassButton.Style.Accent, 100);
            btnSend.Height = 32;
            btnSend.Bold = true;
            btnSend.Click += (s, e) => DoSend();
            headRight.Controls.Add(btnSend);
            btnStopFile = MkBtn("停止", GlassButton.Style.Danger, 56);
            btnStopFile.Click += (s, e) => StopFileSend("已停止");
            headRight.Controls.Add(btnStopFile);
            btnSendFile = MkBtn("发送文件", GlassButton.Style.Neutral, 82);
            btnSendFile.Click += (s, e) => StartFileSend();
            headRight.Controls.Add(btnSendFile);
            btnOpenFile = MkBtn("打开文件", GlassButton.Style.Neutral, 82);
            btnOpenFile.Click += (s, e) => OpenFileToSend();
            headRight.Controls.Add(btnOpenFile);

            FlowLayoutPanel opt = MkRow(46);
            opt.Dock = DockStyle.Bottom;
            opt.Padding = new Padding(14, 5, 8, 0);
            ckHexSend = MkChk("HEX 发送", true);
            opt.Controls.Add(ckHexSend);
            ckNewline = MkChk("回车换行", false);
            opt.Controls.Add(ckNewline);
            ckTimer = MkChk("定时发送", false);
            ckTimer.CheckedChanged += (s, e) => ToggleTimerSend();
            opt.Controls.Add(ckTimer);
            tbTimerMs = MkInput("1000", 58);
            AddInput(opt, tbTimerMs);
            opt.Controls.Add(MkLabel("ms/次", C_MUTED, false));
            opt.Controls.Add(MkLabel("第", C_MUTED, false));
            tbFromByte = MkInput("1", 48);
            AddInput(opt, tbFromByte);
            opt.Controls.Add(MkLabel("字节 至", C_MUTED, false));
            cbToPos = MkCombo(70, false);
            cbToPos.Items.AddRange(new string[] { "末尾", "头部", "两端" });
            cbToPos.SelectedIndex = 0;
            opt.Controls.Add(cbToPos);
            opt.Controls.Add(MkLabel("加校验", C_MUTED, false));
            cbChecksum = MkCombo(82, false);
            cbChecksum.Items.AddRange(new string[] { "None", "字节和", "异或" });
            cbChecksum.SelectedIndex = 0;
            opt.Controls.Add(cbChecksum);

            txBox = new DarkTextBox();
            Panel editor = MkEditor(txBox);
            editor.Dock = DockStyle.Fill;

            head.Controls.Add(headLeft);
            head.Controls.Add(headRight);
            sendCard.Padding = new Padding(11 + 6, 0, 11 + 6, 11 + 8);
            sendCard.Controls.Add(editor);
            sendCard.Controls.Add(opt);
            sendCard.Controls.Add(head);
            return WrapCard(sendCard, DockStyle.Bottom, 220, 0);
        }

        // ================= 串口管理 =================

        /// 立即刷新端口列表，并异步补充设备描述
        private void RefreshPorts()
        {
            string cur = SelectedPortName();
            string[] names = SerialPort.GetPortNames();
            Array.Sort(names);
            FillPortList(names, null);
            SetComboSelection(cbPort, cur);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Dictionary<string, string> map = QueryFriendlyNames();
                lock (pendingLock)
                {
                    pendingNames = names;
                    pendingMap = map;
                }
            });
        }

        private readonly object pendingLock = new object();
        private string[] pendingNames;
        private Dictionary<string, string> pendingMap;

        private void ApplyPendingFriendly()
        {
            string[] names;
            Dictionary<string, string> map;
            lock (pendingLock)
            {
                names = pendingNames;
                map = pendingMap;
                pendingNames = null;
                pendingMap = null;
            }
            if (names == null || map == null) return;
            if (port != null && port.IsOpen) return;
            string cur = SelectedPortName();
            FillPortList(names, map);
            SetComboSelection(cbPort, cur);
        }

        private void FillPortList(string[] names, Dictionary<string, string> map)
        {
            cbPort.Items.Clear();
            foreach (string n in names)
            {
                string desc;
                if (map != null && map.TryGetValue(n, out desc) && desc.Length > 0)
                    cbPort.Items.Add(n + "  (" + desc + ")");
                else
                    cbPort.Items.Add(n);
            }
        }

        private static void SetComboSelection(GlassCombo cb, string cur)
        {
            if (string.IsNullOrEmpty(cur))
            {
                if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                else cb.Text = "";
                return;
            }
            for (int i = 0; i < cb.Items.Count; i++)
            {
                string it = cb.Items[i];
                if (it == cur || it.StartsWith(cur + " ")) { cb.SelectedIndex = i; return; }
            }
            cb.Text = cur;
        }

        /// 从显示文本里取出真实 COM 号
        private string SelectedPortName()
        {
            string t = cbPort.Text == null ? "" : cbPort.Text.Trim();
            Match m = Regex.Match(t, @"^(COM\d+)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            return t;
        }

        /// 查询各 COM 口对应的设备友好名称
        private static Dictionary<string, string> QueryFriendlyNames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            string name = mo["Name"] as string;
                            if (string.IsNullOrEmpty(name)) continue;
                            Match m = Regex.Match(name, @"\((COM\d+)\)\s*$", RegexOptions.IgnoreCase);
                            if (!m.Success) continue;
                            string com = m.Groups[1].Value.ToUpperInvariant();
                            string desc = name.Substring(0, m.Index).Trim();
                            if (desc.Length > 0 && !map.ContainsKey(com)) map[com] = desc;
                        }
                        finally { try { mo.Dispose(); } catch { } }
                    }
                }
            }
            catch { }
            return map;
        }

        private void TogglePort()
        {
            if (port != null && port.IsOpen)
            {
                ClosePort();
                return;
            }
            string portName = SelectedPortName();
            if (string.IsNullOrEmpty(portName))
            {
                Dlg.Msg(this, "没有可用的串口，请检查设备连接后点击“刷新”。", "提示");
                return;
            }
            int baud;
            if (!int.TryParse(cbBaud.Text, out baud) || baud <= 0)
            {
                Dlg.Msg(this, "波特率填写不正确。", "提示");
                return;
            }
            try
            {
                SerialPort p = new SerialPort(portName);
                p.BaudRate = baud;
                p.DataBits = DataBitsTable[moreDataIdx];
                p.Parity = ParityTable[moreParityIdx];
                p.StopBits = StopBitsTable[moreStopIdx];
                p.RtsEnable = false;
                p.DtrEnable = false;
                p.ReceivedBytesThreshold = 1;
                p.DataReceived += OnPortData;
                p.Open();
                port = p;
                btnOpen.Text = "关闭串口";
                btnOpen.Kind = GlassButton.Style.Danger;
                dotConnected = true;
                if (dotHost != null) dotHost.Invalidate();
                lblStatus.Text = portName + " 已连接";
                lblStatus.ForeColor = C_GREEN;
                cbPort.Enabled = false;
                cbBaud.Enabled = false;
                btnMore.Enabled = false;
            }
            catch (Exception ex)
            {
                Dlg.Msg(this, "打开串口失败：" + ex.Message, "错误", true);
            }
        }

        private void ClosePort()
        {
            try
            {
                if (port != null)
                {
                    port.DataReceived -= OnPortData;
                    if (port.IsOpen) port.Close();
                    port.Dispose();
                }
            }
            catch { }
            port = null;
            if (btnOpen != null)
            {
                btnOpen.Text = "打开串口";
                btnOpen.Kind = GlassButton.Style.Accent;
            }
            dotConnected = false;
            if (dotHost != null) dotHost.Invalidate();
            if (lblStatus != null)
            {
                lblStatus.Text = "未连接";
                lblStatus.ForeColor = C_MUTED;
            }
            if (cbPort != null) cbPort.Enabled = true;
            if (cbBaud != null) cbBaud.Enabled = true;
            if (btnMore != null) btnMore.Enabled = true;
        }

        private void OnPortData(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort p = port;
            if (p == null || !p.IsOpen) return;
            try
            {
                int n = p.BytesToRead;
                if (n <= 0) return;
                byte[] buf = new byte[n];
                int read = p.Read(buf, 0, n);
                if (read <= 0) return;
                if (read < n) Array.Resize(ref buf, read);
                System.Threading.Interlocked.Add(ref rxTotal, read);
                FileStream fs = rxFileStream;
                if (fs != null)
                {
                    try { fs.Write(buf, 0, buf.Length); fs.Flush(); } catch { }
                }
                lock (rxLock) { rxBuf.AddRange(buf); }
                try { BeginInvoke((Action)(() => { rxIdleTimer.Stop(); SyncRxIdleInterval(); rxIdleTimer.Start(); })); } catch { }
            }
            catch { }
        }

        private void SyncRxIdleInterval()
        {
            int ms;
            if (!int.TryParse(tbTimeout.Text, out ms) || ms < 1) ms = 20;
            if (ms > 10000) ms = 10000;
            rxIdleTimer.Interval = ms;
        }

        private void FlushRxPacket()
        {
            rxIdleTimer.Stop();
            byte[] packet;
            lock (rxLock)
            {
                if (rxBuf.Count == 0) return;
                packet = rxBuf.ToArray();
                rxBuf.Clear();
            }
            string body;
            if (ckHexShow.Checked) body = BytesToHex(packet);
            else body = Encoding.Default.GetString(packet);
            string prefix = ckTimestamp.Checked
                ? "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] "
                : "";
            AppendRx(prefix + body);
            UpdateCounts();
        }

        private void AppendRx(string s)
        {
            if (rxBox.InvokeRequired)
            {
                try { rxBox.BeginInvoke((Action)(() => AppendRx(s))); } catch { }
                return;
            }
            rxBox.AppendText(s + "\r\n");
            if (rxBox.TextLength > 600000)
            {
                rxBox.Select(0, 200000);
                rxBox.SelectedText = "";
            }
            rxBox.ScrollToEnd();
        }

        private void UpdateCounts()
        {
            if (lblCount.InvokeRequired)
            {
                try { lblCount.BeginInvoke((Action)UpdateCounts); } catch { }
                return;
            }
            lblCount.Text = "RX " + rxTotal + " · TX " + txTotal;
        }

        // ================= 发送 =================

        private byte[] BuildSendBytes()
        {
            string s = txBox.Text;
            byte[] data;
            if (ckHexSend.Checked) data = ParseHex(s);
            else data = Encoding.Default.GetBytes(s);
            if (ckNewline.Checked)
            {
                byte[] crlf = { 0x0D, 0x0A };
                byte[] joined = new byte[data.Length + 2];
                Buffer.BlockCopy(data, 0, joined, 0, data.Length);
                Buffer.BlockCopy(crlf, 0, joined, data.Length, 2);
                data = joined;
            }
            return data;
        }

        private void DoSend()
        {
            if (port == null || !port.IsOpen)
            {
                Dlg.Msg(this, "串口未打开，无法发送。", "提示");
                return;
            }
            byte[] data;
            try
            {
                data = BuildSendBytes();
            }
            catch (FormatException ex)
            {
                ckTimer.Checked = false;
                Dlg.Msg(this, ex.Message, "HEX 格式错误");
                return;
            }
            if (data.Length == 0) return;

            if (cbChecksum.SelectedIndex != 0)
            {
                int len = data.Length;
                int fromIdx = ParseInt(tbFromByte.Text, 1) - 1;
                if (fromIdx > len) fromIdx = len;
                int off, cnt;
                if (cbToPos.SelectedIndex == 0) { off = fromIdx; cnt = len - fromIdx; }
                else if (cbToPos.SelectedIndex == 1) { off = 0; cnt = Math.Min(fromIdx + 1, len); }
                else { off = 0; cnt = len; }
                byte cs = ComputeChecksum(data, off, cnt, cbChecksum.SelectedIndex);
                byte[] withCs = new byte[len + 1];
                Buffer.BlockCopy(data, 0, withCs, 0, len);
                withCs[len] = cs;
                data = withCs;
            }
            try
            {
                port.Write(data, 0, data.Length);
                System.Threading.Interlocked.Add(ref txTotal, data.Length);
                UpdateCounts();
            }
            catch (Exception ex)
            {
                ckTimer.Checked = false;
                Dlg.Msg(this, "发送失败：" + ex.Message, "错误", true);
            }
        }

        private void ToggleTimerSend()
        {
            if (ckTimer.Checked)
            {
                int ms = ParseInt(tbTimerMs.Text, 1000);
                if (ms < 1) ms = 1000;
                if (ms > 60000) ms = 60000;
                txTimer.Interval = ms;
                txTimer.Start();
            }
            else
            {
                txTimer.Stop();
            }
        }

        // ================= 文件发送 =================

        private void OpenFileToSend()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择要发送的文件";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                StopFileSend("未开始");
                try
                {
                    sendStream = File.OpenRead(dlg.FileName);
                    sendFilePath = dlg.FileName;
                    lblFileStatus.Text = "已载入: " + Path.GetFileName(sendFilePath) + " (" + sendStream.Length + " 字节)";
                    lblFileStatus.ForeColor = C_TEXT;
                }
                catch (Exception ex)
                {
                    Dlg.Msg(this, "打开文件失败：" + ex.Message, "错误", true);
                }
            }
        }

        private void StartFileSend()
        {
            if (port == null || !port.IsOpen)
            {
                Dlg.Msg(this, "串口未打开，无法发送文件。", "提示");
                return;
            }
            if (sendStream == null)
            {
                Dlg.Msg(this, "请先点击“打开文件”选择要发送的文件。", "提示");
                return;
            }
            int ms = ParseInt(tbTimerMs.Text, 100);
            if (ms < 1) ms = 100;
            if (ms > 60000) ms = 60000;
            fileTimer.Interval = ms;
            fileTimer.Start();
            lblFileStatus.Text = "正在发送: " + Path.GetFileName(sendFilePath);
            lblFileStatus.ForeColor = C_GREEN;
        }

        private void FileSendTick()
        {
            if (port == null || !port.IsOpen || sendStream == null)
            {
                StopFileSend("已停止");
                return;
            }
            try
            {
                byte[] chunk = new byte[512];
                int read = sendStream.Read(chunk, 0, chunk.Length);
                if (read <= 0)
                {
                    StopFileSend("发送完成");
                    return;
                }
                port.Write(chunk, 0, read);
                System.Threading.Interlocked.Add(ref txTotal, read);
                UpdateCounts();
                lblFileStatus.Text = "正在发送: " + Path.GetFileName(sendFilePath) + "  " + sendStream.Position + "/" + sendStream.Length;
            }
            catch (Exception ex)
            {
                StopFileSend("发送出错");
                Dlg.Msg(this, "文件发送失败：" + ex.Message, "错误", true);
            }
        }

        private void StopFileSend(string stateText)
        {
            fileTimer.Stop();
            if (sendStream != null)
            {
                try { sendStream.Close(); } catch { }
                sendStream = null;
            }
            if (stateText == "发送完成")
            {
                lblFileStatus.Text = "文件发送完成: " + Path.GetFileName(sendFilePath);
                lblFileStatus.ForeColor = C_GREEN;
            }
            else if (stateText == "未开始")
            {
                lblFileStatus.Text = "文件发送: 未开始";
                lblFileStatus.ForeColor = C_MUTED;
            }
            else
            {
                lblFileStatus.Text = "文件发送: " + stateText;
                lblFileStatus.ForeColor = C_MUTED;
            }
        }

        // ================= 接收存文件 / 保存数据 =================

        private bool suppressRxFileEvent;

        private void ToggleRxFile()
        {
            if (suppressRxFileEvent) return;
            if (ckSaveFile.Checked)
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = "接收数据保存到的文件";
                    dlg.Filter = "数据文件 (*.bin)|*.bin|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                    dlg.FileName = "rx_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bin";
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                    {
                        suppressRxFileEvent = true;
                        ckSaveFile.Checked = false;
                        suppressRxFileEvent = false;
                        return;
                    }
                    try
                    {
                        rxFileStream = new FileStream(dlg.FileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                    }
                    catch (Exception ex)
                    {
                        Dlg.Msg(this, "创建接收文件失败：" + ex.Message, "错误", true);
                        suppressRxFileEvent = true;
                        ckSaveFile.Checked = false;
                        suppressRxFileEvent = false;
                    }
                }
            }
            else
            {
                if (rxFileStream != null)
                {
                    try { rxFileStream.Close(); } catch { }
                    rxFileStream = null;
                }
            }
        }

        private void SaveRxData()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "保存接收窗口数据";
                dlg.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dlg.FileName = "rx_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, rxBox.Text, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Dlg.Msg(this, "保存失败：" + ex.Message, "错误", true);
                }
            }
        }

        // ================= 更多设置 / 帮助 =================

        private void ShowMoreSettings()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "串口参数";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(330, 206);
                dlg.BackColor = Theme.BG;
                dlg.ForeColor = C_TEXT;
                dlg.Font = F_UI;
                dlg.Icon = Theme.AppIcon;
                dlg.Paint += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    using (SolidBrush b = new SolidBrush(Theme.BG))
                        g.FillRectangle(b, 0, 0, 330, 206);
                };
                dlg.Load += (s, e) => UI.Dark(dlg);
                UI.AnimateIn(dlg, 200, 10);

                Label cap = new Label();
                cap.Text = "串口参数（下次打开时生效）";
                cap.Font = Theme.UI_BOLD;
                cap.ForeColor = Theme.TEXT;
                cap.BackColor = Color.Transparent;
                cap.AutoSize = true;
                cap.Location = new Point(24, 18);
                dlg.Controls.Add(cap);

                GlassCombo cbData = MkCombo(152, false);
                cbData.Left = 132; cbData.Top = 52;
                cbData.Items.AddRange(new string[] { "8", "7", "6", "5" });
                cbData.SelectedIndex = moreDataIdx;
                GlassCombo cbParity = MkCombo(152, false);
                cbParity.Left = 132; cbParity.Top = 88;
                cbParity.Items.AddRange(new string[] { "None", "Odd", "Even", "Mark", "Space" });
                cbParity.SelectedIndex = moreParityIdx;
                GlassCombo cbStop = MkCombo(152, false);
                cbStop.Left = 132; cbStop.Top = 124;
                cbStop.Items.AddRange(new string[] { "1", "1.5", "2" });
                cbStop.SelectedIndex = moreStopIdx;

                Label l1 = MkLabel("数据位", C_MUTED, false); l1.Left = 28; l1.Top = 58; l1.Margin = Padding.Empty;
                Label l2 = MkLabel("校验位", C_MUTED, false); l2.Left = 28; l2.Top = 94; l2.Margin = Padding.Empty;
                Label l3 = MkLabel("停止位", C_MUTED, false); l3.Left = 28; l3.Top = 130; l3.Margin = Padding.Empty;
                dlg.Controls.Add(l1); dlg.Controls.Add(l2); dlg.Controls.Add(l3);
                dlg.Controls.Add(cbData); dlg.Controls.Add(cbParity); dlg.Controls.Add(cbStop);

                GlassButton ok = MkBtn("确定", GlassButton.Style.Accent, 94);
                ok.Height = 34; ok.Bold = true; ok.Radius = 8;
                ok.Left = 100; ok.Top = 160; ok.DialogResult = DialogResult.OK;
                GlassButton cancel = MkBtn("取消", GlassButton.Style.Neutral, 94);
                cancel.Height = 34; cancel.Radius = 8;
                cancel.Left = 204; cancel.Top = 160; cancel.DialogResult = DialogResult.Cancel;
                dlg.Controls.Add(ok); dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    moreDataIdx = cbData.SelectedIndex;
                    moreParityIdx = cbParity.SelectedIndex;
                    moreStopIdx = cbStop.SelectedIndex;
                }
            }
        }

        private void ShowHelp()
        {
            Dlg.Msg(this,
                "1. 顶栏选择端口与波特率，点击“打开串口”。\n" +
                "2. 接收区显示收到的数据；勾选 HEX 显示 以十六进制查看。\n" +
                "3. “时间戳分包”按超时时间把连续数据分成一包并加时间前缀。\n" +
                "4. 发送区输入内容；勾选 HEX 发送 时按十六进制解析（如 31 32 或 0x3132）。\n" +
                "5. 勾选“定时发送”后按设定间隔重复发送发送区内容。\n" +
                "6. “第 N 字节 至 末尾/头部/两端 加校验”在发送数据末尾追加 1 字节校验。\n" +
                "7. “接收存文件”把原始接收字节实时写入文件；“保存数据”导出接收窗口文本。\n" +
                "8. “打开文件”+“发送文件”按定时间隔分块发送文件原始字节；“校验工具”可计算和/异或/CRC16。",
                "使用说明", false, 580);
        }

        // ================= 参数保存 =================

        private string SettingsPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serial_settings.ini");
        }

        private void SaveSettings()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("port=" + cbPort.Text);
                sb.AppendLine("baud=" + cbBaud.Text);
                sb.AppendLine("databits=" + moreDataIdx);
                sb.AppendLine("parity=" + moreParityIdx);
                sb.AppendLine("stopbits=" + moreStopIdx);
                sb.AppendLine("hexshow=" + (ckHexShow.Checked ? 1 : 0));
                sb.AppendLine("hexsend=" + (ckHexSend.Checked ? 1 : 0));
                sb.AppendLine("timerms=" + tbTimerMs.Text);
                sb.AppendLine("timeout=" + tbTimeout.Text);
                sb.AppendLine("newline=" + (ckNewline.Checked ? 1 : 0));
                sb.AppendLine("timestamp=" + (ckTimestamp.Checked ? 1 : 0));
                sb.AppendLine("topos=" + cbToPos.SelectedIndex);
                sb.AppendLine("checksum=" + cbChecksum.SelectedIndex);
                sb.AppendLine("frombyte=" + ParseInt(tbFromByte.Text, 1));
                File.WriteAllText(SettingsPath(), sb.ToString(), Encoding.UTF8);
                Dlg.Msg(this, "参数已保存到程序目录下的 serial_settings.ini。", "已保存");
            }
            catch (Exception ex)
            {
                Dlg.Msg(this, "保存参数失败：" + ex.Message, "错误", true);
            }
        }

        private void LoadSettings()
        {
            try
            {
                string path = SettingsPath();
                if (!File.Exists(path)) return;
                Dictionary<string, string> kv = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0) kv[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
                string v;
                if (kv.TryGetValue("port", out v) && v.Length > 0) cbPort.Text = v;
                if (kv.TryGetValue("baud", out v) && v.Length > 0) cbBaud.Text = v;
                if (kv.TryGetValue("databits", out v)) moreDataIdx = ClampInt(v, 0, 3, moreDataIdx);
                if (kv.TryGetValue("parity", out v)) moreParityIdx = ClampInt(v, 0, 4, moreParityIdx);
                if (kv.TryGetValue("stopbits", out v)) moreStopIdx = ClampInt(v, 0, 2, moreStopIdx);
                if (kv.TryGetValue("hexshow", out v)) ckHexShow.Checked = v == "1";
                if (kv.TryGetValue("hexsend", out v)) ckHexSend.Checked = v == "1";
                if (kv.TryGetValue("timerms", out v) && v.Length > 0) tbTimerMs.Text = v;
                if (kv.TryGetValue("timeout", out v) && v.Length > 0) tbTimeout.Text = v;
                if (kv.TryGetValue("newline", out v)) ckNewline.Checked = v == "1";
                if (kv.TryGetValue("timestamp", out v)) ckTimestamp.Checked = v == "1";
                if (kv.TryGetValue("topos", out v)) cbToPos.SelectedIndex = ClampInt(v, 0, 2, 0);
                if (kv.TryGetValue("checksum", out v)) cbChecksum.SelectedIndex = ClampInt(v, 0, 2, 0);
                if (kv.TryGetValue("frombyte", out v)) tbFromByte.Text = ClampInt(v, 1, 4096, 1).ToString();
            }
            catch { }
        }

        private static int ClampInt(string s, int min, int max, int def)
        {
            int r;
            if (!int.TryParse(s, out r)) return def;
            if (r < min) return min;
            if (r > max) return max;
            return r;
        }

        private static int ParseInt(string s, int def)
        {
            int r;
            if (!int.TryParse(s, out r)) return def;
            return r;
        }

        // ================= 工具函数 =================

        public static string BytesToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
                sb.Append(' ');
            }
            return sb.ToString().TrimEnd();
        }

        public static byte[] ParseHex(string s)
        {
            StringBuilder sb = new StringBuilder();
            string upper = s.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')) sb.Append(c);
                else if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ',' || c == '-') continue;
                else if (c == 'X') continue;
                else throw new FormatException("包含非法字符：“" + c + "”。HEX 模式只允许 0-9、A-F 及空格分隔。");
            }
            if (sb.Length % 2 != 0) throw new FormatException("HEX 字符个数必须为偶数（每两个字符一个字节）。");
            byte[] ret = new byte[sb.Length / 2];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = Convert.ToByte(sb.ToString(i * 2, 2), 16);
            return ret;
        }

        public static byte ComputeChecksum(byte[] data, int off, int cnt, int type)
        {
            if (cnt < 0) cnt = 0;
            if (off < 0) off = 0;
            if (off + cnt > data.Length) cnt = data.Length - off;
            if (type == 1)
            {
                int sum = 0;
                for (int i = off; i < off + cnt; i++) sum += data[i];
                return (byte)(sum & 0xFF);
            }
            byte xor = 0;
            for (int i = off; i < off + cnt; i++) xor ^= data[i];
            return xor;
        }

        public static ushort Crc16Modbus(byte[] data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int k = 0; k < 8; k++)
                {
                    if ((crc & 0x0001) != 0) crc = (ushort)((crc >> 1) ^ 0xA001);
                    else crc = (ushort)(crc >> 1);
                }
            }
            return crc;
        }

        private void Cleanup()
        {
            try { rxIdleTimer.Stop(); } catch { }
            try { txTimer.Stop(); } catch { }
            try { fileTimer.Stop(); } catch { }
            try { uiApplyTimer.Stop(); } catch { }
            if (rxFileStream != null) { try { rxFileStream.Close(); } catch { } rxFileStream = null; }
            if (sendStream != null) { try { sendStream.Close(); } catch { } sendStream = null; }
            ClosePort();
        }
    }

    /// 玻璃小输入框外壳
    internal class InputShell : Panel
    {
        public Control Inner;
        private bool hover = false;

        public InputShell()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.IBeam;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            Graphics g = e.Graphics;
            UI.HiQuality(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            bool focus = Inner != null && Inner.Focused;
            using (GraphicsPath path = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, 7f))
            {
                // 简约风格：纯色填充 + 细边框，焦点为蓝色
                using (SolidBrush b = new SolidBrush(Theme.INPUT)) g.FillPath(b, path);
                Color edge = focus ? Theme.BLUE : (hover ? Theme.MUTED : Theme.BORDER);
                using (GraphicsPath p2 = UI.RoundRectF(r.X, r.Y, r.Width, r.Height, 7f))
                using (Pen pen = new Pen(edge, focus ? 1.5f : 1.1f)) g.DrawPath(pen, p2);
            }
        }
    }

    public class ChecksumForm : Form
    {
        private DarkTextBox tbInput;
        private TextBox tbOutput;

        public ChecksumForm()
        {
            Text = "校验和计算器";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(492, 306);
            BackColor = Theme.BG;
            ForeColor = Theme.TEXT;
            Font = Theme.UI;
            Icon = Theme.AppIcon;
            DoubleBuffered = true;
            Load += (s, e) => UI.Dark(this);
            UI.AnimateIn(this, 200, 10);

            Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(Theme.BG))
                    g.FillRectangle(b, 0, 0, Width, Height);
            };

            Label cap = new Label();
            cap.Text = "校验和计算器";
            cap.Font = Theme.UI_BOLD;
            cap.ForeColor = Theme.BLUE_HI;
            cap.BackColor = Color.Transparent;
            cap.AutoSize = true;
            cap.Location = new Point(24, 18);
            Controls.Add(cap);

            tbInput = new DarkTextBox();
            tbInput.Font = new Font("Consolas", 10.5F);
            Controls.Add(WrapBox(tbInput, 24, 46, 444, 64));

            GlassButton btn = new GlassButton();
            btn.Text = "计算";
            btn.Kind = GlassButton.Style.Accent;
            btn.Bold = true;
            btn.Radius = 8;
            btn.Size = new Size(100, 34);
            btn.Location = new Point(24, 122);
            btn.UseBackdrop = false;
            btn.Click += (s, e) => Calc();
            Controls.Add(btn);

            tbOutput = new TextBox();
            tbOutput.Multiline = true;
            tbOutput.ReadOnly = true;
            tbOutput.BorderStyle = BorderStyle.None;
            tbOutput.ScrollBars = ScrollBars.None;
            tbOutput.BackColor = Theme.INPUT;
            tbOutput.ForeColor = Theme.TEXT;
            tbOutput.Font = new Font("Consolas", 10.5F);
            tbOutput.Dock = DockStyle.Fill;
            Controls.Add(WrapBox(tbOutput, 24, 168, 444, 110));

            Label hint = new Label
            {
                Text = "输入 HEX 数据（可含空格，支持 0x 前缀）",
                Left = 24,
                Top = 284,
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Theme.MUTED
            };
            Controls.Add(hint);
            AcceptButton = btn;
        }

        private static Panel WrapBox(Control box, int x, int y, int w, int h)
        {
            GlassWell well = new GlassWell();
            well.Location = new Point(x, y);
            well.Size = new Size(w, h);
            well.Radius = 12;
            well.Padding = new Padding(10, 7, 4, 7);
            box.Dock = DockStyle.Fill;
            well.Controls.Add(box);
            return well;
        }

        private void Calc()
        {
            byte[] data;
            try
            {
                data = MainForm.ParseHex(tbInput.Text);
            }
            catch (FormatException ex)
            {
                tbOutput.Text = "输入错误：" + ex.Message;
                return;
            }
            if (data.Length == 0) { tbOutput.Text = "无有效数据。"; return; }
            int sum = 0;
            foreach (byte b in data) sum += b;
            byte xor = MainForm.ComputeChecksum(data, 0, data.Length, 2);
            ushort crc = MainForm.Crc16Modbus(data);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("字节数: " + data.Length);
            sb.AppendLine("字节和(mod 256): 0x" + (sum & 0xFF).ToString("X2") + "   累加和(16位): 0x" + (sum & 0xFFFF).ToString("X4"));
            sb.AppendLine("异或校验: 0x" + xor.ToString("X2"));
            sb.AppendLine("CRC16-Modbus: 0x" + crc.ToString("X4") + "  (低字节在前: " + (crc & 0xFF).ToString("X2") + " " + (crc >> 8).ToString("X2") + ")");
            tbOutput.Text = sb.ToString();
        }
    }
}
