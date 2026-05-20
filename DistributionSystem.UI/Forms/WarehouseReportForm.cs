using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public partial class WarehouseReportForm : Form
    {
        private readonly WarehouseService _service;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Guna2DataGridView dgvWarehouse;
        private Label lblCountBadge;

        // كروت الإحصاء
        private Label lblProductCount;
        private Label lblTotalQty;
        private Label lblTotalValue;

        private List<WarehouseBalanceDto> _allBalances = new List<WarehouseBalanceDto>();
        private int _currentPage = 1;
        private const int PageSize = 8;
        private Panel _paginationBar;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allBalances.Count / (double)PageSize));

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════
        private static readonly PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            { try { _dbProp?.SetValue(ctrl, true); } catch { } if (ctrl.Controls.Count > 0) EnableDbAll(ctrl); }
        }
        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

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

        public WarehouseReportForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            UpdateStyles();
            _service = new WarehouseService();
            BuildNewUI();
            Load += (s, e) => LoadWarehouse();
            SizeChanged += (s, e) => FitColumns();
        }

        // ══════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════
        private void BuildNewUI()
        {
            SuspendLayout();
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(0);
            foreach (Control c in Controls) if (c != null) c.Visible = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.SuspendLayout();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildSummaryCards(), 0, 1);
            root.Controls.Add(BuildTableCard(), 0, 2);

            root.ResumeLayout(false);
            EnableDbAll(root);
            Controls.Add(root);
            root.BringToFront();
            ResumeLayout(true);
        }

        // ══════════════════════════════════════════════════════
        //  HEADER
        // ══════════════════════════════════════════════════════
        private Panel BuildPageHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 50, Padding = new Padding(0), BackColor = ColorTranslator.FromHtml("#EEF0F5") };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            banner.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    for (int x = 10; x < banner.Width; x += 22)
                        for (int y = 8; y < banner.Height; y += 22)
                            g.FillEllipse(dot, x, y, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                {
                    g.FillEllipse(cb, banner.Width - 130, -50, 220, 220);
                    g.FillEllipse(cb, banner.Width - 30, 20, 160, 160);
                }
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold))
                using (var sf2f = new Font("Cairo", 10.5F))
                {
                    string title = "إدارة المخزن";
                    string sub = "عرض وإدارة أرصدة المنتجات";
                    var szT = g.MeasureString(title, tf);
                    var szS = g.MeasureString(sub, sf2f);
                    float gap = 4f, block = szT.Height + gap + szS.Height;
                    float startY = (banner.Height - block) / 2f;
                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);
                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                        g.DrawString(sub, sf2f, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);
                    float lineY = startY + szT.Height + gap + szS.Height + 4f;
                    using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                        g.FillRectangle(b1, banner.Width - 44, lineY, 40, 3);
                    using (var b2 = new SolidBrush(Color.FromArgb(140, 100, 181, 246)))
                        g.FillRectangle(b2, banner.Width - 62, lineY, 14, 3);
                }
            };

            pnl.Controls.Add(banner);
            return pnl;
        }

        // ══════════════════════════════════════════════════════
        //  كروت الإحصاء
        // ══════════════════════════════════════════════════════
        private Panel BuildSummaryCards()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 6) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var card1 = MakeStatCard("عدد المنتجات", "0", ColorTranslator.FromHtml("#2563EB"), ColorTranslator.FromHtml("#EFF6FF"), ColorTranslator.FromHtml("#BFDBFE"), out lblProductCount);
            var card2 = MakeStatCard("إجمالي المخزون", "0 قطعة", ColorTranslator.FromHtml("#0891B2"), ColorTranslator.FromHtml("#F0F9FF"), ColorTranslator.FromHtml("#BAE6FD"), out lblTotalQty);
            var card3 = MakeStatCard("قيمة المخزون", "0.00 ج", ColorTranslator.FromHtml("#7C3AED"), ColorTranslator.FromHtml("#F5F3FF"), ColorTranslator.FromHtml("#DDD6FE"), out lblTotalValue);

            layout.Controls.Add(card1, 0, 0);
            layout.Controls.Add(card2, 1, 0);
            layout.Controls.Add(card3, 2, 0);
            pnl.Controls.Add(layout);
            return pnl;
        }

        private Panel MakeStatCard(string title, string value, Color accentColor, Color bgColor, Color borderColor, out Label valueLabel)
        {
            var wrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 5, 0), BackColor = Color.Transparent };
            var card = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 14, BorderThickness = 1, BorderColor = borderColor };
            card.ShadowDecoration.Enabled = true; card.ShadowDecoration.Depth = 10; card.ShadowDecoration.Color = Color.FromArgb(12, 0, 0, 0);

            var topStrip = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accentColor };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(10, 4, 10, 4) };

            var lbl = new Label { Text = value, Font = new Font("Cairo", 16F, FontStyle.Bold), ForeColor = accentColor, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            valueLabel = lbl;

            var lblTitle = new Label { Text = title, Font = new Font("Cairo", 10F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#64748B"), AutoSize = false, Height = 22, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };

            body.Controls.Add(lbl); body.Controls.Add(lblTitle);
            card.Controls.Add(body); card.Controls.Add(topStrip);
            wrapper.Controls.Add(card);
            return wrapper;
        }

        // ══════════════════════════════════════════════════════
        //  TABLE CARD
        // ══════════════════════════════════════════════════════
        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblCountBadge = new Label { Text = "0 منتج", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblCountBadge.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblCountBadge);

            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("سجل أرصدة المخزن", tf); g.DrawString("سجل أرصدة المخزن", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
                string badge = lblCountBadge?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badge, bf); int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2))
                    using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };

            var searchSeparator = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSeparator.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSeparator.Width, 3), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, searchSeparator.Width, 3);
            };

            dgvWarehouse = new Guna2DataGridView
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                AutoGenerateColumns = false,
                ColumnHeadersHeight = 48,
                EnableHeadersVisualStyles = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                AllowUserToResizeRows = false,
                ScrollBars = ScrollBars.Vertical,
                GridColor = Color.White,
                BackColor = Color.White
            };
            dgvWarehouse.RowTemplate.Height = 76;
            dgvWarehouse.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvWarehouse.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvWarehouse.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgvWarehouse.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgvWarehouse.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvWarehouse.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvWarehouse.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");
            dgvWarehouse.DefaultCellStyle.BackColor = Color.White;
            dgvWarehouse.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvWarehouse.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvWarehouse.DefaultCellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
            dgvWarehouse.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgvWarehouse.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");

            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgvWarehouse, true); } catch { }

            BuildColumns();
            dgvWarehouse.CellPainting += Dgv_CellPainting;
            dgvWarehouse.Resize += (s, e) => FitColumns();

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrapper.Controls.Add(dgvWarehouse);

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) =>
            { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0); };

            container.Controls.Add(dgvWrapper);
            container.Controls.Add(_paginationBar);
            container.Controls.Add(searchSeparator);
            container.Controls.Add(topBar);
            card.Controls.Add(container);
            return card;
        }

        // ══════════════════════════════════════════════════════
        //  COLUMNS — المنتج | الكمية (قطعة) | سعر الوحدة | الإجمالي
        // ══════════════════════════════════════════════════════
        private void BuildColumns()
        {
            dgvWarehouse.Columns.Clear();
            void Add(string name, string hdr, string prop, int w) =>
                dgvWarehouse.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = hdr, DataPropertyName = prop, Width = w });

            Add("ProductName", "المنتج", "ProductName", 220);
            Add("Balance", "الكمية (قطعة)", "Balance", 140);
            Add("AvgCost", "سعر الوحدة", "AvgCost", 130);
            Add("TotalCost", "الإجمالي الكلي", "TotalCost", 140);

            foreach (DataGridViewColumn c in dgvWarehouse.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        // ══════════════════════════════════════════════════════
        //  DATA
        // ══════════════════════════════════════════════════════
        private void LoadWarehouse()
        {
            try
            {
                List<WarehouseBalanceDto> all;
                try { all = _service.GetAllBalancesWithCost()?.ToList() ?? new List<WarehouseBalanceDto>(); }
                catch { all = _service.GetAllBalances()?.ToList() ?? new List<WarehouseBalanceDto>(); foreach (var b in all) b.TotalCost = b.Balance * b.AvgCost; }

                _allBalances = all;
                _currentPage = Math.Min(_currentPage, TotalPages);
                var page = _allBalances.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
                dgvWarehouse.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                UpdateCards();
                RenderPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل المخزن: {GetInner(ex)}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateCards()
        {
            int productCount = _allBalances.Select(x => x.ProductId).Distinct().Count();
            int totalPieces = _allBalances.Sum(x => x.Balance);
            decimal totalVal = _allBalances.Sum(x => x.TotalCost);

            if (lblProductCount != null) lblProductCount.Text = productCount.ToString("N0", Inv);
            if (lblTotalQty != null) lblTotalQty.Text = $"{totalPieces} قطعة";
            if (lblTotalValue != null) lblTotalValue.Text = totalVal.ToString("N2", Inv) + " ج";
            if (lblCountBadge != null) lblCountBadge.Text = $"{productCount} منتج";
        }

        private void FitColumns()
        {
            if (dgvWarehouse == null || dgvWarehouse.Columns.Count == 0) return;
            int w = dgvWarehouse.ClientSize.Width; if (w <= 0) return;
            int wQty = 140, wPrice = 120, wTotal = 130;
            int wName = Math.Max(140, w - wQty - wPrice - wTotal);
            dgvWarehouse.Columns["ProductName"].Width = wName;
            dgvWarehouse.Columns["Balance"].Width = wQty;
            dgvWarehouse.Columns["AvgCost"].Width = wPrice;
            dgvWarehouse.Columns["TotalCost"].Width = wTotal;
        }

        // ══════════════════════════════════════════════════════
        //  CELL PAINTING
        // ══════════════════════════════════════════════════════
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1)
                {
                    e.Handled = true;
                    var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(e.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical))
                        g.FillRectangle(br, e.CellBounds);
                    using (var font = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); g.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
                    return;
                }
                if (e.RowIndex < 0) return;

                bool sel = dgvWarehouse.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                var col = dgvWarehouse.Columns[e.ColumnIndex].Name;

                if (col == "ProductName") PaintProductCell(e, bg);
                else if (col == "Balance") PaintQuantityCell(e, bg);
                else if (col == "AvgCost" || col == "TotalCost") PaintPriceCell(e, bg);
                else
                {
                    e.Handled = true;
                    e.Graphics.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
                    e.PaintContent(e.CellBounds);
                }

                using (var wPen = new Pen(Color.White, 2f))
                {
                    e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom);
                    e.Graphics.DrawLine(wPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
                    e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }
                using (var divPen = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f))
                    e.Graphics.DrawLine(divPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
            catch { }
        }

        private void PaintProductCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            string name = e.Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(name)) { g.ResetClip(); return; }

            var avColors = new[] { "#4E73DF", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#0891B2", "#DC2626" };
            int avSize = 36, pad = 14;
            int avX = e.CellBounds.Right - avSize - pad;
            int avY = e.CellBounds.Top + (e.CellBounds.Height - avSize) / 2;

            using (var sh = new SolidBrush(Color.FromArgb(20, 0, 0, 0))) g.FillEllipse(sh, avX + 2, avY + 2, avSize, avSize);
            using (var avBrush = new SolidBrush(ColorTranslator.FromHtml(avColors[e.RowIndex % avColors.Length]))) g.FillEllipse(avBrush, avX, avY, avSize, avSize);

            string letter = name.Length > 0 ? name[0].ToString() : "?";
            using (var lf = new Font("Cairo", 13F, FontStyle.Bold))
            { var ls = g.MeasureString(letter, lf); g.DrawString(letter, lf, Brushes.White, avX + (avSize - ls.Width) / 2f, avY + (avSize - ls.Height) / 2f); }

            float textW = (avX - 8f) - e.CellBounds.Left - 4f;
            using (var nf = new Font("Cairo", 13F, FontStyle.Bold)) using (var nb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, nf, nb, new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, textW, e.CellBounds.Height), _sfCenter);
            g.ResetClip();
        }

        // ── الكمية بالقطعة — نفس أسلوب InboundForm ──────────
        private void PaintQuantityCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);

            int qty = 0; try { qty = Convert.ToInt32(e.Value); } catch { }
            string txt = $"{qty} قطعة";

            int bw = 110, bh = 28;
            int bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2;
            int by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var brc = new Rectangle(bx, by, bw, bh);

            using (var path = RoundPath(brc, bh / 2))
            { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#DBEAFE")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }

            using (var f = new Font("Cairo", 10F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
                g.DrawString(txt, f, tb, brc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintPriceCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            decimal val = 0m; try { val = Convert.ToDecimal(e.Value); } catch { }
            using (var f = new Font("Cairo", 13F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(val.ToString("N2", Inv) + " ج", f, tb, e.CellBounds,
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        // ══════════════════════════════════════════════════════
        //  PAGINATION
        // ══════════════════════════════════════════════════════
        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = TotalPages;

            _paginationBar.Controls.Add(new Label
            {
                Text = $"عرض {Math.Min(_allBalances.Count, (_currentPage - 1) * PageSize + 1)}-{Math.Min(_allBalances.Count, _currentPage * PageSize)} من {_allBalances.Count}",
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Width = 180,
                Height = 56,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            });

            var pnlPages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0) };
            pnlPages.Controls.Add(MakeNavBtn("›", _currentPage < total, () => { _currentPage++; LoadWarehouse(); }));

            for (int i = total; i >= 1; i--)
            {
                int pg = i; bool cur = pg == _currentPage;
                var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = cur ? Cursors.Default : Cursors.Hand, Margin = new Padding(3, 10, 3, 10) };
                btn.Paint += (s2, pe2) =>
                {
                    var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rc2 = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    if (cur)
                    {
                        using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        using (var path = RoundPath(rc2, 8)) g.FillPath(br, path);
                        using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                            g.DrawString(pg.ToString(), f, Brushes.White, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else
                    {
                        using (var path = RoundPath(rc2, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                            g.DrawString(pg.ToString(), f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!cur) btn.Click += (s2, e2) => { _currentPage = pg; LoadWarehouse(); };
                pnlPages.Controls.Add(btn);
            }

            pnlPages.Controls.Add(MakeNavBtn("‹", _currentPage > 1, () => { _currentPage--; LoadWarehouse(); }));
            _paginationBar.Controls.Add(pnlPages);
        }

        private Panel MakeNavBtn(string text, bool enabled, Action onClick)
        {
            var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(3, 10, 3, 10) };
            btn.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = RoundPath(rc2, 8)) { g.FillPath(new SolidBrush(enabled ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#F1F5F9")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                using (var f = new Font("Segoe UI", 13F)) using (var tb = new SolidBrush(enabled ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#CBD5E1")))
                    g.DrawString(text, f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            if (enabled) btn.Click += (s2, e2) => onClick();
            return btn;
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════
        private static string GetInner(Exception ex)
        {
            if (ex == null) return string.Empty;
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }

        private async void ShowSuccessToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#10B981"), ColorTranslator.FromHtml("#ECFDF5"));
        private async void ShowErrorToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#FEF2F2"));
        private async Task ShowToast(string msg, Color accent, Color bgColor)
        {
            var toast = new Panel { Size = new Size(360, 52), BackColor = bgColor, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath()) { gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(toast.Width - 20, 0, 20, 20, 270, 90); gp.AddArc(toast.Width - 20, toast.Height - 20, 20, 20, 0, 90); gp.AddArc(0, toast.Height - 20, 20, 20, 90, 90); gp.CloseFigure(); toast.Region = new Region(gp); }
            toast.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(accent, 1.5f)) using (var path = RoundPath(new Rectangle(0, 0, toast.Width - 1, toast.Height - 1), 10)) pe.Graphics.DrawPath(pen, path);
                pe.Graphics.FillRectangle(new SolidBrush(accent), 0, 8, 4, toast.Height - 16);
                using (var f = new Font("Cairo", 10.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1F2937")))
                    pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, toast.Width - 8, toast.Height), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40);
            Controls.Add(toast); toast.BringToFront();
            toast.Click += (s, e) => { try { Controls.Remove(toast); toast.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10) { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + (100 - i) / 5); await Task.Delay(8); }
            await Task.Delay(2800);
            for (int i = 0; i <= 100; i += 10) { try { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + i / 5); } catch { break; } await Task.Delay(8); }
            try { Controls.Remove(toast); toast.Dispose(); } catch { }
        }

        private void WarehouseReportForm_Load(object sender, EventArgs e) { }
    }
}