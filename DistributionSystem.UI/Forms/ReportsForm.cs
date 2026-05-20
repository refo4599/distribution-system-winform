using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public partial class ReportsForm : Form
    {
        private readonly VehicleService _vehicleSvc = new VehicleService();
        private readonly TreasuryService _treasurySvc = new TreasuryService();
        private readonly PdfReportService _vehiclePdf = new PdfReportService();
        private readonly TreasuryPdfService _treasuryPdf = new TreasuryPdfService();
        private readonly SalesInvoiceService _invoiceSvc = new SalesInvoiceService();
        private readonly CustomerService _customerSvc = new CustomerService();
        private readonly SalesInvoicePdfService _invPdfSvc = new SalesInvoicePdfService();

        private static readonly Color ThemeDark = ColorTranslator.FromHtml("#1a2f5e");
        private static readonly Color ThemeMid = ColorTranslator.FromHtml("#1565c0");
        private static readonly Color ThemeAccent = ColorTranslator.FromHtml("#4E73DF");

        private Bitmap _bannerCache;

        public ReportsForm()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            BuildLayout();
        }

        private void BuildLayout()
        {
            SuspendLayout();
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5"); Padding = new Padding(0);
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildBanner(), 0, 0);
            root.Controls.Add(BuildCardsArea(), 0, 1);
            Controls.Add(root); root.BringToFront();
            EnableDbAll(this); ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  BANNER
        // ??????????????????????????????????????????????????????
        private Panel BuildBanner()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 88, BackColor = Color.Transparent };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            banner.Paint += (s, e) =>
            {
                if (_bannerCache == null ||
                    _bannerCache.Width != banner.Width ||
                    _bannerCache.Height != banner.Height)
                {
                    _bannerCache?.Dispose();
                    if (banner.Width <= 0 || banner.Height <= 0) return;
                    _bannerCache = new Bitmap(banner.Width, banner.Height);
                    using (var g = Graphics.FromImage(_bannerCache))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                        using (var br = new LinearGradientBrush(rc, ThemeDark, ThemeMid, LinearGradientMode.Horizontal))
                        using (var path = RoundPath(rc, 16))
                            g.FillPath(br, path);
                        using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                            for (int x = 10; x < banner.Width; x += 22)
                                for (int y = 8; y < banner.Height; y += 22)
                                    g.FillEllipse(dot, x, y, 2, 2);
                        using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                        {
                            g.FillEllipse(cb, banner.Width - 130, -50, 220, 220);
                            g.FillEllipse(cb, banner.Width - 30, 20, 160, 160);
                        }
                        using (var tf = new Font("Cairo", 22F, FontStyle.Bold))
                        using (var tb = new SolidBrush(Color.White))
                        {
                            var sz = g.MeasureString("«· ﬁ«—Ì—", tf);
                            g.DrawString("«· ﬁ«—Ì—", tf, tb, banner.Width - sz.Width - 24, 8);
                        }
                        using (var sf = new Font("Cairo", 9.5F))
                        using (var sb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                            g.DrawString(" ﬁ«—Ì— «·⁄„·«¡  ï  «·”Ì«—«   ï  «·Œ“‰…  ï  «·›Ê« Ì—",
                                sf, sb, banner.Width - 320, 46);
                        using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                            g.FillRectangle(b1, banner.Width - 42, 44, 38, 3);
                        using (var b2 = new SolidBrush(Color.FromArgb(120, 100, 181, 246)))
                            g.FillRectangle(b2, banner.Width - 60, 44, 14, 3);
                    }
                }
                e.Graphics.DrawImage(_bannerCache, 0, 0);
            };
            banner.Resize += (s, e) => { _bannerCache?.Dispose(); _bannerCache = null; };
            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  CARDS AREA  ó ·ÊÃÊ ›Êﬁ «·ﬂ—Ê 
        // ??????????????????????????????????????????????????????
        private Panel BuildCardsArea()
        {
            var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // ?? Œ·›Ì… ??????????????????????????????????????????
            wrapper.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = wrapper.Width, H = wrapper.Height;

                using (var br = new SolidBrush(Color.FromArgb(55, 26, 47, 94)))
                {
                    g.FillEllipse(br, -80, -80, 320, 320);
                    g.FillEllipse(br, W - 180, H - 180, 300, 300);
                    g.FillEllipse(br, W / 2 - 100, H / 2 - 80, 200, 200);
                }
                using (var pen = new Pen(Color.FromArgb(80, 26, 47, 94), 2f))
                {
                    DrawDocIcon(g, pen, 60, H / 2 - 60, 52, 66);
                    DrawDocIcon(g, pen, W - 130, 80, 44, 56);
                    DrawDocIcon(g, pen, W - 80, H / 2 + 20, 36, 46);
                    DrawDocIcon(g, pen, 30, H - 130, 40, 50);
                }
                using (var pen = new Pen(Color.FromArgb(90, 21, 101, 192), 2.5f))
                {
                    int bx = W - 200, by2 = H - 100;
                    int[] heights = { 28, 42, 18, 36, 24 };
                    int bw2 = 12, gap2 = 6;
                    foreach (var bh2 in heights)
                    {
                        using (var br = new SolidBrush(Color.FromArgb(40, 21, 101, 192)))
                            g.FillRectangle(br, bx, by2 - bh2, bw2, bh2);
                        g.DrawRectangle(pen, bx, by2 - bh2, bw2, bh2);
                        bx += bw2 + gap2;
                    }
                    g.DrawLine(pen, W - 204, by2, W - 204 + (12 + 6) * 5, by2);
                }
                using (var pen = new Pen(Color.FromArgb(85, 26, 47, 94), 2f))
                {
                    int pr = 40, px2 = 100, py2 = 60;
                    g.DrawEllipse(pen, px2, py2, pr * 2, pr * 2);
                    using (var br1 = new SolidBrush(Color.FromArgb(45, 26, 47, 94)))
                        g.FillPie(br1, px2, py2, pr * 2, pr * 2, 0, 120);
                    using (var br2 = new SolidBrush(Color.FromArgb(30, 21, 101, 192)))
                        g.FillPie(br2, px2, py2, pr * 2, pr * 2, 120, 140);
                    g.DrawLine(pen, px2 + pr, py2 + pr, px2 + pr + pr, py2 + pr);
                    g.DrawLine(pen, px2 + pr, py2 + pr,
                        px2 + pr + (int)(pr * Math.Cos(-Math.PI / 3)),
                        py2 + pr + (int)(pr * Math.Sin(-Math.PI / 3)));
                    g.DrawLine(pen, px2 + pr, py2 + pr,
                        px2 + pr + (int)(pr * Math.Cos(Math.PI / 3)),
                        py2 + pr + (int)(pr * Math.Sin(Math.PI / 3)));
                }
                using (var pen = new Pen(Color.FromArgb(75, 21, 101, 192), 2.5f))
                {
                    var pts = new Point[]
                    {
                        new Point(W/2 - 80,  H - 60),  new Point(W/2 - 40,  H - 90),
                        new Point(W/2,       H - 70),  new Point(W/2 + 40,  H - 110),
                        new Point(W/2 + 80,  H - 85),  new Point(W/2 + 120, H - 50),
                    };
                    g.DrawCurve(pen, pts, 0.4f);
                    using (var br = new SolidBrush(Color.FromArgb(90, 21, 101, 192)))
                        foreach (var pt in pts)
                            g.FillEllipse(br, pt.X - 5, pt.Y - 5, 10, 10);
                }
                using (var f = new Font("Cairo", 36F, FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(35, 26, 47, 94)))
                { g.DrawString("$", f, br, W - 260, 20); g.DrawString("%", f, br, 20, H / 2 + 40); }
                using (var f = new Font("Cairo", 20F, FontStyle.Bold))
                using (var br = new SolidBrush(Color.FromArgb(40, 26, 47, 94)))
                { g.DrawString("?", f, br, W / 2 - 30, 30); g.DrawString("?", f, br, 80, H - 60); }
            };

            // ?? «··ÊÃÊ ??????????????????????????????????????????
            var logoPnl = new Panel
            {
                BackColor = Color.Transparent,
                Height = 155,
                Width = 300    // ⁄—÷ „ƒﬁ  ó ”Ì Õœœ ›Ì Resize
            };
            logoPnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                int W = logoPnl.Width;
                float cx = W / 2f;
                float cy = 68f;
                float r = 58f;

                // Ÿ· Œ›Ì›
                using (var sh = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                    g.FillEllipse(sh, cx - r + 4, cy - r + 4, r * 2, r * 2);

                // «·œ«Ì—… «·œ«ﬂ‰…
                using (var br = new SolidBrush(ThemeDark))
                    g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);

                // «·»Ê—œ— «·√“—ﬁ «·„ Ê”ÿ
                using (var pen = new Pen(ThemeMid, 2.5f))
                    g.DrawEllipse(pen, cx - r - 3, cy - r - 3, (r + 3) * 2, (r + 3) * 2);

                // ‰’ "»’Ê’" ó „ﬁ«” Ê„— ﬂ“
                using (var f = new Font("Cairo", 26f, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString("»’Ê’", f);
                    g.DrawString("»’Ê’", f, b,
                        cx - sz.Width / 2f,
                        cy - sz.Height / 2f);
                }

                // "‘—ﬂ… »’Ê’ ·· Ê“Ì⁄"  Õ  «·œ«Ì—…
                using (var f = new Font("Cairo", 11f, FontStyle.Bold))
                using (var b = new SolidBrush(ThemeDark))
                {
                    SizeF sz = g.MeasureString("‘—ﬂ… »’Ê’ ·· Ê“Ì⁄", f);
                    g.DrawString("‘—ﬂ… »’Ê’ ·· Ê“Ì⁄", f, b,
                        cx - sz.Width / 2f,
                        cy + r + 6f);
                }
            };

            // ?? «·ﬂ—Ê  ?????????????????????????????????????????
            var center = new Panel { BackColor = Color.Transparent };
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            row.Controls.Add(BuildReportCard(" ﬁ—Ì— «·⁄„Ì·", "«Œ — ⁄„Ì· ·«” Œ—«Ã  ﬁ—Ì— „›’· »«·›Ê« Ì— Ê«·Ê«—œ« ", "#4E73DF", "#1e3a6e", "person", () => NavigateToCustomerReport()), 0, 0);
            row.Controls.Add(BuildReportCard(" ﬁ—Ì— «·”Ì«—…", "«Œ — ”Ì«—… Ê‘Â— ·«” Œ—«Ã  ﬁ—Ì— ‘Â—Ì „›’· »√Ê«„— «·’—›", "#10B981", "#065F46", "truck", () => ShowVehicleReportPopup()), 1, 0);
            row.Controls.Add(BuildReportCard(" ﬁ—Ì— «·Œ“‰…", "«Œ — ÌÊ„ ·«” Œ—«Ã  ﬁ—Ì— „›’· »Õ—ﬂ«  «·Œ“‰… Ê«·—’Ìœ", "#F59E0B", "#92400E", "treasury", () => ShowTreasuryReportPopup()), 2, 0);
            row.Controls.Add(BuildReportCard(" ﬁ—Ì— «·›« Ê—…", "«Œ — ⁄„Ì· · Õ„Ì· ›Ê« Ì—Â »«· ›«’Ì· «·ﬂ«„·…", "#7C3AED", "#4C1D95", "invoice", () => ShowInvoiceReportPopup()), 3, 0);

            center.Controls.Add(row);

            // ?? Resize:  „Ê÷⁄ «··ÊÃÊ Ê«·ﬂ—Ê  ??????????????????
            wrapper.Resize += (s, e) =>
            {
                const int cardW = 230;
                const int cardH = 300;
                const int gap = 14;
                const int logoH = 155;
                const int spacing = 12;    // „”«›… »Ì‰ «··ÊÃÊ Ê«·ﬂ—Ê 

                int totalW = cardW * 4 + gap * 3;
                int totalH = logoH + spacing + cardH;
                int x = Math.Max(16, (wrapper.Width - totalW) / 2);
                int y = Math.Max(16, (wrapper.Height - totalH) / 2);

                // «··ÊÃÊ ó ›Ì «·„‰ ’› √›ﬁÌ«
                logoPnl.SetBounds(x, y, totalW, logoH);

                // «·ﬂ—Ê  ó  Õ  «··ÊÃÊ „»«‘—…
                center.SetBounds(x, y + logoH + spacing, totalW, cardH);

                wrapper.Invalidate();
            };

            wrapper.Controls.Add(center);
            wrapper.Controls.Add(logoPnl);   // Ìı÷«› »⁄œ center ⁄‘«‰ ÌŸÂ— ›ÊﬁÂ
            return wrapper;
        }

        // ??????????????????????????????????????????????????????
        //  REPORT CARD
        // ??????????????????????????????????????????????????????
        private Guna2Panel BuildReportCard(
            string title, string desc,
            string accentHex, string darkHex,
            string iconType, Action onClick)
        {
            var accent = ColorTranslator.FromHtml(accentHex);

            var card = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = 14,
                BorderThickness = 0,
                Margin = new Padding(7),
                Cursor = Cursors.Hand
            };
            card.ShadowDecoration.Enabled = true;
            card.ShadowDecoration.Depth = 7;
            card.ShadowDecoration.Color = Color.FromArgb(20, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };
            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.Transparent, Padding = new Padding(14, 4, 14, 10) };
            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Cursor = Cursors.Hand };

            var iconPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            iconPanel.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int sz = 32, ix = (iconPanel.Width - sz) / 2, iy = (iconPanel.Height - sz) / 2;
                int cx = ix + sz / 2, cy = iy + sz / 2;
                using (var br = new SolidBrush(Color.FromArgb(22, accent)))
                    g.FillEllipse(br, ix, iy, sz, sz);
                using (var pen2 = new Pen(accent, 1.3f))
                    g.DrawEllipse(pen2, ix + 1, iy + 1, sz - 2, sz - 2);
                using (var p = new Pen(accent, 1.8f))
                using (var brF = new SolidBrush(accent))
                {
                    if (iconType == "person")
                    {
                        g.DrawEllipse(p, cx - 6, cy - 12, 12, 12);
                        using (var path2 = new GraphicsPath())
                        { path2.AddArc(cx - 9, cy + 2, 18, 10, 180, 180); g.DrawPath(p, path2); }
                    }
                    else if (iconType == "truck")
                    {
                        g.DrawRectangle(p, cx - 10, cy - 5, 15, 9);
                        g.DrawRectangle(p, cx + 5, cy - 3, 7, 7);
                        g.FillEllipse(brF, cx - 8, cy + 4, 6, 6);
                        g.FillEllipse(brF, cx + 5, cy + 4, 6, 6);
                        g.DrawRectangle(p, cx + 6, cy - 2, 4, 3);
                    }
                    else if (iconType == "invoice")
                    {
                        g.DrawRectangle(p, cx - 9, cy - 11, 17, 22);
                        g.DrawLine(p, cx - 5, cy - 6, cx + 3, cy - 6);
                        g.DrawLine(p, cx - 5, cy - 1, cx + 3, cy - 1);
                        g.DrawLine(p, cx - 5, cy + 4, cx, cy + 4);
                        g.DrawLine(p, cx + 2, cy - 11, cx + 8, cy - 5);
                        g.DrawLine(p, cx + 2, cy - 11, cx + 2, cy - 5);
                        g.DrawLine(p, cx + 2, cy - 5, cx + 8, cy - 5);
                    }
                    else
                    {
                        g.DrawRectangle(p, cx - 9, cy - 7, 18, 14);
                        g.DrawEllipse(p, cx - 3, cy - 3, 6, 6);
                        g.FillEllipse(brF, cx - 1, cy - 1, 3, 3);
                        g.DrawLine(p, cx - 9, cy - 1, cx - 4, cy - 1);
                        g.DrawLine(p, cx + 4, cy - 1, cx + 9, cy - 1);
                    }
                }
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            var lblDesc = new Label
            {
                Text = desc,
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 8F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var btn = new Guna2Button
            {
                Text = "≈‰‘«¡ «· ﬁ—Ì—",
                FillColor = accent,
                ForeColor = Color.White,
                BorderRadius = 9,
                Font = new Font("Cairo", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill
            };
            btn.HoverState.FillColor = ColorTranslator.FromHtml(darkHex);
            btn.ShadowDecoration.Enabled = true;
            btn.ShadowDecoration.Depth = 3;
            btn.ShadowDecoration.Color = Color.FromArgb(35, accent);
            btnPanel.Controls.Add(btn);

            int ctH = 44 + 24 + 40;
            var centerTable = new TableLayoutPanel
            {
                Dock = DockStyle.None,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            centerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            centerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            centerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            centerTable.Controls.Add(iconPanel, 0, 0);
            centerTable.Controls.Add(lblTitle, 0, 1);
            centerTable.Controls.Add(lblDesc, 0, 2);

            inner.Controls.Add(centerTable);
            inner.Resize += (s, e) =>
            {
                int w = inner.Width, y = Math.Max(0, (inner.Height - ctH) / 2);
                centerTable.Size = new Size(w, ctH);
                centerTable.Location = new Point(0, y);
            };

            btn.Click += (s, e) => onClick();
            inner.Click += (s, e) => onClick();
            centerTable.Click += (s, e) => onClick();
            iconPanel.Click += (s, e) => onClick();
            lblTitle.Click += (s, e) => onClick();
            lblDesc.Click += (s, e) => onClick();
            card.Click += (s, e) => onClick();

            card.MouseEnter += (s, e) => { card.ShadowDecoration.Depth = 12; card.ShadowDecoration.Color = Color.FromArgb(30, accent); };
            card.MouseLeave += (s, e) => { card.ShadowDecoration.Depth = 7; card.ShadowDecoration.Color = Color.FromArgb(20, 0, 0, 0); };

            card.Controls.Add(inner);
            card.Controls.Add(btnPanel);
            card.Controls.Add(topBar);
            return card;
        }

        // ??????????????????????????????????????????????????????
        //  NAVIGATE
        // ??????????????????????????????????????????????????????
        private void NavigateToCustomerReport()
        {
            var mainLayout = FindParentForm<MainLayoutForm>(this);
            if (mainLayout != null) { mainLayout.LoadChildForm(new CustomerReportForm()); return; }
            new CustomerReportForm().ShowDialog(this);
        }

        private static T FindParentForm<T>(Control ctrl) where T : Form
        {
            var current = ctrl?.Parent;
            while (current != null) { if (current is T match) return match; current = current.Parent; }
            return null;
        }

        // ??????????????????????????????????????????????????????
        //  POPUP ó  ﬁ—Ì— «·”Ì«—…
        // ??????????????????????????????????????????????????????
        private void ShowVehicleReportPopup()
        {
            List<VehicleDto> vehicles = new List<VehicleDto>();
            try { vehicles = _vehicleSvc.GetAllVehicles()?.Where(v => v.IsActive).ToList() ?? new List<VehicleDto>(); } catch { }
            if (vehicles.Count == 0) { MessageBox.Show("·«  ÊÃœ ”Ì«—«  ‰‘ÿ… ›Ì «·‰Ÿ«„", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.5, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);

            int pw = 480, ph = 460;
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(pw, ph), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.No, RightToLeftLayout = false };
            pf.Location = new Point(sc.Left + (sc.Width - pw) / 2, sc.Top + (sc.Height - ph) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pw - 36, 0, 36, 36, 270, 90); rgn.AddArc(pw - 36, ph - 36, 36, 36, 0, 90); rgn.AddArc(0, ph - 36, 36, 36, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#064e3b"), ColorTranslator.FromHtml("#065F46"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White)) { var tsz = g.MeasureString(" Õ„Ì· «· ﬁ—Ì— «·‘Â—Ì", tf); g.DrawString(" Õ„Ì· «· ﬁ—Ì— «·‘Â—Ì", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString("«Œ — «·”Ì«—… Ê«·‘Â— Ê«·”‰…", sf3); g.DrawString("«Œ — «·”Ì«—… Ê«·‘Â— Ê«·”‰…", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); }
            };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX); pnlHead.Layout += (s, e) => btnX.Location = new Point(18, 18);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#065F46"), ColorTranslator.FromHtml("#D1FAE5"), LinearGradientMode.Horizontal)) pe6.Graphics.FillRectangle(br, 0, 1, footer.Width, 2); };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20, 12, 20, 8) };

            Panel MkLblPanel(string txt) { var p = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent }; p.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) { var sz2 = pe.Graphics.MeasureString(txt, f2); pe.Graphics.DrawString(txt, f2, b2, p.Width - sz2.Width - 2, p.Height - sz2.Height - 1); } }; return p; }

            Panel MkCboField(string placeholder, out ComboBox cboOut)
            {
                var cbo = new ComboBox { Height = 42, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
                cbo.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#ECFDF5") : Color.White), de.Bounds); string txt2 = cbo.GetItemText(cbo.Items[de.Index]); using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#064e3b") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(txt2, f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };
                var ov = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
                ov.Paint += (s2, pe2) => { var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, ov.Width - 1, ov.Height - 1); using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(rc2, 8)) g.FillPath(brs, path2); using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(rc2, 8)) g.DrawPath(pen2, path2); int ax = 18, ay = ov.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); } string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : placeholder; bool isPh = cbo.SelectedIndex < 0; using (var f2 = new Font("Cairo", 11F)) using (var b2 = new SolidBrush(isPh ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A"))) g.DrawString(selTxt, f2, b2, new RectangleF(36, 0, ov.Width - 52, ov.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter }); };
                cbo.SetBounds(0, 0, 400, 42); ov.Controls.Add(cbo); ov.Resize += (s2, e2) => cbo.SetBounds(0, 0, ov.Width, 42); cbo.SelectedIndexChanged += (s2, e2) => ov.Invalidate(); cboOut = cbo; return ov;
            }

            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };

            var cboVehPanel = MkCboField("«Œ — ”Ì«—…", out var cboVeh);
            var vList = new List<VehicleDto>(vehicles); vList.Insert(0, new VehicleDto { Id = 0, Name = "«Œ — ”Ì«—…" });
            cboVeh.DisplayMember = "Name"; cboVeh.ValueMember = "Id"; cboVeh.DataSource = null; cboVeh.DataSource = vList;

            string[] arabicMonths = { "Ì‰«Ì—", "›»—«Ì—", "„«—”", "√»—Ì·", "„«ÌÊ", "ÌÊ‰ÌÊ", "ÌÊ·ÌÊ", "√€”ÿ”", "”» „»—", "√ﬂ Ê»—", "‰Ê›„»—", "œÌ”„»—" };
            var cboMonthPanel = MkCboField("«Œ — «·‘Â—", out var cboMonth);
            foreach (var m in arabicMonths) cboMonth.Items.Add(m);
            cboMonth.SelectedIndex = Math.Max(0, DateTime.Now.Month - 2);

            var cboYearPanel = MkCboField("«Œ — «·”‰…", out var cboYear);
            int curYear = DateTime.Now.Year;
            for (int y = curYear - 3; y <= curYear + 1; y++) cboYear.Items.Add(y);
            cboYear.SelectedItem = curYear;

            body.SuspendLayout();
            body.Controls.Add(Sp(8)); body.Controls.Add(cboYearPanel); body.Controls.Add(MkLblPanel("«·”‰… *"));
            body.Controls.Add(Sp(8)); body.Controls.Add(cboMonthPanel); body.Controls.Add(MkLblPanel("«·‘Â— *"));
            body.Controls.Add(Sp(8)); body.Controls.Add(cboVehPanel); body.Controls.Add(MkLblPanel("«·”Ì«—… *"));
            body.Controls.Add(Sp(8)); body.ResumeLayout(true);

            var btnGen = new Guna2Button { Dock = DockStyle.Fill, Text = "≈‰‘«¡ Ê Õ„Ì· PDF", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#065F46"), ForeColor = Color.White, Font = new Font("Cairo", 12F, FontStyle.Bold), Animated = true };
            btnGen.HoverState.FillColor = ColorTranslator.FromHtml("#047857");
            btnGen.ShadowDecoration.Enabled = true; btnGen.ShadowDecoration.Depth = 4; btnGen.ShadowDecoration.Color = Color.FromArgb(40, 6, 95, 70);
            btnGen.Click += async (s, e) =>
            {
                int vId = 0; try { vId = Convert.ToInt32(cboVeh.SelectedValue); } catch { }
                if (vId == 0) { MessageBox.Show("«Œ — ”Ì«—… √Ê·«", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                int selMonth = cboMonth.SelectedIndex + 1; int selYear = cboYear.SelectedItem != null ? (int)cboYear.SelectedItem : curYear;
                var vehicle = vehicles.FirstOrDefault(v => v.Id == vId); if (vehicle == null) return;
                btnGen.Enabled = false; btnGen.Text = "Ã«—Ú «·≈‰‘«¡...";
                try
                {
                    var orders = await Task.Run(() => _vehicleSvc.GetAllDispatchOrders()?.Where(o => o.VehicleId == vId && o.CreatedAt.Month == selMonth && o.CreatedAt.Year == selYear).ToList() ?? new List<DispatchOrderDto>());
                    if (orders.Count == 0) { MessageBox.Show($"·«  ÊÃœ √Ê«„— ’—› ›Ì {arabicMonths[selMonth - 1]} {selYear}", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    var pdfBytes = await Task.Run(() => _vehiclePdf.GenerateVehicleMonthlyReport(vehicle, orders, selMonth, selYear));
                    using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ «· ﬁ—Ì— «·‘Â—Ì", Filter = "PDF|*.pdf", FileName = $" ﬁ—Ì—_{SanitizeName(vehicle.Name)}_{selYear}_{arabicMonths[selMonth - 1]}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                    { if (sfd.ShowDialog() == DialogResult.OK) { File.WriteAllBytes(sfd.FileName, pdfBytes); pf.Close(); ShowSuccessToast($" „ Õ›Ÿ «· ﬁ—Ì— ({orders.Count} √„— ’—›)"); } }
                }
                catch (Exception ex) { MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { btnGen.Enabled = true; btnGen.Text = "≈‰‘«¡ Ê Õ„Ì· PDF"; }
            };
            footer.Controls.Add(btnGen);
            pf.Controls.Add(body); pf.Controls.Add(footer); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  POPUP ó  ﬁ—Ì— «·Œ“‰…
        // ??????????????????????????????????????????????????????
        private void ShowTreasuryReportPopup()
        {
            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.5, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);

            int pw = 460, ph = 310;
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(pw, ph), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            pf.Location = new Point(sc.Left + (sc.Width - pw) / 2, sc.Top + (sc.Height - ph) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pw - 36, 0, 36, 36, 270, 90); rgn.AddArc(pw - 36, ph - 36, 36, 36, 0, 90); rgn.AddArc(0, ph - 36, 36, 36, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ThemeDark, ThemeMid, LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White)) { var tsz = g.MeasureString(" ﬁ—Ì— «·Œ“‰…", tf); g.DrawString(" ﬁ—Ì— «·Œ“‰…", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString("«Œ — «·ÌÊ„ À„ «÷€ÿ  Õ„Ì·", sf3); g.DrawString("«Œ — «·ÌÊ„ À„ «÷€ÿ  Õ„Ì·", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); }
            };
            var btnX2 = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX2.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX2.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX2); pnlHead.Layout += (s, e) => btnX2.Location = new Point(18, 18);

            var footer2 = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer2.Paint += (s6, pe6) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer2.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer2.Width, 2), ColorTranslator.FromHtml("#1565c0"), ColorTranslator.FromHtml("#DBEAFE"), LinearGradientMode.Horizontal)) pe6.Graphics.FillRectangle(br, 0, 1, footer2.Width, 2); };

            var body2 = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 16, 28, 8) };
            var pnlLblDate = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
            pnlLblDate.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var f2 = new Font("Cairo", 10F, FontStyle.Bold)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) { var sz2 = pe.Graphics.MeasureString(" «—ÌŒ «· ﬁ—Ì—", f2); pe.Graphics.DrawString(" «—ÌŒ «· ﬁ—Ì—", f2, b2, pnlLblDate.Width - sz2.Width - 2, pnlLblDate.Height - sz2.Height); } };
            var dtPicker = new DateTimePicker { Dock = DockStyle.Top, Height = 40, Format = DateTimePickerFormat.Short, Value = DateTime.Today, Font = new Font("Cairo", 11F), RightToLeft = RightToLeft.Yes };
            var pnlHint = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
            pnlHint.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; string hint = "”Ì⁄—÷ «· ﬁ—Ì— ﬂ· Õ—ﬂ«  «·Œ“‰… ›Ì «·ÌÊ„ «·„Œ «—"; using (var f2 = new Font("Cairo", 9F)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#64748B"))) { var sz2 = pe.Graphics.MeasureString(hint, f2); pe.Graphics.DrawString(hint, f2, b2, pnlHint.Width - sz2.Width - 2, (pnlHint.Height - sz2.Height) / 2); } };

            body2.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent });
            body2.Controls.Add(pnlHint);
            body2.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent });
            body2.Controls.Add(dtPicker);
            body2.Controls.Add(pnlLblDate);

            var btnGen2 = new Guna2Button { Dock = DockStyle.Fill, Text = " Õ„Ì· «· ﬁ—Ì—", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#1565c0"), ForeColor = Color.White, Font = new Font("Cairo", 12F, FontStyle.Bold), Animated = true };
            btnGen2.HoverState.FillColor = ThemeDark; btnGen2.ShadowDecoration.Enabled = true; btnGen2.ShadowDecoration.Depth = 4; btnGen2.ShadowDecoration.Color = Color.FromArgb(40, 21, 101, 192);
            btnGen2.Click += async (s, e) =>
            {
                DateTime selectedDate = dtPicker.Value.Date; btnGen2.Enabled = false; btnGen2.Text = "Ã«—Ú «· Õ÷Ì—...";
                try
                {
                    var pdfBytes = await Task.Run(() => { var summary = new TreasurySummaryDto(); decimal inbTotal = 0m, profTotal = 0m; var movements = new List<TreasuryMovementDto>(); try { summary = _treasurySvc.GetSummary(); } catch { } try { inbTotal = _treasurySvc.GetInboundTotal(); } catch { } try { profTotal = _treasurySvc.GetProfitTotal(); } catch { } try { var all = _treasurySvc.GetAllMovements(); movements = all.Where(m => m.Date.Date == selectedDate).ToList(); } catch { } summary.TotalBalance = summary.ManualBalance + summary.InvoicesRevenue - inbTotal - summary.EmployeeExpenses; return _treasuryPdf.GenerateDailyReport(summary, movements, selectedDate, inbTotal, profTotal); });
                    using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ  ﬁ—Ì— «·Œ“‰…", Filter = "PDF|*.pdf", FileName = $" ﬁ—Ì—_«·Œ“‰…_{selectedDate:yyyy-MM-dd}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                    { if (sfd.ShowDialog() == DialogResult.OK) { File.WriteAllBytes(sfd.FileName, pdfBytes); pf.Close(); ShowSuccessToast(" „ Õ›Ÿ  ﬁ—Ì— «·Œ“‰…"); } }
                }
                catch (Exception ex) { MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { btnGen2.Enabled = true; btnGen2.Text = " Õ„Ì· «· ﬁ—Ì—"; }
            };
            footer2.Controls.Add(btnGen2);
            pf.Controls.Add(body2); pf.Controls.Add(footer2); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  POPUP ó  ﬁ—Ì— «·›« Ê—…
        // ??????????????????????????????????????????????????????
        private void ShowInvoiceReportPopup()
        {
            List<CustomerDto> customers = new List<CustomerDto>(); List<SalesInvoiceDto> allInvoices = new List<SalesInvoiceDto>();
            try { allInvoices = _invoiceSvc.GetAllInvoices() ?? new List<SalesInvoiceDto>(); var custIds = allInvoices.Select(i => i.CustomerId).Distinct().ToHashSet(); customers = (_customerSvc.GetAll() ?? Enumerable.Empty<CustomerDto>()).Where(c => custIds.Contains(c.Id)).OrderBy(c => c.Name).ToList(); } catch { }
            if (customers.Count == 0) { MessageBox.Show("·«  ÊÃœ ›Ê« Ì— „”Ã·… ›Ì «·‰Ÿ«„", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.5, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);

            int pw = 480, ph = 430;
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(pw, ph), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            pf.Location = new Point(sc.Left + (sc.Width - pw) / 2, sc.Top + (sc.Height - ph) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pw - 36, 0, 36, 36, 270, 90); rgn.AddArc(pw - 36, ph - 36, 36, 36, 0, 90); rgn.AddArc(0, ph - 36, 36, 36, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#4C1D95"), ColorTranslator.FromHtml("#7C3AED"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White)) { var tsz = g.MeasureString(" ﬁ—Ì— ›« Ê—… «·⁄„Ì·", tf); g.DrawString(" ﬁ—Ì— ›« Ê—… «·⁄„Ì·", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString("«Œ — «·⁄„Ì· · Õ„Ì· ›Ê« Ì—Â", sf3); g.DrawString("«Œ — «·⁄„Ì· · Õ„Ì· ›Ê« Ì—Â", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); }
            };
            var btnXI = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnXI.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnXI.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnXI); pnlHead.Layout += (s, e) => btnXI.Location = new Point(18, 18);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#7C3AED"), ColorTranslator.FromHtml("#EDE9FE"), LinearGradientMode.Horizontal)) pe6.Graphics.FillRectangle(br, 0, 1, footer.Width, 2); };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8) };

            var pnlLblCust = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.Transparent };
            pnlLblCust.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#4C1D95"))) { var sz2 = pe.Graphics.MeasureString("«Œ — «·⁄„Ì· *", f2); pe.Graphics.DrawString("«Œ — «·⁄„Ì· *", f2, b2, pnlLblCust.Width - sz2.Width - 2, pnlLblCust.Height - sz2.Height - 1); } };

            var cbo = new ComboBox { Height = 48, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 36, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
            cbo.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#EDE9FE") : Color.White), de.Bounds); string itTxt = cbo.GetItemText(cbo.Items[de.Index]); using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#4C1D95") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(itTxt, f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };

            var cboOverlay = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
            cboOverlay.Paint += (s2, pe2) => { var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, cboOverlay.Width - 1, cboOverlay.Height - 1); using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(rc2, 10)) g.FillPath(brs, path2); bool focused = cbo.DroppedDown || cbo.Focused; using (var pen2 = new Pen(focused ? ColorTranslator.FromHtml("#7C3AED") : ColorTranslator.FromHtml("#DDD6FE"), focused ? 2f : 1.5f)) using (var path2 = RoundPath(rc2, 10)) g.DrawPath(pen2, path2); int ax = 20, ay = cboOverlay.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#7C3AED"), 2.5f)) { g.DrawLine(ap, ax + 6, ay - 3, ax, ay + 4); g.DrawLine(ap, ax, ay + 4, ax - 6, ay - 3); } string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : "«Œ — «·⁄„Ì· · Õ„Ì· ›« Ê— Â"; bool isPh = cbo.SelectedIndex < 0; using (var f2 = new Font("Cairo", 11F, isPh ? FontStyle.Regular : FontStyle.Bold)) using (var b2 = new SolidBrush(isPh ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#4C1D95"))) g.DrawString(selTxt, f2, b2, new RectangleF(42, 0, cboOverlay.Width - 58, cboOverlay.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter }); };
            cbo.SetBounds(0, 0, 400, 48); cboOverlay.Controls.Add(cbo); cboOverlay.Resize += (s2, e2) => cbo.SetBounds(0, 0, cboOverlay.Width, 48);
            cbo.SelectedIndexChanged += (s2, e2) => cboOverlay.Invalidate(); cbo.DropDown += (s2, e2) => cboOverlay.Invalidate(); cbo.DropDownClosed += (s2, e2) => cboOverlay.Invalidate();

            var previewCard = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.Transparent, Visible = false };
            string previewName = "", previewPhone = "", previewCount = "";
            previewCard.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, previewCard.Width - 1, previewCard.Height - 1);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#F5F3FF"), ColorTranslator.FromHtml("#EDE9FE"), LinearGradientMode.ForwardDiagonal)) using (var path = RoundPath(rc, 12)) g.FillPath(br, path);
                using (var pen = new Pen(ColorTranslator.FromHtml("#DDD6FE"), 1.5f)) using (var path = RoundPath(rc, 12)) g.DrawPath(pen, path);
                using (var brS = new SolidBrush(ColorTranslator.FromHtml("#7C3AED"))) g.FillRectangle(brS, rc.Right - 4, rc.Top + 10, 4, rc.Height - 20);
                int av = 50, ax2 = rc.Right - 14 - av, ay2 = rc.Top + (rc.Height - av) / 2;
                using (var br = new LinearGradientBrush(new Rectangle(ax2, ay2, av, av), ColorTranslator.FromHtml("#4C1D95"), ColorTranslator.FromHtml("#7C3AED"), LinearGradientMode.ForwardDiagonal)) g.FillEllipse(br, ax2, ay2, av, av);
                string letter = !string.IsNullOrEmpty(previewName) ? previewName[0].ToString() : "⁄";
                using (var f = new Font("Cairo", 18F, FontStyle.Bold)) using (var b = new SolidBrush(Color.White)) { var sz = g.MeasureString(letter, f); g.DrawString(letter, f, b, ax2 + (av - sz.Width) / 2f, ay2 + (av - sz.Height) / 2f); }
                using (var f = new Font("Cairo", 13F, FontStyle.Bold)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#4C1D95"))) g.DrawString(previewName, f, b, new RectangleF(rc.Left + 10, rc.Top + 10, ax2 - 14 - (rc.Left + 10), 28), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.None });
                if (!string.IsNullOrEmpty(previewPhone)) using (var f = new Font("Cairo", 10F)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#374151"))) g.DrawString(previewPhone, f, b, new RectangleF(rc.Left + 10, rc.Top + 42, ax2 - 14 - (rc.Left + 10), 22), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                if (!string.IsNullOrEmpty(previewCount)) using (var f = new Font("Cairo", 9F, FontStyle.Bold)) { var bsz = g.MeasureString(previewCount, f); int bw = (int)bsz.Width + 18, bh = 22, bx = rc.Left + 10, by2 = rc.Top + 58; using (var brB = new SolidBrush(ColorTranslator.FromHtml("#EDE9FE"))) using (var path2 = RoundPath(new Rectangle(bx, by2, bw, bh), bh / 2)) g.FillPath(brB, path2); g.DrawString(previewCount, f, new SolidBrush(ColorTranslator.FromHtml("#4C1D95")), new RectangleF(bx, by2, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
            };

            var lblStatus = new Label { Dock = DockStyle.Top, Height = 0, Font = new Font("Cairo", 9.5F), ForeColor = ColorTranslator.FromHtml("#64748B"), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };

            cbo.DisplayMember = "Name"; cbo.ValueMember = "Id";
            var custList = new List<CustomerDto>(customers); custList.Insert(0, new CustomerDto { Id = 0, Name = "«Œ — «·⁄„Ì·" });
            cbo.DataSource = null; cbo.DataSource = custList;

            cbo.SelectedIndexChanged += (s2, e2) =>
            {
                if (!(cbo.SelectedItem is CustomerDto sel) || sel.Id <= 0) { previewCard.Visible = false; lblStatus.Text = ""; lblStatus.Height = 0; return; }
                var custInvoices = allInvoices.Where(i => i.CustomerId == sel.Id).ToList();
                previewName = sel.Name ?? ""; previewPhone = sel.Phone ?? "";
                previewCount = $"{custInvoices.Count} ›« Ê—…  |  ≈Ã„«·Ì: {custInvoices.Sum(i => i.TotalAmount):N2} Ã";
                previewCard.Visible = true; previewCard.Invalidate();
                lblStatus.Text = $" „  Õ„Ì· »Ì«‰«  {custInvoices.Count} ›« Ê—…"; lblStatus.Height = 22; lblStatus.ForeColor = ColorTranslator.FromHtml("#059669");
                cboOverlay.Invalidate();
            };

            Panel Sp2(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            body.SuspendLayout();
            body.Controls.Add(Sp2(6)); body.Controls.Add(lblStatus); body.Controls.Add(Sp2(8)); body.Controls.Add(previewCard);
            body.Controls.Add(Sp2(10)); body.Controls.Add(cboOverlay); body.Controls.Add(Sp2(4)); body.Controls.Add(pnlLblCust);
            body.ResumeLayout(true);

            var btnGen = new Guna2Button { Dock = DockStyle.Fill, Text = " Õ„Ì·  ﬁ—Ì— «·›« Ê—…", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#7C3AED"), ForeColor = Color.White, Font = new Font("Cairo", 12F, FontStyle.Bold), Animated = true };
            btnGen.HoverState.FillColor = ColorTranslator.FromHtml("#4C1D95"); btnGen.ShadowDecoration.Enabled = true; btnGen.ShadowDecoration.Depth = 4; btnGen.ShadowDecoration.Color = Color.FromArgb(40, 124, 58, 237);
            btnGen.Click += async (s, e) =>
            {
                if (!(cbo.SelectedItem is CustomerDto sel) || sel.Id <= 0) { MessageBox.Show("«Œ — ⁄„Ì·« √Ê·«", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                var custInvoices = allInvoices.Where(i => i.CustomerId == sel.Id).ToList();
                if (custInvoices.Count == 0) { MessageBox.Show("·«  ÊÃœ ›Ê« Ì— ·Â–« «·⁄„Ì·", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                btnGen.Enabled = false; btnGen.Text = "Ã«—Ú «· Õ÷Ì—...";
                try
                {
                    if (custInvoices.Count == 1)
                    {
                        var inv = custInvoices[0]; var pdfBytes = await Task.Run(() => _invPdfSvc.GenerateInvoicePdf(inv)); string safeName = SanitizeName(sel.Name ?? "⁄„Ì·");
                        using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ  ﬁ—Ì— «·›« Ê—…", Filter = "PDF|*.pdf", FileName = $"›« Ê—…_{inv.Id}_{safeName}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                        { if (sfd.ShowDialog() == DialogResult.OK) { File.WriteAllBytes(sfd.FileName, pdfBytes); pf.Close(); ShowSuccessToast($" „ Õ›Ÿ ›« Ê—… {safeName}"); } }
                    }
                    else
                    {
                        var choice = MessageBox.Show($"«·⁄„Ì· '{sel.Name}' ⁄‰œÂ {custInvoices.Count} ›« Ê—….\n\n‰⁄„ = Õ›Ÿ ﬂ· ›« Ê—… ›Ì „·› „‰›’·\n·« = «Œ — „Ã·œ Ê«Õœ ··ﬂ·", "«Œ — ÿ—Ìﬁ… «·Õ›Ÿ", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (choice == DialogResult.Cancel) return;
                        if (choice == DialogResult.Yes)
                        {
                            foreach (var inv in custInvoices) { var pdfBytes = await Task.Run(() => _invPdfSvc.GenerateInvoicePdf(inv)); string safeName = SanitizeName(sel.Name ?? "⁄„Ì·"); using (var sfd = new SaveFileDialog { Title = $"Õ›Ÿ ›« Ê—… #{inv.Id}", Filter = "PDF|*.pdf", FileName = $"›« Ê—…_{inv.Id}_{safeName}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }) { if (sfd.ShowDialog() == DialogResult.OK) File.WriteAllBytes(sfd.FileName, pdfBytes); } }
                            pf.Close(); ShowSuccessToast($" „ Õ›Ÿ {custInvoices.Count} ›« Ê—…");
                        }
                        else
                        {
                            using (var fbd = new FolderBrowserDialog { Description = "«Œ — «·„Ã·œ ·Õ›Ÿ «·›Ê« Ì—", ShowNewFolderButton = true })
                            {
                                if (fbd.ShowDialog() != DialogResult.OK) return;
                                string folder = fbd.SelectedPath; string safeName = SanitizeName(sel.Name ?? "⁄„Ì·"); int saved = 0;
                                foreach (var inv in custInvoices) { var pdfBytes = await Task.Run(() => _invPdfSvc.GenerateInvoicePdf(inv)); File.WriteAllBytes(Path.Combine(folder, $"›« Ê—…_{inv.Id}_{safeName}.pdf"), pdfBytes); saved++; }
                                pf.Close(); ShowSuccessToast($" „ Õ›Ÿ {saved} ›« Ê—… ›Ì «·„Ã·œ");
                            }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { btnGen.Enabled = true; btnGen.Text = " Õ„Ì·  ﬁ—Ì— «·›« Ê—…"; }
            };
            footer.Controls.Add(btnGen);
            pf.Controls.Add(body); pf.Controls.Add(footer); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  TOAST
        // ??????????????????????????????????????????????????????
        private async void ShowSuccessToast(string msg)
        {
            var toast = new Panel { Size = new Size(320, 52), BackColor = ColorTranslator.FromHtml("#EEF2FF"), Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath()) { gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(toast.Width - 20, 0, 20, 20, 270, 90); gp.AddArc(toast.Width - 20, toast.Height - 20, 20, 20, 0, 90); gp.AddArc(0, toast.Height - 20, 20, 20, 90, 90); gp.CloseFigure(); toast.Region = new Region(gp); }
            toast.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(ThemeAccent, 1.5f)) using (var path = RoundPath(new Rectangle(0, 0, toast.Width - 1, toast.Height - 1), 10)) pe.Graphics.DrawPath(pen, path); pe.Graphics.FillRectangle(new SolidBrush(ThemeAccent), 0, 8, 4, toast.Height - 16); using (var f = new Font("Cairo", 10.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ThemeDark)) pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, toast.Width - 8, toast.Height), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40);
            Controls.Add(toast); toast.BringToFront();
            toast.Click += (s, e) => { try { Controls.Remove(toast); toast.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10) { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + (100 - i) / 5); await Task.Delay(8); }
            await Task.Delay(2800);
            for (int i = 0; i <= 100; i += 10) { try { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + i / 5); } catch { break; } await Task.Delay(8); }
            try { Controls.Remove(toast); toast.Dispose(); } catch { }
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "report";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        private static readonly System.Reflection.PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent)
        {
            foreach (Control c in parent.Controls)
            { try { _dbProp?.SetValue(c, true); } catch { } if (c.Controls.Count > 0) EnableDbAll(c); }
        }

        private static void DrawDocIcon(Graphics g, Pen pen, int x, int y, int w, int h)
        {
            int fold = w / 4;
            var pts = new Point[] { new Point(x + w - fold, y), new Point(x, y), new Point(x, y + h), new Point(x + w, y + h), new Point(x + w, y + fold), new Point(x + w - fold, y) };
            g.DrawLines(pen, pts);
            g.DrawLine(pen, x + w - fold, y, x + w - fold, y + fold);
            g.DrawLine(pen, x + w - fold, y + fold, x + w, y + fold);
            using (var linePen = new Pen(pen.Color, 1f))
            {
                int lineY = y + h / 3;
                g.DrawLine(linePen, x + 4, lineY, x + w - 6, lineY);
                g.DrawLine(linePen, x + 4, lineY + 8, x + w - 6, lineY + 8);
                g.DrawLine(linePen, x + 4, lineY + 16, x + w / 2, lineY + 16);
            }
        }

        private GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}