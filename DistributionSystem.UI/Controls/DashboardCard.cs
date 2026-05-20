using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DistributionSystem.UI.Controls
{
    public class DashboardCard : UserControl
    {
        // ?? Properties ????????????????????????????????????????
        private string _cardTitle = "ÇáÚäæÇä";
        private string _cardValue = "0";
        private string _cardSubtitle = "";
        private string _iconText = "??";
        private Color _cardColor = ColorTranslator.FromHtml("#2563EB");

        public string CardTitle
        {
            get => _cardTitle;
            set { _cardTitle = value; Invalidate(); }
        }
        public string CardValue
        {
            get => _cardValue;
            set { _cardValue = value; Invalidate(); }
        }
        public string CardSubtitle
        {
            get => _cardSubtitle;
            set { _cardSubtitle = value; Invalidate(); }
        }
        public string IconText
        {
            get => _iconText;
            set { _iconText = value; Invalidate(); }
        }
        public Color CardColor
        {
            get => _cardColor;
            set { _cardColor = value; Invalidate(); }
        }

        // ?? Hover state ???????????????????????????????????????
        private bool _hovered = false;

        public DashboardCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            MinimumSize = new Size(180, 110);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int W = Width, H = Height;
            var rc = new Rectangle(0, 0, W - 1, H - 1);

            // ?? Shadow (offset rect) ??????????????????????????
            if (!_hovered)
            {
                var shadowRc = new Rectangle(3, 4, W - 6, H - 4);
                using (var sb = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                using (var sp = RoundPath(shadowRc, 16))
                    g.FillPath(sb, sp);
            }

            // ?? Card background ???????????????????????????????
            Color bg = _hovered ? Color.FromArgb(248, 250, 252) : Color.White;
            using (var path = RoundPath(rc, 16))
            using (var br = new SolidBrush(bg))
                g.FillPath(br, path);

            // ?? Left accent strip ?????????????????????????????
            var stripRc = new Rectangle(0, 16, 5, H - 32);
            using (var br = new LinearGradientBrush(stripRc, _cardColor, Lighten(_cardColor, 60), LinearGradientMode.Vertical))
            using (var path = RoundPath(stripRc, 3))
                g.FillPath(br, path);

            // ?? Border ???????????????????????????????????????
            Color borderColor = _hovered
                ? Color.FromArgb(80, _cardColor.R, _cardColor.G, _cardColor.B)
                : ColorTranslator.FromHtml("#E2E8F0");
            using (var pen = new Pen(borderColor, _hovered ? 1.8f : 1.2f))
            using (var path = RoundPath(rc, 16))
                g.DrawPath(pen, path);

            // ?? Icon circle (top-left corner) ?????????????????
            int iconSize = 44;
            int iconX = 14;
            int iconY = (H - iconSize) / 2;
            var iconRc = new Rectangle(iconX, iconY, iconSize, iconSize);
            Color iconBg = Color.FromArgb(28, _cardColor.R, _cardColor.G, _cardColor.B);
            using (var br = new SolidBrush(iconBg))
                g.FillEllipse(br, iconRc);
            // Draw color dot instead of emoji (no rendering issues)
            using (var br = new SolidBrush(_cardColor))
                g.FillEllipse(br,
                    iconX + (iconSize - 16) / 2,
                    iconY + (iconSize - 16) / 2,
                    16, 16);

            // ?? Text area — centered in remaining space ????????
            int textX = iconX + iconSize + 10;
            int textW = W - textX - 12;

            var sfCenter = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };

            // Value (big number) — top half
            using (var f = new Font("Cairo", 24F, FontStyle.Bold))
            using (var br = new SolidBrush(_cardColor))
                g.DrawString(_cardValue, f, br,
                    new RectangleF(textX, 8, textW, H / 2 - 4),
                    sfCenter);

            // Title — middle
            using (var f = new Font("Cairo", 10.5F, FontStyle.Bold))
            using (var br = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(_cardTitle, f, br,
                    new RectangleF(textX, H / 2, textW, 24),
                    sfCenter);

            // Subtitle — bottom
            if (!string.IsNullOrEmpty(_cardSubtitle))
                using (var f = new Font("Cairo", 8.5F))
                using (var br = new SolidBrush(ColorTranslator.FromHtml("#94A3B8")))
                    g.DrawString(_cardSubtitle, f, br,
                        new RectangleF(textX, H / 2 + 24, textW, 20),
                        sfCenter);
        }

        private static GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Lighten(Color c, int amount)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + amount),
                Math.Min(255, c.G + amount),
                Math.Min(255, c.B + amount));
        }
    }
}