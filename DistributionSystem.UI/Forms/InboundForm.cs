using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public partial class InboundForm : Form
    {
        private readonly InboundService _inboundService = new InboundService();
        private Guna2DataGridView dgvNew;
        private Guna2TextBox txtSearchNew;
        private Label lblCountBadge;
        private Guna2Button btnAddNew;
        private System.Threading.Timer _searchTimer;

        private List<InboundOrderDto> _allInbounds = new List<InboundOrderDto>();
        private int _currentPage = 1;
        private const int PageSize = 6;
        private Panel _paginationBar;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allInbounds.Count / (double)PageSize));

        public InboundForm()
        {
            InitializeComponent();
            HideOldControls();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            BuildNewUI();
            Shown += (s, e) => BeginInvoke(new Action(async () => await LoadInboundsAsync()));
        }

        private void HideOldControls()
        {
            try { foreach (Control c in this.Controls.Cast<Control>().ToList()) if (!(c is TableLayoutPanel)) c.Visible = false; }
            catch { }
        }

        private void BuildNewUI()
        {
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5"); Padding = new Padding(0);
            this.SuspendLayout();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Padding = new Padding(0) };
            root.SuspendLayout();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildTableCard(), 0, 1);
            root.ResumeLayout(false);
            EnableDbAll(root);
            Controls.Add(root); root.BringToFront();
            this.ResumeLayout(true);
        }

        private static readonly PropertyInfo _dbProp = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent) { foreach (Control ctrl in parent.Controls) { try { _dbProp?.SetValue(ctrl, true); } catch { } if (ctrl.Controls.Count > 0) EnableDbAll(ctrl); } }
        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        private GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2; var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        // ??????????????????????????????????????????????????????
        //  HEADER
        // ??????????????????????????????????????????????????????
        private Panel BuildPageHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 50, Padding = new Padding(0), BackColor = ColorTranslator.FromHtml("#EEF0F5") };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            banner.Paint += (s, e) =>
            {
                var g = e.Graphics; var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    for (int x = 10; x < banner.Width; x += 22) for (int y = 8; y < banner.Height; y += 22) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                { g.FillEllipse(cb, banner.Width - 130, -50, 220, 220); g.FillEllipse(cb, banner.Width - 30, 20, 160, 160); }
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var sf2f = new Font("Cairo", 10.5F))
                {
                    string title = "≈œ«—… «·Ê«—œ"; string sub = "⁄—÷ Ê≈œ«—… Ã„Ì⁄ √Ê«„— «·Ê«—œ";
                    var szT = g.MeasureString(title, tf); var szS = g.MeasureString(sub, sf2f);
                    float gap = 4f; float block = szT.Height + gap + szS.Height; float startY = (banner.Height - block) / 2f;
                    using (var tb = new SolidBrush(Color.White)) g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);
                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.DrawString(sub, sf2f, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);
                    float lineY = startY + szT.Height + gap + szS.Height + 4f;
                    using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6"))) g.FillRectangle(b1, banner.Width - 44, lineY, 40, 3);
                    using (var b2 = new SolidBrush(Color.FromArgb(140, 100, 181, 246))) g.FillRectangle(b2, banner.Width - 62, lineY, 14, 3);
                }
            };
            btnAddNew = new Guna2Button
            {
                Text = "+ ≈÷«›… Ê«—œ",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(148, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(20, 10)
            };
            btnAddNew.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAddNew.Click += (s, e) => ShowInboundPopup();
            banner.Controls.Add(btnAddNew);
            pnl.Controls.Add(banner); return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  TABLE CARD
        // ??????????????????????????????????????????????????????
        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblCountBadge = new Label { Text = "0 Ê«—œ", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblCountBadge.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblCountBadge);

            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("”Ã· √Ê«„— «·Ê«—œ", tf); g.DrawString("”Ã· √Ê«„— «·Ê«—œ", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
                string badge = lblCountBadge?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badge, bf); int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2)) using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };

            txtSearchNew = new Guna2TextBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                PlaceholderText = "«»ÕÀ ⁄‰ √„— Ê«—œ...",
                FillColor = Color.White,
                BorderColor = ColorTranslator.FromHtml("#94A3B8"),
                BorderThickness = 1,
                Font = new Font("Cairo", 10F),
                TextAlign = HorizontalAlignment.Right,
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"),
            };
            txtSearchNew.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");
            txtSearchNew.FocusedState.FillColor = Color.White;
            txtSearchNew.TextChanged += (s, e) =>
            {
                _searchTimer?.Dispose(); _currentPage = 1;
                _searchTimer = new System.Threading.Timer(async _ =>
                { try { await (Task)Invoke(new Func<Task>(LoadInboundsAsync)); } catch { } }, null, 350, System.Threading.Timeout.Infinite);
            };
            var searchWrapper = new Panel { Width = 185, Height = 32, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Top, Location = new Point(12, (58 - 32) / 2) };
            searchWrapper.Controls.Add(txtSearchNew);
            topBar.Controls.Add(searchWrapper);

            var searchSeparator = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSeparator.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSeparator.Width, 3), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, searchSeparator.Width, 3);
            };

            dgvNew = new Guna2DataGridView
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
            dgvNew.RowTemplate.Height = 76;
            dgvNew.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvNew.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvNew.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgvNew.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgvNew.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvNew.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvNew.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");
            dgvNew.DefaultCellStyle.BackColor = Color.White;
            dgvNew.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvNew.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvNew.DefaultCellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
            dgvNew.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgvNew.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgvNew, true); } catch { }

            BuildColumns();
            dgvNew.CellPainting += Dgv_CellPainting;
            dgvNew.CellClick += Dgv_CellClick;
            dgvNew.Resize += (s, e) => FitColumns();

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrapper.Controls.Add(dgvNew);

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) =>
            { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0); };

            container.Controls.Add(dgvWrapper);
            container.Controls.Add(_paginationBar);
            container.Controls.Add(searchSeparator);
            container.Controls.Add(topBar);
            card.Controls.Add(container); return card;
        }

        // ??????????????????????????????????????????????????????
        //  COLUMNS
        // ??????????????????????????????????????????????????????
        private void BuildColumns()
        {
            dgvNew.Columns.Clear();
            void Add(string n, string h, string p, int w) =>
                dgvNew.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w });

            Add("CustomerName", "«·⁄„Ì·", "CustomerName", 200);
            Add("ProductName", "«·„‰ Ã", "ProductName", 180);
            Add("Quantity", "«·ﬂ„Ì…", "Quantity", 110);
            Add("TotalValue", "«·≈Ã„«·Ì", "TotalValue", 120);
            Add("CreatedAt", "«· «—ÌŒ", "CreatedAt", 145);
            Add("Actions", "«·≈Ã—«¡« ", "", 130);

            foreach (DataGridViewColumn c in dgvNew.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        // ??????????????????????????????????????????????????????
        //  DATA
        // ??????????????????????????????????????????????????????
        private async Task LoadInboundsAsync()
        {
            try
            {
                var q = txtSearchNew?.Text?.Trim() ?? "";
                var all = await Task.Run(() =>
                {
                    var list = (_inboundService.GetAllInboundOrders() ?? Enumerable.Empty<InboundOrderDto>()).ToList();
                    if (!string.IsNullOrEmpty(q))
                        list = list.Where(x =>
                            (x.CustomerName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (x.ProductName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            x.Id.ToString().Contains(q)).ToList();
                    return list;
                });
                _allInbounds = all;
                _currentPage = Math.Min(_currentPage, TotalPages);
                var page = _allInbounds.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
                dgvNew.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                if (lblCountBadge != null) lblCountBadge.Text = $"{_allInbounds.Count} Ê«—œ";
                RenderPagination();
            }
            catch (Exception ex)
            { MessageBox.Show($"›‘·  Õ„Ì· √Ê«„— «·Ê«—œ: {ex.Message}", "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void FitColumns()
        {
            if (dgvNew == null || dgvNew.Columns.Count == 0) return;
            int w = dgvNew.ClientSize.Width; if (w <= 0) return;
            int wAct = 130, wDate = 145, wTotal = 110, wQty = 110;
            int rest = w - wAct - wDate - wTotal - wQty;
            dgvNew.Columns["CustomerName"].Width = Math.Max(120, (int)(rest * 0.55));
            dgvNew.Columns["ProductName"].Width = Math.Max(100, rest - dgvNew.Columns["CustomerName"].Width);
            dgvNew.Columns["Quantity"].Width = wQty;
            dgvNew.Columns["TotalValue"].Width = wTotal;
            dgvNew.Columns["CreatedAt"].Width = wDate;
            dgvNew.Columns["Actions"].Width = wAct;
        }

        // ??????????????????????????????????????????????????????
        //  PAGINATION
        // ??????????????????????????????????????????????????????
        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = TotalPages;
            _paginationBar.Controls.Add(new Label
            {
                Text = $"⁄—÷ {Math.Min(_allInbounds.Count, (_currentPage - 1) * PageSize + 1)}-{Math.Min(_allInbounds.Count, _currentPage * PageSize)} „‰ {_allInbounds.Count}",
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
            pnlPages.Controls.Add(MakeNavBtn("õ", _currentPage < total, () => { _currentPage++; _ = LoadInboundsAsync(); }));
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
                        using (var f = new Font("Cairo", 10F, FontStyle.Bold)) g.DrawString(pg.ToString(), f, Brushes.White, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else
                    {
                        using (var path = RoundPath(rc2, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                            g.DrawString(pg.ToString(), f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!cur) btn.Click += (s2, e2) => { _currentPage = pg; _ = LoadInboundsAsync(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MakeNavBtn("ã", _currentPage > 1, () => { _currentPage--; _ = LoadInboundsAsync(); }));
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

        // ??????????????????????????????????????????????????????
        //  GRID EVENTS
        // ??????????????????????????????????????????????????????
        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var colName = dgvNew.Columns[e.ColumnIndex].Name;
            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as InboundOrderDto;
            if (dto == null || colName != "Actions") return;

            var cell = dgvNew.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var mouse = dgvNew.PointToClient(Cursor.Position);
            int btnH = 32, btnY = cell.Top + (cell.Height - btnH) / 2, editW = 62, delW = 32, gap = 8;
            int startX = cell.Left + (cell.Width - editW - gap - delW) / 2;

            if (new Rectangle(startX, btnY, editW, btnH).Contains(mouse))
                ShowInboundPopup(dto);
            else if (new Rectangle(startX + editW + gap, btnY, delW, btnH).Contains(mouse))
                if (ShowDeleteConfirm($"√„— #{dto.Id}"))
                    try { _inboundService.DeleteInboundOrder(dto.Id); _ = LoadInboundsAsync(); }
                    catch (Exception ex) { ShowErrorDialog($"√„— #{dto.Id}", ex.Message); }
        }

        // ??????????????????????????????????????????????????????
        //  CELL PAINTING
        // ??????????????????????????????????????????????????????
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1)
                {
                    e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(e.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical))
                        g.FillRectangle(br, e.CellBounds);
                    using (var font = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); g.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
                    return;
                }
                if (e.RowIndex < 0) return;
                bool sel = dgvNew.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                var col = dgvNew.Columns[e.ColumnIndex].Name;
                if (col == "CustomerName") PaintCustomerCell(e, bg);
                else if (col == "TotalValue") PaintAmountCell(e, bg);
                else if (col == "Quantity") PaintQuantityCell(e, bg);
                else if (col == "CreatedAt") PaintDateCell(e, bg);
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

        private void PaintCustomerCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics;
            g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as InboundOrderDto;
            string name = dto?.CustomerName ?? "";
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

        private void PaintQuantityCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as InboundOrderDto; if (dto == null) return;
            string txt = $"{dto.Quantity} ﬁÿ⁄…";
            int bw = 100, bh = 28, bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var brc = new Rectangle(bx, by, bw, bh);
            using (var path = RoundPath(brc, bh / 2)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#DBEAFE")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }
            using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
                g.DrawString(txt, f, tb, brc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintAmountCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as InboundOrderDto; if (dto == null) return;
            string txt = dto.TotalValue.ToString("N2") + " Ã";
            int bw = 110, bh = 28, bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var brc = new Rectangle(bx, by, bw, bh);
            using (var path = RoundPath(brc, bh / 2)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#D1FAE5")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#6EE7B7"), 1f), path); }
            using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#065F46")))
                g.DrawString(txt, f, tb, brc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintDateCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            string dateText = ""; DateTime local = DateTime.MinValue;
            if (e.Value is DateTime dt) local = dt;
            else if (e.Value != null && DateTime.TryParse(e.Value.ToString(), out DateTime parsed)) local = parsed;
            if (local != DateTime.MinValue)
            {
                var display = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second, DateTimeKind.Local);
                dateText = display.ToString("yyyy/MM/dd  HH:mm");
            }
            using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                g.DrawString(dateText, f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            int btnH = 32, btnY = e.CellBounds.Top + (e.CellBounds.Height - btnH) / 2, editW = 62, delW = 32, gap = 8;
            int startX = e.CellBounds.Left + (e.CellBounds.Width - editW - gap - delW) / 2;
            var editRect = new Rectangle(startX, btnY, editW, btnH);
            using (var path = RoundPath(editRect, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
            { var sz = g.MeasureString(" ⁄œÌ·", f); g.DrawString(" ⁄œÌ·", f, tb, editRect.Left + (editRect.Width - sz.Width) / 2f, editRect.Top + (editRect.Height - sz.Height) / 2f); }
            var delRect = new Rectangle(startX + editW + gap, btnY, delW, btnH);
            using (var path2 = RoundPath(delRect, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path2); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path2); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
            { int cx = delRect.Left + delRect.Width / 2, cy = delRect.Top + delRect.Height / 2; g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4); g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6); g.DrawRectangle(pen, cx - 4, cy - 3, 8, 7); g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 3); g.DrawLine(pen, cx + 1, cy - 1, cx + 1, cy + 3); }
            g.ResetClip();
        }

        // ??????????????????????????????????????????????????????
        //  POPUP ó «·ﬂ„Ì… »«·ﬁÿ⁄ ›ﬁÿ° »œÊ‰  Õﬁﬁ „‰ «·Œ“‰…
        // ??????????????????????????????????????????????????????
        private void ShowInboundPopup(InboundOrderDto editDto = null)
        {
            bool isEdit = editDto != null;
            var sc = Screen.FromControl(this).WorkingArea;

            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.55, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);

            int pfH = Math.Min(sc.Height - 60, 700);
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(490, pfH), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.No, RightToLeftLayout = false };
            pf.Location = new Point(sc.Left + (sc.Width - pf.Width) / 2, sc.Top + (sc.Height - pf.Height) / 2);
            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 40, 40, 180, 90); rgn.AddArc(pf.Width - 40, 0, 40, 40, 270, 90);
                rgn.AddArc(pf.Width - 40, pf.Height - 40, 40, 40, 0, 90); rgn.AddArc(0, pf.Height - 40, 40, 40, 90, 90);
                rgn.CloseFigure(); pf.Region = new Region(rgn);
            }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();

            var popup = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White, BackColor = Color.White };
            popup.ShadowDecoration.Enabled = true; popup.ShadowDecoration.Depth = 32; popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            pf.Controls.Add(popup);

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(db, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                string ht = isEdit ? " ⁄œÌ· √„— «·Ê«—œ" : "≈÷«›… √„— Ê«—œ ÃœÌœ";
                string sub2 = isEdit ? "⁄œ¯· «·»Ì«‰«  À„ «÷€ÿ  ÕœÌÀ" : "«Œ — «·⁄„Ì· Ê√÷› «·„‰ Ã« ";
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(ht, tf); g.DrawString(ht, tf, tb, pnlHead.Width - tsz.Width - 60, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString(sub2, sf3); g.DrawString(sub2, sf3, sb3, pnlHead.Width - ssz.Width - 60, 54); }
            };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255);
            btnX.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX);
            pnlHead.Layout += (s, e) => btnX.Location = new Point(18, 18);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) =>
            {
                var g = pe6.Graphics;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) g.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 1, footer.Width, 2);
            };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Padding = new Padding(20, 10, 20, 8) };

            Panel MkLblPanel(string txt)
            {
                var p = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent };
                p.Paint += (s, pe) =>
                {
                    pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold))
                    using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e")))
                    { var sz2 = pe.Graphics.MeasureString(txt, f2); pe.Graphics.DrawString(txt, f2, b2, p.Width - sz2.Width - 2, p.Height - sz2.Height - 1); }
                };
                return p;
            }

            Panel MkCboField(string placeholder, out ComboBox cboOut)
            {
                var cbo = new ComboBox { Height = 42, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
                cbo.DrawItem += (s2, de) =>
                {
                    if (de.Index < 0) return;
                    bool hot = (de.State & DrawItemState.Selected) != 0;
                    de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#EEF2FF") : Color.White), de.Bounds);
                    string txt2 = cbo.GetItemText(cbo.Items[de.Index]);
                    using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular))
                    using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#1e3a6e") : ColorTranslator.FromHtml("#111827")))
                        de.Graphics.DrawString(txt2, f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                };
                var ov = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
                ov.Paint += (s2, pe2) =>
                {
                    var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.FillPath(new SolidBrush(Color.White), path2);
                    using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.DrawPath(pen2, path2);
                    int ax = 18, ay = ov.Height / 2;
                    using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); }
                    string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : placeholder;
                    bool isPh = cbo.SelectedIndex < 0 || selTxt == placeholder;
                    using (var f2 = new Font("Cairo", 11F)) using (var b2 = new SolidBrush(isPh ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A")))
                        g.DrawString(selTxt, f2, b2, new RectangleF(36, 0, ov.Width - 52, ov.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                };
                cbo.SetBounds(0, 0, 400, 42); ov.Controls.Add(cbo);
                ov.Resize += (s2, e2) => cbo.SetBounds(0, 0, ov.Width, 42);
                cbo.SelectedIndexChanged += (s2, e2) => ov.Invalidate();
                cboOut = cbo; return ov;
            }

            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            Panel Div() => new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };

            void SafeSetCbo(ComboBox cbo, int targetId)
            {
                try { cbo.SelectedValue = targetId; } catch { }
                if (cbo.SelectedValue == null || Convert.ToInt32(cbo.SelectedValue) != targetId)
                    for (int i = 0; i < cbo.Items.Count; i++)
                    {
                        var item = cbo.Items[i];
                        int itemId = (item is CustomerDto cd) ? cd.Id : (item is ProductDto pd) ? pd.Id : -1;
                        if (itemId == targetId) { try { cbo.SelectedIndex = i; } catch { } break; }
                    }
            }

            var productService = new ProductService();
            var customerService = new CustomerService();
            var prodList = new List<ProductDto>();
            var custList = new List<CustomerDto>();
            try { prodList = productService.GetAll()?.ToList() ?? new List<ProductDto>(); } catch { }
            try { custList = customerService.GetAll()?.ToList() ?? new List<CustomerDto>(); } catch { }

            var errCust = new Label { Dock = DockStyle.Top, Height = 0, Font = new Font("Cairo", 9F), ForeColor = ColorTranslator.FromHtml("#EF4444"), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Visible = false };
            var cboCustPanel = MkCboField("«Œ — ⁄„Ì·", out var cboCust);
            var custWithDefault = new List<CustomerDto>(custList);
            custWithDefault.Insert(0, new CustomerDto { Id = 0, Name = "«Œ — ⁄„Ì·" });
            cboCust.DisplayMember = "Name"; cboCust.ValueMember = "Id";
            cboCust.DataSource = null; cboCust.DataSource = custWithDefault;

            var itemsContainer = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent, Height = 0 };
            const int ITEM_H = 170;
            var itemRows = new List<(ComboBox cboProd, Panel cboProdPanel, Guna2TextBox txtQty, Guna2TextBox txtPrice)>();
            void RefreshItemsHeight() => itemsContainer.Height = itemRows.Count * ITEM_H;

            void AddProductRow(InboundOrderItemDto fill = null)
            {
                var card = new Panel { Dock = DockStyle.Top, Height = ITEM_H, BackColor = Color.White, Padding = new Padding(8, 6, 8, 6) };
                card.Paint += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(ColorTranslator.FromHtml("#DBEAFE"), 1.2f))
                    using (var path = RoundPath(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 8))
                        pe.Graphics.DrawPath(pen, path);
                };

                var pList2 = new List<ProductDto>(prodList);
                pList2.Insert(0, new ProductDto { Id = 0, Name = "«Œ — „‰ Ã" });
                var cboProdPanel = MkCboField("«Œ — „‰ Ã", out var cboProd);
                cboProd.Font = new Font("Cairo", 10.5F);
                cboProd.DisplayMember = "Name"; cboProd.ValueMember = "Id";
                cboProd.DataSource = null; cboProd.DataSource = new List<ProductDto>(pList2);

                var txtQty = new Guna2TextBox { Width = 120, Height = 36, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#D1D5DB"), BorderThickness = 1, Font = new Font("Cairo", 10F), PlaceholderText = "«·ﬂ„Ì… (ﬁÿ⁄…)", TextAlign = HorizontalAlignment.Center, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                txtQty.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");

                var txtPrice = new Guna2TextBox { Width = 120, Height = 36, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#D1D5DB"), BorderThickness = 1, Font = new Font("Cairo", 10F), PlaceholderText = "”⁄— «·‘—«¡", TextAlign = HorizontalAlignment.Center, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                txtPrice.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");

                var btnDel = new Panel { Width = 30, Height = 30, BackColor = Color.Transparent, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Left };
                btnDel.Paint += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var ph = RoundPath(new Rectangle(1, 1, 28, 28), 7)) pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), ph);
                    using (var pen2 = new Pen(ColorTranslator.FromHtml("#EF4444"), 2f)) pe.Graphics.DrawLine(pen2, 8, 15, 22, 15);
                };
                btnDel.Click += (s, e) =>
                {
                    itemRows.RemoveAll(r => r.cboProd == cboProd);
                    itemsContainer.Controls.Remove(card);
                    card.Dispose();
                    RefreshItemsHeight();
                };

                var lblsRow = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent };
                lblsRow.Paint += (s, pe) =>
                {
                    int rW = lblsRow.Width;
                    using (var f2 = new Font("Cairo", 8F, FontStyle.Bold)) using (var br = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                    { pe.Graphics.DrawString("«·ﬂ„Ì… (ﬁÿ⁄…)", f2, br, new RectangleF(rW - 124, 1, 120, 16), new StringFormat { Alignment = StringAlignment.Center }); pe.Graphics.DrawString("”⁄— «·‘—«¡", f2, br, new RectangleF(rW - 252, 1, 120, 16), new StringFormat { Alignment = StringAlignment.Center }); }
                };

                var fieldsRow = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
                fieldsRow.Resize += (s, e) =>
                {
                    int rW = fieldsRow.Width, gap = 8;
                    txtQty.SetBounds(rW - txtQty.Width, 0, txtQty.Width, 36);
                    txtPrice.SetBounds(txtQty.Left - gap - txtPrice.Width, 0, txtPrice.Width, 36);
                    btnDel.SetBounds(4, 3, 30, 30);
                };
                fieldsRow.Controls.AddRange(new Control[] { txtQty, txtPrice, btnDel });

                if (fill != null)
                {
                    txtQty.Text = fill.Quantity.ToString();
                    txtPrice.Text = fill.PurchasePrice.ToString("N2");
                    if (fill.ProductId > 0) SafeSetCbo(cboProd, fill.ProductId);
                }

                card.Controls.Add(Sp(4));
                card.Controls.Add(fieldsRow);
                card.Controls.Add(lblsRow);
                card.Controls.Add(Sp(4));
                card.Controls.Add(cboProdPanel);
                card.Controls.Add(MkLblPanel("«·„‰ Ã *"));

                itemRows.Add((cboProd, cboProdPanel, txtQty, txtPrice));
                itemsContainer.Controls.Add(card);
                RefreshItemsHeight();
                body.ScrollControlIntoView(card);
            }

            var btnAddRow = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnAddRow.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, btnAddRow.Width - 1, btnAddRow.Height - 1);
                using (var ph = RoundPath(rc2, 8)) { pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), ph); pe.Graphics.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), ph); }
                using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
                    pe.Graphics.DrawString("+ ≈÷«›… „‰ Ã ¬Œ—", f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            btnAddRow.Click += (s, e) => AddProductRow();

            body.SuspendLayout();
            body.Controls.Add(Sp(6)); body.Controls.Add(btnAddRow); body.Controls.Add(Sp(6));
            body.Controls.Add(itemsContainer); body.Controls.Add(Sp(4)); body.Controls.Add(MkLblPanel("«·„‰ Ã«  *"));
            body.Controls.Add(Sp(8)); body.Controls.Add(Div()); body.Controls.Add(Sp(8));
            body.Controls.Add(errCust); body.Controls.Add(cboCustPanel); body.Controls.Add(MkLblPanel("«·⁄„Ì· *"));
            body.Controls.Add(Sp(8));
            body.ResumeLayout(true);

            if (isEdit)
            {
                if (editDto.CustomerId > 0) SafeSetCbo(cboCust, editDto.CustomerId);
                if (editDto.Items != null && editDto.Items.Count > 0)
                    foreach (var item in editDto.Items) AddProductRow(item);
                else
                    AddProductRow(new InboundOrderItemDto { ProductId = editDto.ProductId, Quantity = editDto.Quantity, PurchasePrice = editDto.PurchasePrice });
            }
            else { AddProductRow(); }

            var btnSave = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = isEdit ? " ÕœÌÀ «·√„—" : "Õ›Ÿ «·√„—",
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#4E73DF"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 13F, FontStyle.Bold),
                Animated = true
            };
            btnSave.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9");
            btnSave.ShadowDecoration.Enabled = true; btnSave.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223); btnSave.ShadowDecoration.Depth = 10;

            btnSave.Click += async (s6, e6) =>
            {
                errCust.Visible = false; errCust.Height = 0;
                int custId = 0;
                try { custId = Convert.ToInt32(cboCust.SelectedValue); } catch { }
                if (custId == 0) { errCust.Text = "ï «Œ — ⁄„Ì·"; errCust.Visible = true; errCust.Height = 18; return; }

                if (itemRows.Count == 0)
                { MessageBox.Show("√÷› „‰ Ã« ⁄·Ï «·√ﬁ·.", " Õﬁﬁ „‰ «·»Ì«‰« ", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                var itemsToSave = new List<InboundOrderItemDto>();
                bool allOk = true;

                foreach (var r in itemRows)
                {
                    int pid = 0;
                    try { pid = Convert.ToInt32(r.cboProd.SelectedValue); } catch { }
                    if (pid == 0) { allOk = false; continue; }

                    if (!int.TryParse(r.txtQty.Text, out int qty) || qty <= 0)
                    { allOk = false; r.txtQty.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }

                    string pt = (r.txtPrice.Text ?? "").Trim().Replace(",", ".");
                    if (!decimal.TryParse(pt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price2) || price2 <= 0)
                    { allOk = false; r.txtPrice.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }

                    itemsToSave.Add(new InboundOrderItemDto { ProductId = pid, Quantity = qty, PurchasePrice = price2, BoxesPerCarton = 1 });
                }

                if (!allOk || itemsToSave.Count == 0)
                { MessageBox.Show("Ì—ÃÏ «· Õﬁﬁ „‰ »Ì«‰«  «·„‰ Ã«  («·„‰ Ã° «·ﬂ„Ì…° «·”⁄—).", " Õﬁﬁ „‰ «·»Ì«‰« ", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                // ?? Õ›Ÿ „»«‘— »œÊ‰  Õﬁﬁ „‰ «·Œ“‰… ??
                btnSave.Enabled = false; btnSave.Text = isEdit ? "Ã«—Ú «· ÕœÌÀ..." : "Ã«—Ú «·Õ›Ÿ...";
                try
                {
                    var dto = new InboundOrderDto { CustomerId = custId };
                    dto.Items.AddRange(itemsToSave);
                    if (!isEdit)
                        await Task.Run(() => _inboundService.SaveInboundOrder(dto));
                    else
                    { dto.Id = editDto.Id; await Task.Run(() => _inboundService.UpdateInboundOrder(dto)); }

                    _ = LoadInboundsAsync();
                    pf.Close();
                }
                catch (Exception ex)
                {
                    var inner = ex;
                    while (inner.InnerException != null) inner = inner.InnerException;
                    MessageBox.Show("Œÿ√: " + ex.Message + "\n\nInner: " + inner.Message + "\n\nStack: " + ex.StackTrace, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { btnSave.Enabled = true; btnSave.Text = isEdit ? " ÕœÌÀ «·√„—" : "Õ›Ÿ «·√„—"; }
            };

            footer.Controls.Add(btnSave);
            popup.Controls.Add(body);
            popup.Controls.Add(footer);
            popup.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  ERROR + DELETE DIALOGS
        // ??????????????????????????????????????????????????????
        private void ShowErrorDialog(string entityName, string rawError)
        {
            bool isFk = rawError != null && (rawError.Contains("FOREIGN KEY") || rawError.Contains("REFERENCE") || rawError.Contains("constraint"));
            string title = isFk ? "·« Ì„ﬂ‰ Õ–› «·√„—" : "ÕœÀ Œÿ√";
            string line1 = isFk ? $"·« Ì„ﬂ‰ Õ–› «·√„— \"{entityName}\"" : "ÕœÀ Œÿ√ €Ì— „ Êﬁ⁄";
            string line2 = isFk ? "·√‰ ·Â »‰Êœ „— »ÿ… ›Ì «·‰Ÿ«„." : rawError;
            string line3 = isFk ? "ÌÃ» Õ–› «·»‰Êœ «·„— »ÿ… √Ê·« À„ «·„Õ«Ê·…." : "";
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(440, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.Transparent };
            header.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#B45309"), ColorTranslator.FromHtml("#D97706"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, header.Width - tsz.Width - 70, 12); }
                using (var sf3 = new Font("Cairo", 9.5F)) using (var sb3 = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.DrawString(" ⁄–¯—  ‰›Ì– ⁄„·Ì… «·Õ–›", sf3, sb3, header.Width - 222, 50);
            };
            var errBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 16, 22, 0) };
            var msgPanel = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            msgPanel.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, msgPanel.Width - 1, msgPanel.Height - 1);
                using (var path = RoundPath(rc2, 12)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFFBEB")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FDE68A"), 1.5f), path); }
                using (var f1 = new Font("Cairo", 11.5F, FontStyle.Bold)) using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#92400E"))) g.DrawString(line1, f1, b1, new RectangleF(12, 10, msgPanel.Width - 24, 28), new StringFormat { Alignment = StringAlignment.Far });
                using (var f2 = new Font("Cairo", 10.5F)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#78350F"))) g.DrawString(line2, f2, b2, new RectangleF(12, 40, msgPanel.Width - 24, 26), new StringFormat { Alignment = StringAlignment.Far });
                if (!string.IsNullOrEmpty(line3)) using (var f3 = new Font("Cairo", 9.5F, FontStyle.Italic)) using (var b3 = new SolidBrush(ColorTranslator.FromHtml("#A16207"))) g.DrawString(line3, f3, b3, new RectangleF(12, 66, msgPanel.Width - 24, 24), new StringFormat { Alignment = StringAlignment.Far });
            };
            errBody.Controls.Add(msgPanel);
            var errFooter = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White, Padding = new Padding(24, 10, 24, 14) };
            var btnOk = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ”‰«° ›Â„ ", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#D97706"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Animated = true };
            btnOk.HoverState.FillColor = ColorTranslator.FromHtml("#B45309");
            btnOk.Click += (s, e) => dlg.Close();
            errFooter.Controls.Add(btnOk); root.Controls.Add(errBody); root.Controls.Add(errFooter); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Return) dlg.Close(); }; dlg.ShowDialog(this);
        }

        private void InboundForm_Load(object sender, EventArgs e) { }

        private bool ShowDeleteConfirm(string label)
        {
            bool result = false;
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(420, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.Transparent };
            header.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#C0392B"), ColorTranslator.FromHtml("#E74C3C"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var tf = new Font("Cairo", 18F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString("Õ–› «·√„—", tf); g.DrawString("Õ–› «·√„—", tf, tb, header.Width - tsz.Width - 68, 14); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.DrawString("Â–« «·≈Ã—«¡ ·« Ì„ﬂ‰ «· —«Ã⁄ ⁄‰Â", sf3, sb3, header.Width - 238, 52);
            };
            var delBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 18, 28, 0) };
            var nameBox = new Panel { Dock = DockStyle.Top, Height = 50 };
            nameBox.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, nameBox.Width - 1, nameBox.Height - 1);
                using (var path = RoundPath(rc2, 10)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1.5f), path); }
                using (var f = new Font("Cairo", 12F, FontStyle.Bold)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#B91C1C")))
                    g.DrawString($"  {label}  ", f, b, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            delBody.Controls.Add(nameBox);
            delBody.Controls.Add(new Label { Text = "Â· √‰  „ √ﬂœ „‰ Õ–› Â–« «·√„— «·Ê«—œø", Font = new Font("Cairo", 12F), ForeColor = ColorTranslator.FromHtml("#374151"), Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            var delFooter = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(24, 12, 24, 20) };
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var btnCancel = new Guna2Button { Dock = DockStyle.Fill, Text = "≈·€«¡", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#F1F5F9"), ForeColor = ColorTranslator.FromHtml("#64748B"), BorderColor = ColorTranslator.FromHtml("#E2E8F0"), BorderThickness = 1, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(0, 0, 6, 0) };
            var btnConfirm = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ–›", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#EF4444"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Animated = true };
            btnCancel.HoverState.FillColor = ColorTranslator.FromHtml("#E2E8F0"); btnCancel.Click += (s, e) => dlg.Close();
            btnConfirm.HoverState.FillColor = ColorTranslator.FromHtml("#DC2626");
            btnConfirm.Click += (s, e) => { result = true; dlg.Close(); };
            tbl.Controls.Add(btnCancel, 0, 0); tbl.Controls.Add(btnConfirm, 1, 0);
            delFooter.Controls.Add(tbl); root.Controls.Add(delBody); root.Controls.Add(delFooter); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) dlg.Close(); };
            dlg.ShowDialog(this); return result;
        }
    }
}