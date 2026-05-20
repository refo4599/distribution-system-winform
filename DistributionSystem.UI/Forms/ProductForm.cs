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
    public class ProductForm : Form
    {
        private readonly ProductService _service = new ProductService();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _selectedProductId = 0;

        private Guna2TextBox txtName, txtPurchasePrice, txtSalePrice, txtSearch;
        private Guna2Button btnAdd, btnEdit, btnDelete;
        private Guna2DataGridView dgvProducts;
        private Label lblCountBadge;
        private System.Threading.Timer _searchTimer;
        private Panel _paginationBar;
        private List<ProductDto> _allProducts = new List<ProductDto>();
        private int _currentPage = 1;
        private const int PageSize = 8;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allProducts.Count / (double)PageSize));

        public ProductForm()
        {
            Text = "إدارة المنتجات";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(0);

            txtName = new Guna2TextBox { Visible = false };
            txtPurchasePrice = new Guna2TextBox { Visible = false };
            txtSalePrice = new Guna2TextBox { Visible = false };
            btnEdit = new Guna2Button { Visible = false };
            btnDelete = new Guna2Button { Visible = false };

            this.SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint, true);
            this.UpdateStyles();

            BuildLayout();
            Shown += (s, e) => BeginInvoke(new Action(LoadProducts));
        }

        // ══════════════════════════════════════════════════════
        //  LAYOUT
        // ══════════════════════════════════════════════════════
        private void BuildLayout()
        {
            this.SuspendLayout();
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(new Panel { Dock = DockStyle.Fill, Height = 0, Visible = false }, 0, 1);
            root.Controls.Add(BuildTableCard(), 0, 2);
            EnableDbAll(root);
            Controls.Add(root);
            this.ResumeLayout(true);
        }

        // ── Header banner ──────────────────────────────────────
        private Panel BuildPageHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 110, Padding = new Padding(8, 8, 8, 8), BackColor = ColorTranslator.FromHtml("#EEF0F5") };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            banner.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var br = new LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#1a2f5e"),
                    ColorTranslator.FromHtml("#1565c0"),
                    LinearGradientMode.Horizontal))
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

                using (var tf = new Font("Cairo", 21F, FontStyle.Bold))
                using (var sf2 = new Font("Cairo", 10.5F))
                {
                    string title = "إدارة المنتجات";
                    string sub = "عرض وإدارة جميع الكوتشتات";
                    var szT = g.MeasureString(title, tf);
                    var szS = g.MeasureString(sub, sf2);

                    float gap = 4f;
                    float block = szT.Height + gap + szS.Height;
                    float startY = (banner.Height - block) / 2f;

                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);

                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                        g.DrawString(sub, sf2, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);

                    float lineY = startY + szT.Height + gap + szS.Height + 4f;
                    using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                        g.FillRectangle(b1, banner.Width - 44, lineY, 40, 3);
                    using (var b2 = new SolidBrush(Color.FromArgb(140, 100, 181, 246)))
                        g.FillRectangle(b2, banner.Width - 62, lineY, 14, 3);
                }
            };

            btnAdd = new Guna2Button
            {
                Text = "+ إضافة منتج",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(148, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(20, 28)
            };
            btnAdd.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAdd.Click += (s, e) => ShowAddProductPopup();
            banner.Controls.Add(btnAdd);

            pnl.Controls.Add(banner);
            return pnl;
        }

        // ══════════════════════════════════════════════════════
        //  TABLE CARD
        // ══════════════════════════════════════════════════════
        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };

            var container = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = 18,
                BorderThickness = 0,
                BorderColor = Color.Transparent
            };
            container.ShadowDecoration.Enabled = true;
            container.ShadowDecoration.Depth = 20;
            container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };

            lblCountBadge = new Label
            {
                Text = "0 منتج",
                BackColor = Color.Transparent,
                ForeColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(1, 1),
                Location = new Point(-100, -100)
            };
            topBar.Controls.Add(lblCountBadge);

            topBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;

                using (var tf = new Font("Cairo", 15F, FontStyle.Bold))
                using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                {
                    var sz = g.MeasureString("قائمة المنتجات", tf);
                    g.DrawString("قائمة المنتجات", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f);
                }

                string badge = lblCountBadge.Text;
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badge, bf);
                    int bw = (int)bsz.Width + 24, bh = 34;
                    int bx = W - bw - 20, by = (H - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2))
                    using (var br = new LinearGradientBrush(brc,
                        ColorTranslator.FromHtml("#4E73DF"),
                        ColorTranslator.FromHtml("#3B5DC9"),
                        LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };
            lblCountBadge.TextChanged += (s, e) => topBar.Invalidate();

            txtSearch = new Guna2TextBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                PlaceholderText = "ابحث عن منتج...",
                FillColor = Color.White,
                BorderColor = ColorTranslator.FromHtml("#94A3B8"),
                BorderThickness = 1,
                Font = new Font("Cairo", 10F),
                TextAlign = HorizontalAlignment.Right,
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"),
            };
            txtSearch.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");
            txtSearch.FocusedState.FillColor = Color.White;
            txtSearch.TextChanged += (s, e) =>
            {
                _searchTimer?.Dispose();
                _currentPage = 1;
                _searchTimer = new System.Threading.Timer(_ =>
                { try { BeginInvoke(new Action(LoadProducts)); } catch { } },
                null, 350, System.Threading.Timeout.Infinite);
            };

            var searchWrapper = new Panel
            {
                Width = 185,
                Height = 32,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(12, (58 - 32) / 2)
            };
            searchWrapper.Controls.Add(txtSearch);
            topBar.Controls.Add(searchWrapper);

            var searchSeparator = new Panel
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = Color.Transparent
            };
            searchSeparator.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, searchSeparator.Width, 3),
                    ColorTranslator.FromHtml("#4E73DF"),
                    ColorTranslator.FromHtml("#E2E8F0"),
                    LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, searchSeparator.Width, 3);
            };

            dgvProducts = new Guna2DataGridView
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
                ScrollBars = ScrollBars.Both,
                GridColor = Color.White,
                Padding = new Padding(0)
            };

            dgvProducts.RowTemplate.Height = 76;
            dgvProducts.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;

            dgvProducts.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvProducts.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgvProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgvProducts.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvProducts.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvProducts.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");

            dgvProducts.DefaultCellStyle.BackColor = Color.White;
            dgvProducts.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvProducts.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvProducts.DefaultCellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
            dgvProducts.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");

            try
            {
                typeof(DataGridView)
                    .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(dgvProducts, true);
            }
            catch { }

            BuildGridColumns();
            dgvProducts.CellPainting += DgvProducts_CellPainting;
            dgvProducts.CellFormatting += DgvProducts_CellFormatting;
            dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;
            dgvProducts.CellClick += DgvProducts_CellClick;
            dgvProducts.Resize += (s, e) => FitColumns();

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };
            dgvWrapper.Controls.Add(dgvProducts);

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f))
                    pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0);
            };

            container.Controls.Add(dgvWrapper);
            container.Controls.Add(_paginationBar);
            container.Controls.Add(searchSeparator);
            container.Controls.Add(topBar);
            card.Controls.Add(container);
            return card;
        }

        private void BuildGridColumns()
        {
            dgvProducts.Columns.Clear();

            void AddCol(string name, string hdr, string prop, int w) =>
                dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
                { Name = name, HeaderText = hdr, DataPropertyName = prop, Width = w });

            // ← TireSize بدل BoxesPerCarton
            AddCol("Product", "المنتج", "Name", 200);
            AddCol("TireSize", "المقاس", "TireSize", 130);
            AddCol("PurchasePrice", "سعر الشراء", "PurchasePrice", 130);
            AddCol("SalePrice", "سعر البيع", "SalePrice", 130);
            AddCol("Status", "الحالة", "", 100);
            AddCol("Actions", "الإجراءات", "", 130);

            foreach (DataGridViewColumn c in dgvProducts.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        // ══════════════════════════════════════════════════════
        //  DATA
        // ══════════════════════════════════════════════════════
        private async void LoadProducts()
        {
            try
            {
                var q = txtSearch?.Text?.Trim() ?? "";
                var all = await Task.Run(() =>
                {
                    var list = (_service.GetAll() ?? Enumerable.Empty<ProductDto>()).ToList();
                    if (!string.IsNullOrEmpty(q))
                        list = list.Where(x =>
                            (x.Name != null && x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (x.TireSize != null && x.TireSize.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        ).ToList();
                    return list;
                });

                _allProducts = all;
                _currentPage = Math.Min(_currentPage, TotalPages);
                var page = _allProducts.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
                dgvProducts.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                if (lblCountBadge != null) lblCountBadge.Text = $"{_allProducts.Count} منتج";
                RenderPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل المنتجات: {ex.Message}", "خطأ",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = TotalPages;
            var lblInfo = new Label
            {
                Text = $"عرض {Math.Min(_allProducts.Count, (_currentPage - 1) * PageSize + 1)}-{Math.Min(_allProducts.Count, _currentPage * PageSize)} من {_allProducts.Count}",
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Width = 180,
                Height = 56,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            };
            _paginationBar.Controls.Add(lblInfo);
            var pnlPages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0) };
            pnlPages.Controls.Add(MakeNavBtn("›", _currentPage < total, () => { _currentPage++; LoadProducts(); }));
            for (int i = total; i >= 1; i--)
            {
                int pg = i; bool isCurrent = pg == _currentPage;
                var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = isCurrent ? Cursors.Default : Cursors.Hand, Margin = new Padding(3, 10, 3, 10) };
                btn.Paint += (s, pe) =>
                {
                    var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    if (isCurrent)
                    {
                        using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        using (var path = RoundPath(rc, 8)) g.FillPath(br, path);
                        using (var f = new Font("Cairo", 10F, FontStyle.Bold)) g.DrawString(pg.ToString(), f, Brushes.White, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else
                    {
                        using (var path = RoundPath(rc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151"))) g.DrawString(pg.ToString(), f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!isCurrent) btn.Click += (s, e) => { _currentPage = pg; LoadProducts(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MakeNavBtn("‹", _currentPage > 1, () => { _currentPage--; LoadProducts(); }));
            _paginationBar.Controls.Add(pnlPages);
        }

        private Panel MakeNavBtn(string text, bool enabled, Action onClick)
        {
            var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(3, 10, 3, 10) };
            btn.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = RoundPath(rc, 8)) { g.FillPath(new SolidBrush(enabled ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#F1F5F9")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                using (var f = new Font("Segoe UI", 13F)) using (var tb = new SolidBrush(enabled ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#CBD5E1")))
                    g.DrawString(text, f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            if (enabled) btn.Click += (s, e) => onClick();
            return btn;
        }

        private void FitColumns()
        {
            if (dgvProducts == null || dgvProducts.Columns.Count == 0) return;
            int w = dgvProducts.ClientSize.Width;
            if (w <= 0) return;
            if (dgvProducts.ScrollBars == ScrollBars.Both || dgvProducts.ScrollBars == ScrollBars.Vertical)
                w -= SystemInformation.VerticalScrollBarWidth;

            int wProd = (int)(w * 0.26);
            int wTire = (int)(w * 0.16);
            int wPurch = (int)(w * 0.16);
            int wSale = (int)(w * 0.16);
            int wStat = (int)(w * 0.12);
            int wAct = w - wProd - wTire - wPurch - wSale - wStat;

            dgvProducts.Columns["Product"].Width = Math.Max(120, wProd);
            dgvProducts.Columns["TireSize"].Width = Math.Max(90, wTire);
            dgvProducts.Columns["PurchasePrice"].Width = Math.Max(90, wPurch);
            dgvProducts.Columns["SalePrice"].Width = Math.Max(90, wSale);
            dgvProducts.Columns["Status"].Width = Math.Max(80, wStat);
            dgvProducts.Columns["Actions"].Width = Math.Max(110, wAct);
        }

        private void ClearForm()
        {
            txtName.Text = ""; txtPurchasePrice.Text = ""; txtSalePrice.Text = "";
            _selectedProductId = 0; dgvProducts?.ClearSelection();
        }

        // ══════════════════════════════════════════════════════
        //  GRID EVENTS
        // ══════════════════════════════════════════════════════
        private void DgvProducts_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvProducts?.CurrentRow?.DataBoundItem is ProductDto dto)
            {
                _selectedProductId = dto.Id;
                txtName.Text = dto.Name;
                txtPurchasePrice.Text = dto.PurchasePrice.ToString("N2", Inv);
                txtSalePrice.Text = dto.SalePrice.ToString("N2", Inv);
            }
            else ClearForm();
        }

        private void DgvProducts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvProducts.Columns[e.ColumnIndex].Name != "Actions") return;
            var dto = dgvProducts.Rows[e.RowIndex].DataBoundItem as ProductDto;
            if (dto == null) return;

            var cell = dgvProducts.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var mouse = dgvProducts.PointToClient(Cursor.Position);
            int btnH = 32, btnY = cell.Top + (cell.Height - btnH) / 2;
            int editW = 72, delW = 32, gap = 8;
            int startX = cell.Left + (cell.Width - editW - gap - delW) / 2;

            if (new Rectangle(startX, btnY, editW, btnH).Contains(mouse))
                ShowAddProductPopup(dto);
            else if (new Rectangle(startX + editW + gap, btnY, delW, btnH).Contains(mouse))
            {
                if (ShowDeleteConfirm(dto.Name))
                    try { _service.Delete(dto.Id); _currentPage = Math.Min(_currentPage, TotalPages); LoadProducts(); }
                    catch (Exception ex) { ShowErrorDialog(dto.Name, ex.Message); }
            }
        }

        private void DgvProducts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var col = dgvProducts.Columns[e.ColumnIndex].Name;
            var dto = dgvProducts.Rows[e.RowIndex].DataBoundItem as ProductDto;
            if (dto == null) return;

            if (col == "PurchasePrice")
            {
                decimal v = 0; try { v = Convert.ToDecimal(e.Value); } catch { v = dto.PurchasePrice; }
                e.Value = v.ToString("N2", Inv) + " جنيه";
                e.CellStyle.ForeColor = ColorTranslator.FromHtml("#059669");
                e.CellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
                e.FormattingApplied = true;
            }
            else if (col == "SalePrice")
            {
                decimal v = 0; try { v = Convert.ToDecimal(e.Value); } catch { v = dto.SalePrice; }
                e.Value = v.ToString("N2", Inv) + " جنيه";
                e.CellStyle.ForeColor = ColorTranslator.FromHtml("#059669");
                e.CellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
                e.FormattingApplied = true;
            }
        }

        // ══════════════════════════════════════════════════════
        //  CELL PAINTING
        // ══════════════════════════════════════════════════════
        private void DgvProducts_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1)
                {
                    e.Handled = true;
                    var g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    using (var br = new LinearGradientBrush(e.CellBounds,
                        ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"),
                        LinearGradientMode.Vertical))
                        g.FillRectangle(br, e.CellBounds);

                    using (var font = new Font("Cairo", 11F, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds,
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });

                    using (var sep2 = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    {
                        g.DrawLine(sep2, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6);
                        g.DrawLine(sep2, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6);
                    }
                    return;
                }

                if (e.RowIndex < 0) return;

                bool sel = dgvProducts.Rows[e.RowIndex].Selected;
                Color bg = sel
                    ? ColorTranslator.FromHtml("#EEF2FF")
                    : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));

                var col = dgvProducts.Columns[e.ColumnIndex].Name;

                if (col == "Product") PaintProductCell(e, bg);
                else if (col == "Status") PaintStatusCell(e, bg);
                else if (col == "Actions") PaintActionsCell(e, bg);
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
            var g = e.Graphics;
            g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var dto = dgvProducts.Rows[e.RowIndex].DataBoundItem as ProductDto;
            string name = dto?.Name ?? "";
            if (string.IsNullOrEmpty(name)) { g.ResetClip(); return; }

            var avColors = new[] { "#4E73DF", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#0891B2", "#DC2626" };
            int avSize = 36, pad = 14;
            int avX = e.CellBounds.Right - avSize - pad;
            int avY = e.CellBounds.Top + (e.CellBounds.Height - avSize) / 2;

            using (var sh = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                g.FillEllipse(sh, avX + 2, avY + 2, avSize, avSize);

            using (var avBrush = new SolidBrush(ColorTranslator.FromHtml(avColors[e.RowIndex % avColors.Length])))
                g.FillEllipse(avBrush, avX, avY, avSize, avSize);

            string letter = name.Length > 0 ? name[0].ToString() : "?";
            using (var lf = new Font("Cairo", 13F, FontStyle.Bold))
            {
                var ls = g.MeasureString(letter, lf);
                g.DrawString(letter, lf, Brushes.White,
                    avX + (avSize - ls.Width) / 2f,
                    avY + (avSize - ls.Height) / 2f);
            }

            float textRight = avX - 8f;
            float textW = textRight - e.CellBounds.Left - 4f;
            using (var nf = new Font("Cairo", 13F, FontStyle.Bold))
            using (var nb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, nf, nb,
                    new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, textW, e.CellBounds.Height),
                    _sfCenter);
            g.ResetClip();
        }

        private void PaintStatusCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics;
            g.SetClip(e.CellBounds);
            g.FillRectangle(new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int pw = 64, ph = 28;
            var pr = new Rectangle(e.CellBounds.Left + (e.CellBounds.Width - pw) / 2, e.CellBounds.Top + (e.CellBounds.Height - ph) / 2, pw, ph);
            using (var path = RoundPath(pr, ph / 2))
            {
                g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#ECFDF5")), path);
                g.DrawPath(new Pen(ColorTranslator.FromHtml("#A7F3D0"), 1f), path);
            }
            int ds = 7, dx = pr.Left + 10, dy = pr.Top + (pr.Height - ds) / 2;
            g.FillEllipse(new SolidBrush(ColorTranslator.FromHtml("#10B981")), dx, dy, ds, ds);
            using (var f = new Font("Cairo", 10F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#059669")))
            {
                var sz = g.MeasureString("نشط", f);
                g.DrawString("نشط", f, tb, pr.Left + (pr.Width - sz.Width) / 2f + 4, pr.Top + (pr.Height - sz.Height) / 2f);
            }
            g.ResetClip();
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics;
            g.SetClip(e.CellBounds);
            g.FillRectangle(new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int btnH = 32, btnY = e.CellBounds.Top + (e.CellBounds.Height - btnH) / 2;
            int editW = 72, delW = 32, gap = 8;
            int startX = e.CellBounds.Left + (e.CellBounds.Width - editW - gap - delW) / 2;

            var editRect = new Rectangle(startX, btnY, editW, btnH);
            using (var path = RoundPath(editRect, 8))
            {
                g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path);
                g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path);
            }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
            {
                var sz = g.MeasureString("تعديل", f);
                g.DrawString("تعديل", f, tb, editRect.Left + (editRect.Width - sz.Width) / 2f, editRect.Top + (editRect.Height - sz.Height) / 2f);
            }

            var delRect = new Rectangle(startX + editW + gap, btnY, delW, btnH);
            using (var path2 = RoundPath(delRect, 8))
            {
                g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path2);
                g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path2);
            }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
            {
                int cx = delRect.Left + delRect.Width / 2, cy = delRect.Top + delRect.Height / 2;
                g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4);
                g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6);
                g.DrawRectangle(pen, cx - 4, cy - 3, 8, 7);
                g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 3);
                g.DrawLine(pen, cx + 1, cy - 1, cx + 1, cy + 3);
            }
            g.ResetClip();
        }

        // ══════════════════════════════════════════════════════
        //  POPUP — إضافة / تعديل منتج
        // ══════════════════════════════════════════════════════
        private async void ShowAddProductPopup(ProductDto editDto = null)
        {
            bool isEdit = editDto != null;

            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = sc.Location,
                Size = sc.Size,
                BackColor = Color.Black,
                Opacity = 0.55,
                ShowInTaskbar = false,
                TopMost = true
            };
            overlay.Show(this);

            var popupForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(500, 460),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };
            popupForm.Location = new Point(
                sc.Left + (sc.Width - popupForm.Width) / 2,
                sc.Top + (sc.Height - popupForm.Height) / 2);

            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 40, 40, 180, 90);
                rgn.AddArc(popupForm.Width - 40, 0, 40, 40, 270, 90);
                rgn.AddArc(popupForm.Width - 40, popupForm.Height - 40, 40, 40, 0, 90);
                rgn.AddArc(0, popupForm.Height - 40, 40, 40, 90, 90);
                rgn.CloseFigure();
                popupForm.Region = new Region(rgn);
            }

            popupForm.FormClosed += (s, e) =>
            { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => popupForm.Close();

            var popup = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White, BackColor = Color.White };
            popup.ShadowDecoration.Enabled = true;
            popup.ShadowDecoration.Depth = 32;
            popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            popupForm.Controls.Add(popup);

            Action closePopup = () => { try { popupForm.Close(); popupForm.Dispose(); } catch { } };

            // ── HEADER ──────────────────────────────────────────────────────────────
            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent, RightToLeft = RightToLeft.Yes };
            pnlHead.Paint += (s2, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 8; x < pnlHead.Width; x += 20)
                        for (int y = 6; y < pnlHead.Height; y += 20)
                            g.FillEllipse(db, x, y, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                    g.FillEllipse(cb, pnlHead.Width - 100, -40, 180, 180);

                string titleStr = isEdit ? "تعديل بيانات المنتج" : "إضافة منتج جديد";
                string subStr = isEdit ? "عدّل البيانات ثم اضغط تحديث" : "أدخل بيانات الكوتش ثم اضغط حفظ";
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(titleStr, tf); g.DrawString(titleStr, tf, tb2, pnlHead.Width - tsz.Width - 20, 16); }
                using (var sf2 = new Font("Cairo", 10F)) using (var sb2 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString(subStr, sf2); g.DrawString(subStr, sf2, sb2, pnlHead.Width - ssz.Width - 20, 54); }
            };

            var btnClose = new Guna2Button
            {
                Size = new Size(30, 30),
                Text = "X",
                FillColor = Color.FromArgb(50, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 8,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(70, 255, 255, 255),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnClose.HoverState.FillColor = Color.FromArgb(80, 255, 255, 255);
            btnClose.Click += (s7, e7) => closePopup();
            pnlHead.Controls.Add(btnClose);
            btnClose.BringToFront();
            pnlHead.Layout += (s8, e8) => btnClose.Location = new Point(25, 20);

            // ── BODY ────────────────────────────────────────────────────────────────
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(24, 14, 24, 8),
                RightToLeft = RightToLeft.Yes
            };

            Panel MakeTxtWrapped(string placeholder, out Guna2TextBox txtOut)
            {
                var t = new Guna2TextBox
                {
                    Dock = DockStyle.Fill,
                    BorderRadius = 8,
                    FillColor = Color.White,
                    BorderColor = ColorTranslator.FromHtml("#C7D2FE"),
                    BorderThickness = 1,
                    Font = new Font("Cairo", 11F),
                    PlaceholderText = placeholder,
                    PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"),
                    ForeColor = ColorTranslator.FromHtml("#0F172A"),
                    TextAlign = HorizontalAlignment.Right,
                    RightToLeft = RightToLeft.No
                };
                t.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");
                t.FocusedState.FillColor = ColorTranslator.FromHtml("#F5F8FF");
                txtOut = t;
                var w = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
                w.Controls.Add(t);
                return w;
            }

            Label MakeLbl(string text) => new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Cairo", 9.5F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#1e3a6e"),
                TextAlign = ContentAlignment.BottomRight,
                BackColor = Color.Transparent,
                RightToLeft = RightToLeft.No
            };

            Label MakeErr() => new Label
            {
                Dock = DockStyle.Top,
                Height = 0,
                Font = new Font("Cairo", 9F),
                ForeColor = ColorTranslator.FromHtml("#EF4444"),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Visible = false,
                RightToLeft = RightToLeft.No
            };

            // ── الحقول ────────────────────────────────────────────────────────────
            var errName = MakeErr();
            var wrapName = MakeTxtWrapped("ادخل اسم المنتج", out var popTxtName);
            var lblName = MakeLbl("اسم المنتج *");

            // ← TireSize بدل BoxesPerCarton
            var errTire = MakeErr();
            var wrapTire = MakeTxtWrapped("مثال: 205/55R16", out var popTxtTire);
            var lblTire = MakeLbl("مقاس العجلة *");

            var spacerTop = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };
            var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };
            var spacerMid = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };
            var spacerTop2 = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };
            var divider2 = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };
            var spacerMid2 = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };

            // ── الأسعار جنباً لجنب ────────────────────────────────────────────────
            var tblPrices = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                RightToLeft = RightToLeft.Yes
            };
            tblPrices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tblPrices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var colPurch = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 6, 0) };
            var errPurch = MakeErr();
            var wrapPurch = MakeTxtWrapped("0.00", out var popTxtPurch); wrapPurch.Dock = DockStyle.Top;
            var lblPurch = MakeLbl("سعر الشراء *"); lblPurch.Dock = DockStyle.Top;
            colPurch.Controls.Add(errPurch); colPurch.Controls.Add(wrapPurch); colPurch.Controls.Add(lblPurch);

            var colSale = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(6, 0, 0, 0) };
            var errSale = MakeErr();
            var wrapSale = MakeTxtWrapped("0.00", out var popTxtSale); wrapSale.Dock = DockStyle.Top;
            var lblSale = MakeLbl("سعر البيع *"); lblSale.Dock = DockStyle.Top;
            colSale.Controls.Add(errSale); colSale.Controls.Add(wrapSale); colSale.Controls.Add(lblSale);

            tblPrices.Controls.Add(colPurch, 0, 0);
            tblPrices.Controls.Add(colSale, 1, 0);
            var lblPrices = MakeLbl("الأسعار");

            // ترتيب الـ Controls (Dock.Top — من الأسفل للأعلى)
            body.Controls.Add(tblPrices);
            body.Controls.Add(lblPrices);
            body.Controls.Add(spacerMid2);
            body.Controls.Add(divider2);
            body.Controls.Add(spacerTop2);
            body.Controls.Add(errTire);
            body.Controls.Add(wrapTire);
            body.Controls.Add(lblTire);
            body.Controls.Add(spacerMid);
            body.Controls.Add(divider);
            body.Controls.Add(spacerTop);
            body.Controls.Add(errName);
            body.Controls.Add(wrapName);
            body.Controls.Add(lblName);

            // تعبئة بيانات التعديل
            if (isEdit)
            {
                popTxtName.Text = editDto.Name;
                popTxtTire.Text = editDto.TireSize;
                popTxtPurch.Text = editDto.PurchasePrice.ToString("N2", Inv);
                popTxtSale.Text = editDto.SalePrice.ToString("N2", Inv);
            }

            // ── FOOTER ─────────────────────────────────────────────────────────────
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = ColorTranslator.FromHtml("#F8FAFF"),
                Padding = new Padding(24, 10, 24, 14)
            };
            footer.Paint += (s6, pe) =>
            {
                var g = pe.Graphics;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 2f))
                    g.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 2, footer.Width, 2),
                    ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"),
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 2, footer.Width, 2);
            };

            var btnSave = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = isEdit ? "تحديث المنتج" : "حفظ المنتج",
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#4E73DF"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 13F, FontStyle.Bold),
                Animated = true
            };
            btnSave.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9");
            btnSave.ShadowDecoration.Enabled = true;
            btnSave.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223);
            btnSave.ShadowDecoration.Depth = 10;

            btnSave.Click += async (s6, e6) =>
            {
                errName.Visible = errTire.Visible = errPurch.Visible = errSale.Visible = false;
                errName.Height = errTire.Height = errPurch.Height = errSale.Height = 0;
                popTxtName.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                popTxtTire.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                popTxtPurch.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                popTxtSale.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                bool valid = true;

                if (string.IsNullOrWhiteSpace(popTxtName.Text))
                {
                    popTxtName.BorderColor = ColorTranslator.FromHtml("#EF4444");
                    errName.Text = "• اسم المنتج مطلوب";
                    errName.Visible = true; errName.Height = 18; valid = false;
                }
                if (string.IsNullOrWhiteSpace(popTxtTire.Text))
                {
                    popTxtTire.BorderColor = ColorTranslator.FromHtml("#EF4444");
                    errTire.Text = "• مقاس العجلة مطلوب";
                    errTire.Visible = true; errTire.Height = 18; valid = false;
                }
                if (!decimal.TryParse(popTxtPurch.Text, NumberStyles.Number, Inv, out decimal pp) || pp <= 0)
                {
                    popTxtPurch.BorderColor = ColorTranslator.FromHtml("#EF4444");
                    errPurch.Text = "• أدخل قيمة أكبر من صفر";
                    errPurch.Visible = true; errPurch.Height = 18; valid = false;
                }
                if (!decimal.TryParse(popTxtSale.Text, NumberStyles.Number, Inv, out decimal sp) || sp <= 0)
                {
                    popTxtSale.BorderColor = ColorTranslator.FromHtml("#EF4444");
                    errSale.Text = "• أدخل قيمة أكبر من صفر";
                    errSale.Visible = true; errSale.Height = 18; valid = false;
                }
                if (!valid) return;

                btnSave.Enabled = false; btnSave.Text = "جارٍ الحفظ...";
                await Task.Delay(500);

                try
                {
                    var dto = new ProductDto
                    {
                        Name = popTxtName.Text.Trim(),
                        TireSize = popTxtTire.Text.Trim(),
                        PurchasePrice = pp,
                        SalePrice = sp
                    };
                    if (!isEdit) _service.AddProduct(dto);
                    else { dto.Id = editDto.Id; _service.Update(dto); }
                    LoadProducts();
                    closePopup();
                }
                catch (Exception ex)
                {
                    btnSave.Enabled = true;
                    btnSave.Text = isEdit ? "تحديث المنتج" : "حفظ المنتج";
                    MessageBox.Show("خطأ: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            footer.Controls.Add(btnSave);
            popup.Controls.Add(body);
            popup.Controls.Add(footer);
            popup.Controls.Add(pnlHead);
            popupForm.Shown += (s5, e5) => popTxtName.Focus();
            popupForm.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════
        private static readonly System.Reflection.PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        private static void EnableDbAll(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                try { _dbProp?.SetValue(ctrl, true); } catch { }
                if (ctrl.Controls.Count > 0) EnableDbAll(ctrl);
            }
        }

        private void ShowErrorDialog(string entityName, string rawError)
        {
            bool isFk = rawError != null && (
                rawError.Contains("FOREIGN KEY") || rawError.Contains("REFERENCE") ||
                rawError.Contains("service operation") || rawError.Contains("constraint"));
            string title = isFk ? "لا يمكن حذف المنتج" : "حدث خطأ";
            string line1 = isFk ? $"لا يمكن حذف المنتج \"{entityName}\"" : "حدث خطأ غير متوقع";
            string line2 = isFk ? "لأن لديه فواتير أو حركات مخزن مسجّلة في النظام." : rawError;
            string line3 = isFk ? "يجب حذف الفواتير المرتبطة أولاً ثم المحاولة مجدداً." : "";

            var dlg = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(440, 260),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };
            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90);
                rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90);
                rgn.CloseFigure(); dlg.Region = new Region(rgn);
            }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.Transparent };
            header.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, header.Width, header.Height);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#B45309"), ColorTranslator.FromHtml("#D97706"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, header.Width - tsz.Width - 20, 12); }
                using (var sf2 = new Font("Cairo", 9.5F)) using (var sb2 = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
                    g.DrawString("تعذّر تنفيذ عملية الحذف", sf2, sb2, header.Width - 222, 50);
            };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 16, 22, 0) };
            var msgPanel = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            msgPanel.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, msgPanel.Width - 1, msgPanel.Height - 1);
                using (var path = RoundPath(rc, 12)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFFBEB")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FDE68A"), 1.5f), path); }
                using (var f1 = new Font("Cairo", 11.5F, FontStyle.Bold)) using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#92400E")))
                    g.DrawString(line1, f1, b1, new RectangleF(12, 10, msgPanel.Width - 24, 28), new StringFormat { Alignment = StringAlignment.Far });
                using (var f2 = new Font("Cairo", 10.5F)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#78350F")))
                    g.DrawString(line2, f2, b2, new RectangleF(12, 40, msgPanel.Width - 24, 26), new StringFormat { Alignment = StringAlignment.Far });
                if (!string.IsNullOrEmpty(line3))
                    using (var f3 = new Font("Cairo", 9.5F, FontStyle.Italic)) using (var b3 = new SolidBrush(ColorTranslator.FromHtml("#A16207")))
                        g.DrawString(line3, f3, b3, new RectangleF(12, 66, msgPanel.Width - 24, 24), new StringFormat { Alignment = StringAlignment.Far });
            };
            body.Controls.Add(msgPanel);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White, Padding = new Padding(24, 10, 24, 14) };
            var btnOk = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = "حسناً، فهمت",
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#D97706"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Animated = true
            };
            btnOk.HoverState.FillColor = ColorTranslator.FromHtml("#B45309");
            btnOk.Click += (s, e) => dlg.Close();
            footer.Controls.Add(btnOk);
            root.Controls.Add(body); root.Controls.Add(footer); root.Controls.Add(header);
            dlg.Controls.Add(root);
            dlg.KeyPreview = true;
            dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Return) dlg.Close(); };
            dlg.ShowDialog(this);
        }

        private bool ShowDeleteConfirm(string productName)
        {
            bool result = false;
            var dlg = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(420, 260),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };
            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90);
                rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90);
                rgn.CloseFigure(); dlg.Region = new Region(rgn);
            }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.Transparent };
            header.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, header.Width, header.Height);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#C0392B"), ColorTranslator.FromHtml("#E74C3C"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var tf = new Font("Cairo", 18F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                { var tsz = g.MeasureString("حذف المنتج", tf); g.DrawString("حذف المنتج", tf, tb, header.Width - tsz.Width - 20, 14); }
                using (var sf2 = new Font("Cairo", 10F)) using (var sb2 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                    g.DrawString("هذا الإجراء لا يمكن التراجع عنه", sf2, sb2, header.Width - 238, 52);
            };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 18, 28, 0) };
            var nameBox = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = ColorTranslator.FromHtml("#FEF2F2") };
            nameBox.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, nameBox.Width - 1, nameBox.Height - 1);
                using (var path = RoundPath(rc, 10)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1.5f), path); }
                using (var f = new Font("Cairo", 12F, FontStyle.Bold)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#B91C1C")))
                    g.DrawString($"  {productName}  ", f, b, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            var lblQ = new Label { Text = "هل أنت متأكد من حذف هذا المنتج؟", Font = new Font("Cairo", 12F), ForeColor = ColorTranslator.FromHtml("#374151"), Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            body.Controls.Add(nameBox); body.Controls.Add(lblQ);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(24, 12, 24, 20) };
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var btnCancel = new Guna2Button { Dock = DockStyle.Fill, Text = "إلغاء", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#F1F5F9"), ForeColor = ColorTranslator.FromHtml("#64748B"), BorderColor = ColorTranslator.FromHtml("#E2E8F0"), BorderThickness = 1, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(0, 0, 6, 0) };
            btnCancel.HoverState.FillColor = ColorTranslator.FromHtml("#E2E8F0"); btnCancel.Click += (s, e) => dlg.Close();
            var btnConfirm = new Guna2Button { Dock = DockStyle.Fill, Text = "حذف", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#EF4444"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Animated = true };
            btnConfirm.HoverState.FillColor = ColorTranslator.FromHtml("#DC2626");
            btnConfirm.Click += (s, e) => { result = true; dlg.Close(); };
            tbl.Controls.Add(btnCancel, 0, 0); tbl.Controls.Add(btnConfirm, 1, 0);
            footer.Controls.Add(tbl);
            root.Controls.Add(body); root.Controls.Add(footer); root.Controls.Add(header);
            dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) dlg.Close(); };
            dlg.ShowDialog(this);
            return result;
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

    public static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush brush,
            int x, int y, int width, int height, int radius)
        {
            try
            {
                int d = radius * 2;
                using (var path = new GraphicsPath())
                {
                    path.AddArc(x, y, d, d, 180, 90);
                    path.AddArc(x + width - d, y, d, d, 270, 90);
                    path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
                    path.AddArc(x, y + height - d, d, d, 90, 90);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                }
            }
            catch { g.FillRectangle(brush, x, y, width, height); }
        }
    }
}