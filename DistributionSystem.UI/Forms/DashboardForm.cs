using DistributionSystem.Business.Services;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DistributionSystem.UI.Forms
{
    public partial class DashboardForm : Form
    {
        private readonly DashboardService _service;
        private System.Windows.Forms.Timer _refreshTimer;
        private System.Windows.Forms.Timer _clockTimer;   //  ÕœÌÀ «· «—ÌŒ ﬂ· À«‰Ì…
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Color C_Dark = ColorTranslator.FromHtml("#1a2f5e");
        private static readonly Color C_Mid = ColorTranslator.FromHtml("#1565c0");
        private static readonly Color C_Accent = ColorTranslator.FromHtml("#4E73DF");
        private static readonly Color C_Bg = ColorTranslator.FromHtml("#EEF0F5");

        private Label _lblProducts, _lblCustomers, _lblTreasury,
                      _lblSales, _lblPurchases, _lblLowStock;
        private Bitmap _bannerCache;

        public DashboardForm()
        {
            InitializeComponent();
            _service = new DashboardService();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            BuildLayout();
        }

        // ??????????????????????????????????????????????????????
        //  LAYOUT
        // ??????????????????????????????????????????????????????
        private void BuildLayout()
        {
            SuspendLayout();
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = C_Bg;
            Padding = new Padding(0);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // banner
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 545F)); // ·ÊÃÊ + ﬂ—Ê 
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // canvas

            root.Controls.Add(BuildBanner(), 0, 0);
            root.Controls.Add(BuildCardsGrid(), 0, 1);
            root.Controls.Add(BuildBgCanvas(), 0, 2);

            Controls.Add(root);
            root.BringToFront();
            EnableDb(this);
            ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  BANNER
        // ??????????????????????????????????????????????????????
        private Panel BuildBanner()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
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
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        var rc = new Rectangle(0, 0, banner.Width, banner.Height);

                        // Œ·›Ì… gradient
                        using (var br = new LinearGradientBrush(rc, C_Dark, C_Mid, LinearGradientMode.Horizontal))
                        using (var path = Rp(rc, 16))
                            g.FillPath(br, path);

                        // ‰ﬁ«ÿ “Œ—›Ì…
                        using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                            for (int x = 10; x < banner.Width; x += 22)
                                for (int y = 8; y < banner.Height; y += 22)
                                    g.FillEllipse(dot, x, y, 2, 2);

                        // œÊ«∆— Œ·›Ì…
                        using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                        {
                            g.FillEllipse(cb, banner.Width - 150, -60, 250, 250);
                            g.FillEllipse(cb, -60, -40, 200, 200);
                        }

                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };

                        // «·⁄‰Ê«‰ «·—∆Ì”Ì
                        using (var f = new Font("Cairo", 24F, FontStyle.Bold))
                        using (var b = new SolidBrush(Color.White))
                            g.DrawString("·ÊÕ… «· Õﬂ„", f, b,
                                new RectangleF(0, -10, banner.Width, banner.Height), sf);

                        // subtitle
                        using (var f = new Font("Cairo", 11F, FontStyle.Bold))
                        using (var b = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
                            g.DrawString("‰Ÿ«„ ≈œ«—… «·„Œ«“‰ Ê«· Ê“Ì⁄", f, b,
                                new RectangleF(0, 28, banner.Width, banner.Height), sf);

                        // «· «—ÌŒ ó Ì„Ì‰ «·ÂÌœ— ó ›Ê‰  √ﬂ»—
                        // (Ìı—”„ Œ«—Ã «·‹ cache ⁄‘«‰ Ì ÕœÀ ﬂ· À«‰Ì… ó —«Ã⁄ e.Graphics.DrawString »⁄œ DrawImage)
                    }
                }
                e.Graphics.DrawImage(_bannerCache, 0, 0);

                // ?? «· «—ÌŒ Ìı—”„ „»«‘—… (Œ«—Ã «·‹ cache) ⁄‘«‰ Ì ÕœÀ ﬂ· À«‰Ì… ??
                string dtStr2 = DateTime.Now.ToString("yyyy/MM/dd  ï  HH:mm:ss", Inv);
                using (var f = new Font("Cairo", 14F, FontStyle.Bold))
                using (var b = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                {
                    SizeF sz = e.Graphics.MeasureString(dtStr2, f);
                    e.Graphics.DrawString(dtStr2, f, b,
                        banner.Width - sz.Width - 20,
                        (banner.Height - sz.Height) / 2f);
                }
            };
            banner.Resize += (s, e) => { _bannerCache?.Dispose(); _bannerCache = null; };

            // “— Œ—ÊÃ ó Ì”«— «·ÂÌœ—
            var btnExit = new Guna.UI2.WinForms.Guna2Button
            {
                Text = "Œ—ÊÃ",
                FillColor = Color.FromArgb(180, 220, 50, 50),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(120, 255, 80, 80),
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                Size = new Size(90, 34),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(16, (110 - 34) / 2)
            };
            btnExit.HoverState.FillColor = Color.FromArgb(220, 200, 30, 30);
            btnExit.Click += (s, e) => Application.Exit();
            banner.Controls.Add(btnExit);

            pnl.Controls.Add(banner);
            // “— «· ﬁ—Ì— «·ÌÊ„Ì
            var btnDailyReport = new Guna.UI2.WinForms.Guna2Button
            {
                Text = "«· ﬁ—Ì— «·ÌÊ„Ì",
                FillColor = Color.FromArgb(99, 102, 241),
                ForeColor = Color.White,
                BorderRadius = 12,
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                Size = new Size(170, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Location = new Point(120, (110 - 38) / 2)
            };
            btnDailyReport.Click += (s, e) =>
            {
                using (var picker = new DatePickerForm())
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        var warehouseSvc = new WarehouseService();
                        var allData = warehouseSvc.GetAllTransactions()?.ToList()
                                      ?? new System.Collections.Generic.List<DistributionSystem.Business.Dtos.WarehouseTransactionViewDto>();
                        var pdfService = new TransactionsReportPdfService();
                        pdfService.GenerateAndOpen(picker.SelectedDate, allData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message, "Œÿ√",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  CARDS GRID ó ·ÊÃÊ ›Êﬁ + 3◊2 ﬂ—Ê 
        // ??????????????????????????????????????????????????????
        private Panel BuildCardsGrid()
        {
            var wrapper = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(20, 8, 20, 16)
            };

            // ?? «··ÊÃÊ ›Êﬁ «·ﬂ—Ê  ó ‰›” „‰ÿﬁ LogoCellEvent ›Ì  ﬁ—Ì— «·›« Ê—… ??
            var logoPnl = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175,
                BackColor = Color.Transparent
            };
            logoPnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                int W = logoPnl.Width;
                // ?? ‰›” ﬁÌ„ LogoCellEvent »«·Ÿ»ÿ ??
                float cx = W / 2f;
                float cy = 75f;          // „—ﬂ“ «·œ«Ì—… ⁄„ÊœÌ«
                float r = 70f;          // œ«Ì—… √ﬂ»— »ﬂ Ì—

                // ?? «·œ«Ì—… «·œ«ﬂ‰… ó bgCb.SetColorFill(_dark) ??
                using (var br = new SolidBrush(C_Dark))
                    g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);

                // ?? «·»Ê—œ— «·√“—ﬁ ó bgCb.SetColorStroke(_mid) r+3 ??
                using (var pen = new Pen(C_Mid, 2.5f))
                    g.DrawEllipse(pen, cx - r - 3, cy - r - 3, (r + 3) * 2, (r + 3) * 2);

                // ?? ‰’ "»’Ê’" ó ‰›” ColumnText SetSimpleColumn(cx-32, cy-9, cx+32, cy+15) ??
                // ‰ÕÊ· «·√»⁄«œ «·‰”»Ì…: ⁄—÷ 64px° «— ›«⁄ 24px° „— ﬂ“… ⁄·Ï cx,cy
                using (var f = new Font("Cairo", 26f, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString("»’Ê’", f);
                    // padding = 16px „‰ ﬂ· Ã«‰» œ«Œ· «·œ«Ì—…
                    float tx = cx - sz.Width / 2f;
                    float ty = cy - sz.Height / 2f;
                    g.DrawString("»’Ê’", f, b, tx, ty);
                }

                // ?? "‘—ﬂ… »’Ê’ ·· Ê“Ì⁄"  Õ  «·œ«Ì—… ??
                using (var f = new Font("Cairo", 11f, FontStyle.Bold))
                using (var b = new SolidBrush(C_Dark))
                {
                    SizeF sz = g.MeasureString("‘—ﬂ… »’Ê’ ·· Ê“Ì⁄", f);
                    float tx = cx - sz.Width / 2f;
                    float ty = cy + r + 6f;
                    g.DrawString("‘—ﬂ… »’Ê’ ·· Ê“Ì⁄", f, b, tx, ty);
                }
            };

            // ?? Ã—Ìœ «·ﬂ—Ê  2◊3 ??
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            for (int c = 0; c < 3; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var defs = new string[][]
            {
                new[]{ "#4E73DF", "products",  "«·„‰ Ã« ",       "Products"  },
                new[]{ "#10B981", "customers", "«·⁄„·«¡",         "Customers" },
                new[]{ "#F59E0B", "treasury",  "«·—’Ìœ «·ﬂ·Ì",   "Suppliers" },
                new[]{ "#6366F1", "invoice",   "›Ê« Ì— «·»Ì⁄",   "Sales"     },
                new[]{ "#0891B2", "box",       "√Ê«„— «·Ê«—œ",   "Purchases" },
                new[]{ "#EF4444", "alert",     " ‰»ÌÂ«  «·„Œ“‰", "Warehouse" },
            };

            Label[] refs = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                var card = MakeCard(defs[i][0], defs[i][1], defs[i][2], defs[i][3], out refs[i]);
                grid.Controls.Add(card, col, row);
            }

            _lblProducts = refs[0]; _lblCustomers = refs[1]; _lblTreasury = refs[2];
            _lblSales = refs[3]; _lblPurchases = refs[4]; _lblLowStock = refs[5];

            //  — Ì» «·≈÷«›… „Â„: grid √Ê·« À„ logoPnl (⁄‘«‰ Dock=Top ÌŸÂ— ›Êﬁ)
            wrapper.Controls.Add(grid);
            wrapper.Controls.Add(logoPnl);
            return wrapper;
        }

        private Panel MakeCard(string accentHex, string iconType, string title, string tag, out Label valueLabel)
        {
            var accent = ColorTranslator.FromHtml(accentHex);
            var outer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(8),
                Cursor = Cursors.Hand
            };

            var card = new Guna.UI2.WinForms.Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = 16,
                BorderThickness = 0
            };
            card.ShadowDecoration.Enabled = true;
            card.ShadowDecoration.Depth = 8;
            card.ShadowDecoration.Color = Color.FromArgb(20, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = accent };

            var inner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 8, 12, 8)
            };

            // √ÌﬁÊ‰…
            var iconP = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
            iconP.Paint += (s, e) => DrawCardIcon(e.Graphics, iconType, accent, iconP.Width, iconP.Height);

            // «·ﬁÌ„…
            var lblVal = new Label
            {
                Text = "ó",
                Dock = DockStyle.Top,
                Height = 38,
                Font = new Font("Cairo", 20F, FontStyle.Bold),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            // «·⁄‰Ê«‰
            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#374151"),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            inner.Controls.Add(lblTitle);
            inner.Controls.Add(lblVal);
            inner.Controls.Add(iconP);

            card.Controls.Add(inner);
            card.Controls.Add(topBar);
            outer.Controls.Add(card);

            // Hover
            card.MouseEnter += (s, e) => { card.ShadowDecoration.Depth = 16; card.ShadowDecoration.Color = Color.FromArgb(35, accent); };
            card.MouseLeave += (s, e) => { card.ShadowDecoration.Depth = 8; card.ShadowDecoration.Color = Color.FromArgb(20, 0, 0, 0); };

            Action onClick = () =>
            {
                var parent = FindParent<MainLayoutForm>(this);
                if (parent == null) return;
                switch (tag)
                {
                    case "Products": parent.LoadChildForm(new ProductForm()); break;
                    case "Customers": parent.LoadChildForm(new CustomerForm()); break;
                    case "Suppliers": parent.LoadChildForm(new TreasuryForm()); break;
                    case "Sales": parent.LoadChildForm(new SalesInvoicesForm()); break;
                    case "Purchases": parent.LoadChildForm(new InboundForm()); break;
                    case "Warehouse": parent.LoadChildForm(new WarehouseReportForm()); break;
                }
            };
            card.Click += (s, e) => onClick();
            inner.Click += (s, e) => onClick();
            iconP.Click += (s, e) => onClick();
            lblVal.Click += (s, e) => onClick();
            lblTitle.Click += (s, e) => onClick();

            valueLabel = lblVal;
            return outer;
        }

        // ??????????????????????????????????????????????????????
        //  BACKGROUND CANVAS
        // ??????????????????????????????????????????????????????
        private Panel BuildBgCanvas()
        {
            var canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            canvas.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = canvas.Width, H = canvas.Height;
                if (W <= 0 || H <= 0) return;

                using (var darkBr = new SolidBrush(Color.FromArgb(40, 26, 47, 94)))
                using (var midBr = new SolidBrush(Color.FromArgb(32, 21, 101, 192)))
                using (var darkPen = new Pen(Color.FromArgb(55, 26, 47, 94), 1.8f))
                using (var midPen = new Pen(Color.FromArgb(50, 21, 101, 192), 1.8f))
                {
                    DrawTruckIllustration(g, darkPen, midPen, midBr, darkBr, 40, H / 2 - 50);
                    DrawTrendChart(g, darkPen, midPen, midBr, W / 2 - 120, H - 130, 240, 100);
                    DrawBarChart(g, darkPen, midBr, W - 200, H - 120, 160, 90);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(28, 26, 47, 94)), -60, H - 200, 280, 280);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(20, 21, 101, 192)), W - 80, -60, 200, 200);
                    DrawPackageIcon(g, darkPen, 55, 30, 50, 50);
                    DrawArrowFlow(g, midPen, W / 2 - 40, 20, 80);
                    using (var f = new Font("Cairo", 40F, FontStyle.Bold))
                    using (var br = new SolidBrush(Color.FromArgb(22, 26, 47, 94)))
                    {
                        g.DrawString("%", f, br, W - 90, H - 110);
                        g.DrawString("?", f, br, 20, H - 100);
                    }
                }
            };
            return canvas;
        }

        // ?? —”Ê„«  ????????????????????????????????????????????
        private static void DrawTruckIllustration(Graphics g, Pen dp, Pen mp, SolidBrush mb, SolidBrush db, int x, int y)
        {
            var body = new Rectangle(x + 30, y + 10, 90, 55);
            g.FillRectangle(mb, body); g.DrawRectangle(dp, body);
            var cab = new Rectangle(x, y + 20, 38, 45);
            g.FillRectangle(db, cab); g.DrawRectangle(dp, cab);
            g.FillRectangle(new SolidBrush(Color.FromArgb(40, 100, 181, 246)), x + 4, y + 24, 28, 20);
            g.DrawRectangle(mp, x + 4, y + 24, 28, 20);
            g.FillEllipse(db, x + 4, y + 58, 22, 22); g.DrawEllipse(dp, x + 4, y + 58, 22, 22);
            g.FillEllipse(db, x + 55, y + 58, 22, 22); g.DrawEllipse(dp, x + 55, y + 58, 22, 22);
            g.FillEllipse(db, x + 95, y + 58, 22, 22); g.DrawEllipse(dp, x + 95, y + 58, 22, 22);
            for (int i = 0; i < 3; i++) g.DrawRectangle(mp, x + 36 + i * 28, y + 16, 24, 22);
            g.DrawLine(dp, x - 10, y + 82, x + 145, y + 82);
            for (int i = 0; i < 5; i++) g.DrawLine(mp, x - 10 + i * 30, y + 86, x + 8 + i * 30, y + 86);
        }

        private static void DrawTrendChart(Graphics g, Pen dp, Pen mp, SolidBrush mb, int x, int y, int w, int h)
        {
            g.DrawLine(dp, x, y, x, y + h); g.DrawLine(dp, x, y + h, x + w, y + h);
            var pts = new[] {
                new PointF(x,          y+h-20), new PointF(x+w*0.15f, y+h-55),
                new PointF(x+w*0.30f,  y+h-35), new PointF(x+w*0.45f, y+h-75),
                new PointF(x+w*0.60f,  y+h-50), new PointF(x+w*0.75f, y+h-85),
                new PointF(x+w,        y+h-65)
            };
            g.DrawCurve(mp, pts, 0.5f);
            var fp = new PointF[pts.Length + 2];
            fp[0] = new PointF(x, y + h);
            for (int i = 0; i < pts.Length; i++) fp[i + 1] = pts[i];
            fp[fp.Length - 1] = new PointF(x + w, y + h);
            g.FillPolygon(mb, fp);
            foreach (var pt in pts)
                g.FillEllipse(new SolidBrush(Color.FromArgb(80, 21, 101, 192)), pt.X - 4, pt.Y - 4, 8, 8);
        }

        private static void DrawBarChart(Graphics g, Pen dp, SolidBrush mb, int x, int y, int w, int h)
        {
            g.DrawLine(dp, x, y, x, y + h); g.DrawLine(dp, x, y + h, x + w, y + h);
            int[] heights = { 40, 65, 30, 75, 55, 45 }; int bw = 18, bx = x + 8;
            foreach (var bh in heights)
            {
                g.FillRectangle(mb, bx, y + h - bh, bw, bh);
                g.DrawRectangle(dp, bx, y + h - bh, bw, bh);
                bx += bw + 6;
            }
        }

        private static void DrawPackageIcon(Graphics g, Pen pen, int x, int y, int w, int h)
        {
            g.DrawRectangle(pen, x, y + h / 3, w, h * 2 / 3);
            g.DrawLine(pen, x, y + h / 3, x + w / 2 - 6, y);
            g.DrawLine(pen, x + w, y + h / 3, x + w / 2 + 6, y);
            g.DrawLine(pen, x + w / 2 - 6, y, x + w / 2 + 6, y);
            g.DrawLine(pen, x + w / 3, y + h / 3, x + w / 3, y + h);
            g.DrawLine(pen, x + w * 2 / 3, y + h / 3, x + w * 2 / 3, y + h);
        }

        private static void DrawArrowFlow(Graphics g, Pen pen, int x, int y, int len)
        {
            int step = len / 3;
            for (int i = 0; i < 3; i++)
            {
                int sx = x + i * step, ex = sx + step - 4;
                g.DrawLine(pen, sx, y + 12, ex, y + 12);
                g.DrawLine(pen, ex - 6, y + 6, ex, y + 12);
                g.DrawLine(pen, ex - 6, y + 18, ex, y + 12);
                g.DrawRectangle(pen, sx, y, 18, 12);
            }
        }

        private static void DrawCardIcon(Graphics g, string type, Color accent, int W, int H)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int sz = 34, ix = (W - sz) / 2, iy = (H - sz) / 2;
            int cx = ix + sz / 2, cy = iy + sz / 2;
            using (var bg = new SolidBrush(Color.FromArgb(20, accent)))
                g.FillEllipse(bg, ix - 2, iy - 2, sz + 4, sz + 4);
            using (var p = new Pen(accent, 2f))
            using (var br = new SolidBrush(accent))
            {
                switch (type)
                {
                    case "products":
                        g.DrawRectangle(p, cx - 9, cy - 5, 18, 13);
                        g.DrawLine(p, cx - 9, cy, cx + 9, cy);
                        g.DrawLine(p, cx - 9, cy - 5, cx - 3, cy - 11);
                        g.DrawLine(p, cx + 9, cy - 5, cx + 3, cy - 11);
                        g.DrawLine(p, cx - 3, cy - 11, cx + 3, cy - 11);
                        g.DrawLine(p, cx - 2, cy - 5, cx + 2, cy - 5);
                        break;
                    case "customers":
                        g.DrawEllipse(p, cx - 5, cy - 11, 10, 10);
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(cx - 8, cy + 1, 16, 9, 180, 180);
                            g.DrawPath(p, path);
                        }
                        break;
                    case "treasury":
                        g.DrawRectangle(p, cx - 9, cy - 9, 18, 18);
                        g.DrawEllipse(p, cx - 4, cy - 4, 8, 8);
                        g.FillEllipse(br, cx - 1, cy - 1, 3, 3);
                        g.DrawLine(p, cx + 4, cy, cx + 9, cy);
                        break;
                    case "invoice":
                        g.DrawRectangle(p, cx - 8, cy - 11, 16, 22);
                        g.DrawLine(p, cx - 4, cy - 6, cx + 4, cy - 6);
                        g.DrawLine(p, cx - 4, cy - 1, cx + 4, cy - 1);
                        g.DrawLine(p, cx - 4, cy + 4, cx + 1, cy + 4);
                        break;
                    case "box":
                        g.DrawRectangle(p, cx - 9, cy - 4, 18, 13);
                        g.DrawLine(p, cx - 9, cy - 4, cx - 3, cy - 11);
                        g.DrawLine(p, cx + 9, cy - 4, cx + 3, cy - 11);
                        g.DrawLine(p, cx - 3, cy - 11, cx + 3, cy - 11);
                        g.DrawLine(p, cx - 2, cy - 4, cx - 2, cy + 9);
                        g.DrawLine(p, cx + 2, cy - 4, cx + 2, cy + 9);
                        g.DrawLine(p, cx, cy - 14, cx, cy - 11);
                        g.DrawLine(p, cx - 3, cy - 13, cx, cy - 10);
                        g.DrawLine(p, cx + 3, cy - 13, cx, cy - 10);
                        break;
                    case "alert":
                        var tri = new PointF[]
                        {
                            new PointF(cx,      cy - 11),
                            new PointF(cx - 10, cy + 8),
                            new PointF(cx + 10, cy + 8)
                        };
                        g.DrawPolygon(p, tri);
                        g.DrawLine(p, cx, cy - 4, cx, cy + 2);
                        g.FillEllipse(br, cx - 1.5f, cy + 4, 3, 3);
                        break;
                }
            }
        }

        // ??????????????????????????????????????????????????????
        //  LOAD STATS
        // ??????????????????????????????????????????????????????
        private async void DashboardForm_Load(object sender, EventArgs e)
        {
            await LoadStatsAsync();
            StartAutoRefresh();
        }

        private void StartAutoRefresh()
        {
            //  ÕœÌÀ «·»Ì«‰«  ﬂ· 30 À«‰Ì…
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _refreshTimer.Tick += async (s, e) => await LoadStatsAsync();
            _refreshTimer.Start();

            //  ÕœÌÀ «· «—ÌŒ ›Ì «·ÂÌœ— ﬂ· À«‰Ì…
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
            _clockTimer.Tick += (s, e) =>
            {
                // ‰⁄Ìœ —”„ «·ÂÌœ— ›ﬁÿ (invalidate «·»«‰—)
                var banner = Controls.Count > 0
                    ? FindBannerPanel(Controls[0])
                    : null;
                banner?.Invalidate();
            };
            _clockTimer.Start();
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var stats = await Task.Run(() => _service.GetStats());
                if (IsDisposed) return;
                _lblProducts.Text = stats.TotalProducts.ToString("N0", Inv);
                _lblCustomers.Text = stats.TotalCustomers.ToString("N0", Inv);
                _lblTreasury.Text = stats.TreasuryBalance.ToString("N0", Inv) + " Ã";
                _lblSales.Text = stats.TotalSales.ToString("N0", Inv);
                _lblPurchases.Text = stats.TotalPurchases.ToString("N0", Inv);
                _lblLowStock.Text = stats.LowStockAlerts.ToString("N0", Inv);
            }
            catch { }
        }

        // ?? HELPERS ???????????????????????????????????????????
        private static T FindParent<T>(Control ctrl) where T : Form
        {
            var c = ctrl?.Parent;
            while (c != null) { if (c is T t) return t; c = c.Parent; }
            return null;
        }

        private static GraphicsPath Rp(Rectangle r, int radius)
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

        private static readonly PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void EnableDb(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                try { _dbProp?.SetValue(c, true); } catch { }
                if (c.Controls.Count > 0) EnableDb(c);
            }
        }

        // ?? Ì»ÕÀ ⁄‰ √Ê· Panel ›Ì ’› «·ÂÌœ— ⁄‘«‰ Ì⁄„· Invalidate ﬂ· À«‰Ì… ??
        private static Panel FindBannerPanel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Panel p) return p;
                var found = FindBannerPanel(c);
                if (found != null) return found;
            }
            return null;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer?.Stop(); _refreshTimer?.Dispose();
            _clockTimer?.Stop(); _clockTimer?.Dispose();
            _bannerCache?.Dispose();
            base.OnFormClosed(e);
        }

        private void Card_Click(object sender, EventArgs e) { }
    }
}