using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public partial class VehiclesForm : Form
    {
        private readonly VehicleService _vehicleService;
        private readonly ProductService _productService;
        private readonly PdfReportService _pdfService;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Guna2DataGridView dgvDispatch;
        private Label lblCountBadge;
        private Panel _paginationBar;
        private Guna2TextBox txtSearch;
        private System.Threading.Timer _searchTimer;

        private List<DispatchOrderDto> _allDispatches = new List<DispatchOrderDto>();
        private List<VehicleDto> _vehicles = new List<VehicleDto>();
        private int _currentPage = 1;
        private const int PageSize = 7;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allDispatches.Count / (double)PageSize));

        private static readonly PropertyInfo _dbProp = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent) { foreach (Control ctrl in parent.Controls) { try { _dbProp?.SetValue(ctrl, true); } catch { } if (ctrl.Controls.Count > 0) EnableDbAll(ctrl); } }
        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        public VehiclesForm()
        {
            InitializeComponent();
            HideOldControls();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            UpdateStyles();
            _vehicleService = new VehicleService();
            _productService = new ProductService();
            _pdfService = new PdfReportService();
            BuildNewUI();
            Shown += (s, e) => BeginInvoke(new Action(async () => { LoadVehicles(); await LoadDispatchesAsync(); }));
        }

        private void HideOldControls()
        {
            try { foreach (Control c in Controls.Cast<Control>().ToList()) if (!(c is TableLayoutPanel)) c.Visible = false; }
            catch { }
        }

        private void BuildNewUI()
        {
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5"); Padding = new Padding(0);
            SuspendLayout();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Padding = new Padding(0) };
            root.SuspendLayout();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildTableCard(), 0, 1);
            root.ResumeLayout(false);
            EnableDbAll(root);
            Controls.Add(root); root.BringToFront();
            ResumeLayout(true);
        }

        private Panel BuildPageHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 50, BackColor = ColorTranslator.FromHtml("#EEF0F5") };
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
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var sf2 = new Font("Cairo", 10.5F))
                {
                    string title = "«·”Ì«—«  Ê«· Ê“Ì⁄"; string sub = "≈œ«—… «·”Ì«—«  Ê√Ê«„— «·’—›";
                    var szT = g.MeasureString(title, tf); var szS = g.MeasureString(sub, sf2);
                    float gap = 4f; float block = szT.Height + gap + szS.Height; float startY = (banner.Height - block) / 2f;
                    using (var tb = new SolidBrush(Color.White)) g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);
                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.DrawString(sub, sf2, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);
                    float lineY = startY + szT.Height + gap + szS.Height + 4f;
                    using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6"))) g.FillRectangle(b1, banner.Width - 44, lineY, 40, 3);
                    using (var b2 = new SolidBrush(Color.FromArgb(140, 100, 181, 246))) g.FillRectangle(b2, banner.Width - 62, lineY, 14, 3);
                }
            };

            Guna2Button MakeBtn(string text, int width, int left)
            {
                var b = new Guna2Button
                {
                    Text = text,
                    FillColor = Color.FromArgb(30, 255, 255, 255),
                    ForeColor = Color.White,
                    BorderRadius = 12,
                    BorderThickness = 1,
                    BorderColor = Color.FromArgb(60, 255, 255, 255),
                    Font = new Font("Cairo", 10F, FontStyle.Bold),
                    Size = new Size(width, 38),
                    Anchor = AnchorStyles.Left | AnchorStyles.Top,
                    Location = new Point(left, 6)
                };
                b.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
                return b;
            }

            int bx = 12, btnGap = 8;
            var btnDispatch = MakeBtn("+ √„— ’—›", 122, bx); bx += 122 + btnGap;
            var btnAddVeh = MakeBtn("+ ”Ì«—… ÃœÌœ…", 132, bx); bx += 132 + btnGap;
            var btnVehicles = MakeBtn("«·”Ì«—« ", 110, bx); bx += 110 + btnGap;
            var btnReturn = MakeBtn("„— Ã⁄", 100, bx); bx += 100 + btnGap;
            var btnReport = MakeBtn(" Õ„Ì· «· ﬁ—Ì—", 130, bx);

            btnReturn.FillColor = Color.FromArgb(35, 255, 255, 255);
            btnReturn.BorderColor = Color.FromArgb(80, 255, 180, 100);

            btnDispatch.Click += (s, e) => ShowDispatchPopup();
            btnAddVeh.Click += (s, e) => ShowVehiclePopup();
            btnVehicles.Click += (s, e) => ShowVehiclesListPopup();
            btnReturn.Click += (s, e) => ShowReturnPopup();
            btnReport.Click += (s, e) => ShowLoadReportPopup();

            banner.Controls.AddRange(new Control[] { btnDispatch, btnAddVeh, btnVehicles, btnReturn, btnReport });
            pnl.Controls.Add(banner); return pnl;
        }

        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblCountBadge = new Label { Text = "0 √„—", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblCountBadge.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblCountBadge);

            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("”Ã· √Ê«„— «·’—›", tf); g.DrawString("”Ã· √Ê«„— «·’—›", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
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

            txtSearch = new Guna2TextBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                PlaceholderText = "«»ÕÀ »«”„ «·”Ì«—… √Ê «·„‰œÊ»...",
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
                _searchTimer?.Dispose(); _currentPage = 1;
                _searchTimer = new System.Threading.Timer(async _ =>
                { try { await (Task)Invoke(new Func<Task>(LoadDispatchesAsync)); } catch { } }, null, 350, System.Threading.Timeout.Infinite);
            };
            var searchWrapper = new Panel { Width = 220, Height = 32, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Top, Location = new Point(12, (58 - 32) / 2) };
            searchWrapper.Controls.Add(txtSearch);
            topBar.Controls.Add(searchWrapper);

            var searchSep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSep.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSep.Width, 3), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, searchSep.Width, 3);
            };

            dgvDispatch = new Guna2DataGridView
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
            dgvDispatch.RowTemplate.Height = 70;
            dgvDispatch.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvDispatch.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvDispatch.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgvDispatch.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgvDispatch.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvDispatch.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvDispatch.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");
            dgvDispatch.DefaultCellStyle.BackColor = Color.White;
            dgvDispatch.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvDispatch.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvDispatch.DefaultCellStyle.Font = new Font("Cairo", 12F, FontStyle.Bold);
            dgvDispatch.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgvDispatch.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgvDispatch, true); } catch { }

            BuildColumns();
            dgvDispatch.CellPainting += Dgv_CellPainting;
            dgvDispatch.CellClick += Dgv_CellClick;
            dgvDispatch.Resize += (s, e) => FitColumns();

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) =>
            { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0); };

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrapper.Controls.Add(dgvDispatch);

            container.Controls.Add(dgvWrapper);
            container.Controls.Add(_paginationBar);
            container.Controls.Add(searchSep);
            container.Controls.Add(topBar);
            card.Controls.Add(container); return card;
        }

        private void BuildColumns()
        {
            dgvDispatch.Columns.Clear();
            void Add(string n, string h, string p, int w) =>
                dgvDispatch.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            Add("DId", "—ﬁ„", "Id", 55);
            Add("DVehicle", "«·”Ì«—…", "VehicleName", 160);
            Add("DRep", "«·„‰œÊ»", "RepName", 130);
            Add("DDate", "«· «—ÌŒ", "CreatedAt", 160);
            Add("DStatus", "«·Õ«·…", "", 90);
            Add("DDetails", "«· ›«’Ì·", "", 76);
            Add("DActions", "Õ–›", "", 66);
        }

        private void FitColumns()
        {
            if (dgvDispatch == null || dgvDispatch.Columns.Count == 0) return;
            int w = dgvDispatch.ClientSize.Width; if (w <= 0) return;
            int wId = 55, wDet = 72, wDel = 62, wSt = 88, wDt = 158;
            int rest = w - wId - wDet - wDel - wSt - wDt;
            int wV = Math.Max(100, (int)(rest * 0.50));
            int wR = Math.Max(90, rest - wV);
            dgvDispatch.Columns["DId"].Width = wId;
            dgvDispatch.Columns["DVehicle"].Width = wV;
            dgvDispatch.Columns["DRep"].Width = wR;
            dgvDispatch.Columns["DDate"].Width = wDt;
            dgvDispatch.Columns["DStatus"].Width = wSt;
            dgvDispatch.Columns["DDetails"].Width = wDet;
            dgvDispatch.Columns["DActions"].Width = wDel;
        }

        private void LoadVehicles()
        {
            try { _vehicles = _vehicleService.GetAllVehicles()?.Where(v => v.IsActive).ToList() ?? new List<VehicleDto>(); }
            catch { _vehicles = new List<VehicleDto>(); }
        }

        private async Task LoadDispatchesAsync()
        {
            try
            {
                var q = txtSearch?.Text?.Trim() ?? "";
                var all = await Task.Run(() =>
                {
                    var list = (_vehicleService.GetAllDispatchOrders() ?? Enumerable.Empty<DispatchOrderDto>()).ToList();
                    if (!string.IsNullOrEmpty(q))
                        list = list.Where(x =>
                            (x.VehicleName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (x.RepName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                            x.Id.ToString().Contains(q)).ToList();
                    return list;
                });
                _allDispatches = all;
                _currentPage = Math.Min(_currentPage, TotalPages);
                var page = _allDispatches.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
                dgvDispatch.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                if (lblCountBadge != null) lblCountBadge.Text = $"{_allDispatches.Count} √„—";
                RenderPagination();
            }
            catch (Exception ex) { ShowErrorToast("›‘·  Õ„Ì· «·√Ê«„—: " + GetInner(ex)); }
        }

        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            _paginationBar.Controls.Add(new Label
            {
                Text = $"⁄—÷ {Math.Min(_allDispatches.Count, (_currentPage - 1) * PageSize + 1)}-{Math.Min(_allDispatches.Count, _currentPage * PageSize)} „‰ {_allDispatches.Count}",
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Width = 180,
                Height = 56,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            });
            var pnlPages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false };
            pnlPages.Controls.Add(MakeNavBtn("õ", _currentPage < TotalPages, () => { _currentPage++; _ = LoadDispatchesAsync(); }));
            for (int i = TotalPages; i >= 1; i--)
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
                if (!cur) btn.Click += (s2, e2) => { _currentPage = pg; _ = LoadDispatchesAsync(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MakeNavBtn("ã", _currentPage > 1, () => { _currentPage--; _ = LoadDispatchesAsync(); }));
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

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dto = dgvDispatch.Rows[e.RowIndex].DataBoundItem as DispatchOrderDto;
            if (dto == null) return;
            string col = dgvDispatch.Columns[e.ColumnIndex].Name;
            if (col == "DDetails") { ShowDetailsPopup(dto); return; }
            if (col == "DActions")
            {
                if (ShowDeleteConfirm($"√„— #{dto.Id}"))
                    try { _vehicleService.DeleteDispatchOrder(dto.Id); _ = LoadDispatchesAsync(); ShowSuccessToast(" „ «·Õ–› Ê⁄ﬂ” «·„Œ“‰"); }
                    catch (Exception ex) { ShowErrorDialog($"√„— #{dto.Id}", ex.Message); }
            }
        }

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
                bool sel = dgvDispatch.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                string col = dgvDispatch.Columns[e.ColumnIndex].Name;
                switch (col)
                {
                    case "DVehicle": PaintVehicleCell(e, bg); break;
                    case "DDate": PaintDateCell(e, bg); break;
                    case "DStatus": PaintStatusCell(e, bg); break;
                    case "DDetails": PaintEyeCell(e, bg); break;
                    case "DActions": PaintDeleteCell(e, bg); break;
                    default:
                        e.Handled = true;
                        e.Graphics.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
                        e.PaintContent(e.CellBounds);
                        break;
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

        private void PaintVehicleCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var dto = dgvDispatch.Rows[e.RowIndex].DataBoundItem as DispatchOrderDto; string name = dto?.VehicleName ?? "";
            using (var nf = new Font("Cairo", 12F, FontStyle.Bold)) using (var nb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, nf, nb, e.CellBounds, _sfCenter);
        }

        private void PaintDateCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            string dateText = ""; DateTime dt2 = DateTime.MinValue;
            if (e.Value is DateTime dv) dt2 = dv;
            else if (e.Value != null) DateTime.TryParse(e.Value.ToString(), out dt2);
            if (dt2 != DateTime.MinValue)
            {
                if (dt2.Kind == DateTimeKind.Utc) dt2 = dt2.ToLocalTime();
                else if (dt2.Kind == DateTimeKind.Unspecified) dt2 = DateTime.SpecifyKind(dt2, DateTimeKind.Local);
                dateText = dt2.ToString("yyyy/MM/dd  HH:mm");
            }
            using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                g.DrawString(dateText, f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintStatusCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            int pw = 64, ph = 26, px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2, py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
            using (var path = RoundPath(new Rectangle(px, py, pw, ph), ph / 2))
            { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#ECFDF5")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#A7F3D0"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#065F46")))
                g.DrawString("‰‘ÿ", f, tb, new RectangleF(px, py, pw, ph), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintEyeCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            int bw = 38, bh = 30, bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var rc2 = new Rectangle(bx, by, bw, bh);
            using (var path = RoundPath(rc2, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#93C5FD"), 1.2f), path); }
            int cx = bx + bw / 2, cy = by + bh / 2;
            using (var pen = new Pen(ColorTranslator.FromHtml("#2563EB"), 1.8f))
            { var ep = new GraphicsPath(); ep.AddArc(cx - 9, cy - 5, 18, 10, 180, -180); ep.AddArc(cx - 9, cy - 5, 18, 10, 0, -180); g.DrawPath(pen, ep); ep.Dispose(); }
            g.FillEllipse(new SolidBrush(ColorTranslator.FromHtml("#2563EB")), cx - 3, cy - 3, 6, 6);
            g.FillEllipse(Brushes.White, cx + 1, cy - 2, 2, 2);
        }

        private void PaintDeleteCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            int bw = 32, bh = 28, bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            using (var path = RoundPath(new Rectangle(bx, by, bw, bh), 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
            { int dx = bx + bw / 2, dy = by + bh / 2; g.DrawLine(pen, dx - 5, dy - 3, dx + 5, dy - 3); g.DrawLine(pen, dx - 1, dy - 5, dx + 1, dy - 5); g.DrawRectangle(pen, dx - 4, dy - 2, 8, 6); g.DrawLine(pen, dx - 1, dy, dx - 1, dy + 3); g.DrawLine(pen, dx + 1, dy, dx + 1, dy + 3); }
        }

        // ???????????????????????????????????????????????????????
        //  DISPATCH POPUP  ó «·ﬂ„Ì… »«·ﬁÿ⁄… ›ﬁÿ
        // ???????????????????????????????????????????????????????
        private void ShowDispatchPopup()
        {
            if (_vehicles.Count == 0) { ShowErrorToast("√÷› ”Ì«—… √Ê·«"); return; }
            var overlay = ShowOverlay();

            var sc = Screen.FromControl(this).WorkingArea;
            int pfH = Math.Min(sc.Height - 60, 720);
            var pf = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(500, pfH),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.No,
                RightToLeftLayout = false
            };
            pf.Location = new Point(sc.Left + (sc.Width - pf.Width) / 2, sc.Top + (sc.Height - pf.Height) / 2);
            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pf.Width - 36, 0, 36, 36, 270, 90);
                rgn.AddArc(pf.Width - 36, pf.Height - 36, 36, 36, 0, 90); rgn.AddArc(0, pf.Height - 36, 36, 36, 90, 90);
                rgn.CloseFigure(); pf.Region = new Region(rgn);
            }
            pf.FormClosed += (s, e) => CloseOverlay(overlay);
            overlay.Click += (s, e) => pf.Close();

            // Header
            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc2);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White))
                { var tsz = g.MeasureString("≈÷«›… √„— ’—› ÃœÌœ", tf); g.DrawString("≈÷«›… √„— ’—› ÃœÌœ", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString("«Œ — «·”Ì«—… Ê√÷› «·„‰ Ã« ", sf3); g.DrawString("«Œ — «·”Ì«—… Ê√÷› «·„‰ Ã« ", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); }
            };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX); pnlHead.Layout += (s, e) => btnX.Location = new Point(18, 18);

            // Footer
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal))
                    pe6.Graphics.FillRectangle(br, 0, 1, footer.Width, 2);
            };

            // Body
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Padding = new Padding(20, 10, 20, 8) };

            // Helpers
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
                    var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.FillPath(brs, path2);
                    using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.DrawPath(pen2, path2);
                    int ax = 18, ay = ov.Height / 2;
                    using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); }
                    string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : placeholder;
                    bool isPlaceholder = cbo.SelectedIndex < 0 || selTxt == placeholder;
                    using (var f2 = new Font("Cairo", 11F))
                    using (var b2 = new SolidBrush(isPlaceholder ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A")))
                        g.DrawString(selTxt, f2, b2, new RectangleF(36, 0, ov.Width - 52, ov.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                };
                cbo.SetBounds(0, 0, 400, 42); ov.Controls.Add(cbo);
                ov.Resize += (s2, e2) => cbo.SetBounds(0, 0, ov.Width, 42);
                cbo.SelectedIndexChanged += (s2, e2) => ov.Invalidate();
                cboOut = cbo; return ov;
            }

            Guna2TextBox MkTxt2(string ph, bool ro = false)
            {
                var t = new Guna2TextBox { Height = 40, Dock = DockStyle.Top, BorderRadius = 8, FillColor = ro ? ColorTranslator.FromHtml("#F3F4F6") : Color.White, BorderColor = ColorTranslator.FromHtml("#C7D2FE"), BorderThickness = 1, Font = new Font("Cairo", 10.5F), PlaceholderText = ph, PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"), ForeColor = ColorTranslator.FromHtml("#0F172A"), TextAlign = HorizontalAlignment.Right, ReadOnly = ro };
                if (!ro) { t.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF"); t.FocusedState.FillColor = ColorTranslator.FromHtml("#F5F8FF"); }
                return t;
            }

            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            Panel Div() => new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };

            // «·”Ì«—…
            var errV = MakeErrLbl();
            var cboVehPanel = MkCboField("«Œ — ”Ì«—…", out var cboVeh);
            var vList = new List<VehicleDto>(_vehicles); vList.Insert(0, new VehicleDto { Id = 0, Name = "«Œ — ”Ì«—…" });
            cboVeh.DisplayMember = "Name"; cboVeh.ValueMember = "Id"; cboVeh.DataSource = null; cboVeh.DataSource = vList;

            // «·„‰œÊ»
            var fRep = MkTxt2("Ìı„·√  ·ﬁ«∆Ì«", ro: true);
            cboVeh.SelectedIndexChanged += (s, e) =>
            {
                var v = cboVeh.SelectedItem as VehicleDto;
                fRep.Text = v?.Id > 0 ? v.RepName : "";
                cboVehPanel.Invalidate();
            };

            // «·„‰ Ã« 
            List<ProductDto> products = new List<ProductDto>();
            try { products = _productService.GetAll()?.ToList() ?? new List<ProductDto>(); } catch { }

            var itemsContainer = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent, Height = 0 };
            // ﬂ· row: (cboProd, cboProdPanel, txtQty, txtPrice)
            var itemRows = new List<(ComboBox cbo, Panel cboPanel, Guna2TextBox qty, Guna2TextBox price)>();
            const int ITEM_H = 160;

            void RefreshItemsHeight() => itemsContainer.Height = itemRows.Count * ITEM_H;

            void AddProductRow()
            {
                var card = new Panel { Dock = DockStyle.Top, Height = ITEM_H, BackColor = Color.White, Padding = new Padding(8, 6, 8, 6) };
                card.Paint += (s, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(ColorTranslator.FromHtml("#DBEAFE"), 1.2f))
                    using (var path = RoundPath(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 8))
                        pe.Graphics.DrawPath(pen, path);
                };

                var pList2 = new List<ProductDto>(products); pList2.Insert(0, new ProductDto { Id = 0, Name = "«Œ — „‰ Ã" });
                var cboProdPanel = MkCboField("«Œ — „‰ Ã", out var cboProd);
                cboProd.Font = new Font("Cairo", 10.5F);
                cboProd.DisplayMember = "Name"; cboProd.ValueMember = "Id";
                cboProd.DataSource = null; cboProd.DataSource = new List<ProductDto>(pList2);

                // Õﬁ· «·ﬂ„Ì… »«·ﬁÿ⁄…
                var txtQty = new Guna2TextBox { Width = 130, Height = 38, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#D1D5DB"), BorderThickness = 1, Font = new Font("Cairo", 10F), PlaceholderText = "«·ﬂ„Ì… (ﬁÿ⁄…)", TextAlign = HorizontalAlignment.Center, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                txtQty.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");

                // Õﬁ· ”⁄— «·»Ì⁄
                var txtPrc = new Guna2TextBox { Width = 120, Height = 38, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#D1D5DB"), BorderThickness = 1, Font = new Font("Cairo", 10F), PlaceholderText = "”⁄— «·»Ì⁄", TextAlign = HorizontalAlignment.Center, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                txtPrc.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");

                var btnDel = new Panel { Width = 30, Height = 30, BackColor = Color.Transparent, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Left };
                btnDel.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var ph = RoundPath(new Rectangle(1, 1, 28, 28), 7)) pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), ph); using (var pen2 = new Pen(ColorTranslator.FromHtml("#EF4444"), 2f)) pe.Graphics.DrawLine(pen2, 8, 15, 22, 15); };
                btnDel.Click += (s, e) =>
                {
                    itemRows.RemoveAll(r => r.cbo == cboProd);
                    itemsContainer.Controls.Remove(card); card.Dispose();
                    RefreshItemsHeight();
                };

                //  ”„Ì«  «·ÕﬁÊ·
                var lblsRow = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent };
                lblsRow.Paint += (s, pe) =>
                {
                    int rW = lblsRow.Width;
                    using (var f2 = new Font("Cairo", 8F, FontStyle.Bold)) using (var br = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                    {
                        pe.Graphics.DrawString("«·ﬂ„Ì… (ﬁÿ⁄…)", f2, br, new RectangleF(rW - 132, 1, 130, 16), new StringFormat { Alignment = StringAlignment.Center });
                        pe.Graphics.DrawString("”⁄— «·»Ì⁄", f2, br, new RectangleF(rW - 258, 1, 120, 16), new StringFormat { Alignment = StringAlignment.Center });
                    }
                };

                var fieldsRow = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent };
                fieldsRow.Resize += (s, e) =>
                {
                    int rW = fieldsRow.Width, g2 = 8, ty = 0;
                    txtQty.SetBounds(rW - txtQty.Width, ty, txtQty.Width, 38);
                    txtPrc.SetBounds(txtQty.Left - g2 - txtPrc.Width, ty, txtPrc.Width, 38);
                    btnDel.SetBounds(4, ty + 4, 30, 30);
                };
                fieldsRow.Controls.Add(txtQty);
                fieldsRow.Controls.Add(txtPrc);
                fieldsRow.Controls.Add(btnDel);

                // ‘—Ìÿ «·—’Ìœ «·„ «Õ
                var pnlBalance = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = ColorTranslator.FromHtml("#EFF6FF") };
                pnlBalance.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f)) using (var ph = RoundPath(new Rectangle(0, 0, pnlBalance.Width - 1, pnlBalance.Height - 1), 6)) pe.Graphics.DrawPath(pen, ph); };
                var lblBalance = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Cairo", 8.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#94A3B8"), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Text = "«Œ — „‰ Ã« ·⁄—÷ «·—’Ìœ" };
                pnlBalance.Controls.Add(lblBalance);

                cboProd.SelectedIndexChanged += (s, e) =>
                {
                    try
                    {
                        int pid2 = Convert.ToInt32(cboProd.SelectedValue ?? 0);
                        var sp2 = products.FirstOrDefault(p => p.Id == pid2);
                        if (sp2?.Id > 0)
                        {
                            txtPrc.Text = sp2.SalePrice > 0 ? sp2.SalePrice.ToString("N2", Inv) : "";
                            int balance = 0;
                            try { balance = _vehicleService.GetProductWarehouseBalance(pid2); } catch { }
                            if (balance <= 0)
                            {
                                lblBalance.Text = "? —’Ìœ «·„Œ“‰ ’›— ·Â–« «·„‰ Ã";
                                lblBalance.ForeColor = ColorTranslator.FromHtml("#DC2626");
                                pnlBalance.BackColor = ColorTranslator.FromHtml("#FEF2F2");
                                txtQty.BorderColor = ColorTranslator.FromHtml("#EF4444");
                                txtQty.Enabled = false;
                            }
                            else
                            {
                                lblBalance.Text = $"«·—’Ìœ «·„ «Õ: {balance} ﬁÿ⁄…";
                                lblBalance.ForeColor = ColorTranslator.FromHtml("#059669");
                                pnlBalance.BackColor = ColorTranslator.FromHtml("#ECFDF5");
                                txtQty.BorderColor = ColorTranslator.FromHtml("#D1D5DB");
                                txtQty.Enabled = true;
                            }
                            pnlBalance.Invalidate();
                        }
                        else
                        {
                            txtPrc.Text = "";
                            txtQty.Enabled = true;
                            txtQty.BorderColor = ColorTranslator.FromHtml("#D1D5DB");
                            lblBalance.Text = "«Œ — „‰ Ã« ·⁄—÷ «·—’Ìœ";
                            lblBalance.ForeColor = ColorTranslator.FromHtml("#94A3B8");
                            pnlBalance.BackColor = ColorTranslator.FromHtml("#EFF6FF");
                            pnlBalance.Invalidate();
                        }
                    }
                    catch { }
                    cboProdPanel.Invalidate();
                };

                card.Controls.Add(pnlBalance);
                card.Controls.Add(Sp(4));
                card.Controls.Add(fieldsRow);
                card.Controls.Add(lblsRow);
                card.Controls.Add(Sp(3));
                card.Controls.Add(cboProdPanel);
                card.Controls.Add(MkLblPanel("«·„‰ Ã *"));

                itemRows.Add((cboProd, cboProdPanel, txtQty, txtPrc));
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
            body.Controls.Add(Sp(6));
            body.Controls.Add(btnAddRow);
            body.Controls.Add(Sp(6));
            body.Controls.Add(itemsContainer);
            body.Controls.Add(Sp(4));
            body.Controls.Add(MkLblPanel("«·„‰ Ã«  *"));
            body.Controls.Add(Sp(8));
            body.Controls.Add(Div());
            body.Controls.Add(Sp(8));
            var fRepWrap = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
            fRep.Dock = DockStyle.None; fRep.SetBounds(0, 0, fRepWrap.Width, 40);
            fRepWrap.Resize += (s, e2) => fRep.SetBounds(0, 0, fRepWrap.Width, 40);
            fRepWrap.Controls.Add(fRep);
            body.Controls.Add(fRepWrap);
            body.Controls.Add(MkLblPanel("«·„‰œÊ»"));
            body.Controls.Add(Sp(8));
            body.Controls.Add(errV);
            body.Controls.Add(cboVehPanel);
            body.Controls.Add(MkLblPanel("«·”Ì«—… *"));
            body.Controls.Add(Sp(8));
            body.ResumeLayout(true);

            AddProductRow();

            // Save
            var btnSave = MakeSaveBtn("Õ›Ÿ √„— «·’—›");
            btnSave.Click += async (s, e) =>
            {
                errV.Visible = false; errV.Height = 0;
                int vId = 0; try { vId = Convert.ToInt32(cboVeh.SelectedValue); } catch { }
                if (vId == 0) { errV.Text = "ï «Œ — ”Ì«—…"; errV.Visible = true; errV.Height = 18; return; }
                if (itemRows.Count == 0) { ShowErrorToast("√÷› „‰ Ã« ⁄·Ï «·√ﬁ·"); return; }

                var items2 = new List<DispatchOrderItemDto>(); bool valid2 = true;
                foreach (var r in itemRows)
                {
                    int pid2 = 0; try { pid2 = Convert.ToInt32(r.cbo.SelectedValue); } catch { }
                    if (pid2 == 0) { valid2 = false; continue; }

                    if (!int.TryParse(r.qty.Text, out int total2) || total2 <= 0)
                    { valid2 = false; r.qty.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }

                    //  Õﬁﬁ „‰ —’Ìœ «·„Œ“‰
                    int stockBalance = 0;
                    try { stockBalance = _vehicleService.GetProductWarehouseBalance(pid2); } catch { }
                    if (stockBalance <= 0)
                    {
                        valid2 = false;
                        string prodName = products.FirstOrDefault(p => p.Id == pid2)?.Name ?? $"#{pid2}";
                        ShowErrorToast($"? «·„‰ Ã '{prodName}' —’ÌœÂ ’›— ›Ì «·„Œ“‰");
                        r.qty.BorderColor = ColorTranslator.FromHtml("#EF4444");
                        continue;
                    }
                    if (total2 > stockBalance)
                    {
                        valid2 = false;
                        string prodName = products.FirstOrDefault(p => p.Id == pid2)?.Name ?? $"#{pid2}";
                        ShowErrorToast($"? '{prodName}': «·ﬂ„Ì… ({total2} ﬁÿ⁄…) > «·—’Ìœ ({stockBalance} ﬁÿ⁄…)");
                        r.qty.BorderColor = ColorTranslator.FromHtml("#EF4444");
                        continue;
                    }

                    decimal pr2 = 0; string prTxt = (r.price.Text ?? "").Trim().Replace(",", ".");
                    if (!decimal.TryParse(prTxt, System.Globalization.NumberStyles.Any, Inv, out pr2)) decimal.TryParse(r.price.Text.Trim(), out pr2);
                    items2.Add(new DispatchOrderItemDto { ProductId = pid2, ProductName = products.FirstOrDefault(p => p.Id == pid2)?.Name ?? "", Quantity = total2, SalePrice = pr2, BoxesPerCarton = 1 });
                }
                if (!valid2) { ShowErrorToast(" Õﬁﬁ „‰ »Ì«‰«  «·„‰ Ã« "); return; }

                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                try
                {
                    var dto2 = new DispatchOrderDto { VehicleId = vId, Items = items2 };
                    await Task.Run(() => _vehicleService.SaveDispatchOrder(dto2));
                    _ = LoadDispatchesAsync(); pf.Close(); ShowSuccessToast(" „ Õ›Ÿ √„— «·’—› ÊŒ’„ «·„Œ“‰ ?");
                }
                catch (Exception ex) { ShowErrorToast("›‘·: " + GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = "Õ›Ÿ √„— «·’—›"; }
            };

            footer.Controls.Add(btnSave);
            pf.Controls.Add(body); pf.Controls.Add(footer); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ???????????????????????????????????????????????????????
        //  RETURN POPUP  ó «·ﬂ„Ì… »«·ﬁÿ⁄… ›ﬁÿ
        // ???????????????????????????????????????????????????????
        private void ShowReturnPopup()
        {
            if (_vehicles.Count == 0) { ShowErrorToast("√÷› ”Ì«—… √Ê·«"); return; }
            var overlay = ShowOverlay();

            var sc = Screen.FromControl(this).WorkingArea;
            int pfH = Math.Min(sc.Height - 60, 760);
            var pf = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(500, pfH),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.No,
                RightToLeftLayout = false
            };
            pf.Location = new Point(sc.Left + (sc.Width - pf.Width) / 2, sc.Top + (sc.Height - pf.Height) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pf.Width - 36, 0, 36, 36, 270, 90); rgn.AddArc(pf.Width - 36, pf.Height - 36, 36, 36, 0, 90); rgn.AddArc(0, pf.Height - 36, 36, 36, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => CloseOverlay(overlay);
            overlay.Click += (s, e) => pf.Close();

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#7c2d12"), ColorTranslator.FromHtml("#ea580c"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White))
                { var tsz = g.MeasureString("„— Ã⁄ ”Ì«—…", tf); g.DrawString("„— Ã⁄ ”Ì«—…", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString("«Œ — «·”Ì«—… Ê√„— «·’—› Ê√÷› «·„— Ã⁄« ", sf3); g.DrawString("«Œ — «·”Ì«—… Ê√„— «·’—› Ê√÷› «·„— Ã⁄« ", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); }
            };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX); pnlHead.Layout += (s, e) => btnX.Location = new Point(18, 18);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#ea580c"), ColorTranslator.FromHtml("#FED7AA"), LinearGradientMode.Horizontal)) pe6.Graphics.FillRectangle(br, 0, 1, footer.Width, 2);
            };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Padding = new Padding(20, 10, 20, 8) };

            Panel MkLblPanel(string txt) { var p = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent }; p.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) { var sz2 = pe.Graphics.MeasureString(txt, f2); pe.Graphics.DrawString(txt, f2, b2, p.Width - sz2.Width - 2, p.Height - sz2.Height - 1); } }; return p; }

            Panel MkCboField(string placeholder, out ComboBox cboOut)
            {
                var cbo = new ComboBox { Height = 42, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
                cbo.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#FFF7ED") : Color.White), de.Bounds); string txt2 = cbo.GetItemText(cbo.Items[de.Index]); using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#7c2d12") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(txt2, f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };
                var ov = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
                ov.Paint += (s2, pe2) => { var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.FillPath(brs, path2); using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.DrawPath(pen2, path2); int ax = 18, ay = ov.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); } string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : placeholder; bool isPh = cbo.SelectedIndex < 0 || selTxt == placeholder; using (var f2 = new Font("Cairo", 11F)) using (var b2 = new SolidBrush(isPh ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A"))) g.DrawString(selTxt, f2, b2, new RectangleF(36, 0, ov.Width - 52, ov.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter }); };
                cbo.SetBounds(0, 0, 400, 42); ov.Controls.Add(cbo); ov.Resize += (s2, e2) => cbo.SetBounds(0, 0, ov.Width, 42); cbo.SelectedIndexChanged += (s2, e2) => ov.Invalidate();
                cboOut = cbo; return ov;
            }

            Guna2TextBox MkTxt2(string ph, bool ro = false) { var t = new Guna2TextBox { Height = 40, Dock = DockStyle.Top, BorderRadius = 8, FillColor = ro ? ColorTranslator.FromHtml("#F3F4F6") : Color.White, BorderColor = ColorTranslator.FromHtml("#C7D2FE"), BorderThickness = 1, Font = new Font("Cairo", 10.5F), PlaceholderText = ph, PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"), ForeColor = ColorTranslator.FromHtml("#0F172A"), TextAlign = HorizontalAlignment.Right, ReadOnly = ro }; if (!ro) { t.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF"); t.FocusedState.FillColor = ColorTranslator.FromHtml("#F5F8FF"); } return t; }

            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            Panel Div() => new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };

            var errV = MakeErrLbl();
            var cboVehPanel = MkCboField("«Œ — ”Ì«—…", out var cboVeh);
            var vList = new List<VehicleDto>(_vehicles); vList.Insert(0, new VehicleDto { Id = 0, Name = "«Œ — ”Ì«—…" });
            cboVeh.DisplayMember = "Name"; cboVeh.ValueMember = "Id"; cboVeh.DataSource = null; cboVeh.DataSource = vList;

            var fRep = MkTxt2("Ìı„·√  ·ﬁ«∆Ì«", ro: true);
            var errD = MakeErrLbl();
            var cboDspPanel = MkCboField("«Œ — √„— «·’—›", out var cboDsp);
            List<DispatchOrderDto> vehDispatches = new List<DispatchOrderDto>();

            cboVeh.SelectedIndexChanged += (s, e) =>
            {
                var v = cboVeh.SelectedItem as VehicleDto;
                fRep.Text = v?.Id > 0 ? v.RepName : "";
                cboVehPanel.Invalidate();
                vehDispatches.Clear(); cboDsp.DataSource = null; cboDsp.Items.Clear();
                if (v?.Id > 0)
                {
                    try { vehDispatches = _vehicleService.GetDispatchOrdersByVehicle(v.Id) ?? new List<DispatchOrderDto>(); } catch { vehDispatches = new List<DispatchOrderDto>(); }
                    var dList = vehDispatches.Select(d => new DispatchDisplayItem { Id = d.Id, DisplayText = $"√„— #{d.Id}  ó  {ToLocalStr(d.CreatedAt)}  ({d.Items.Count} „‰ Ã)" }).ToList();
                    dList.Insert(0, new DispatchDisplayItem { Id = 0, DisplayText = "«Œ — √„— «·’—›" });
                    cboDsp.DisplayMember = "DisplayText"; cboDsp.ValueMember = "Id"; cboDsp.DataSource = dList;
                    cboDspPanel.Invalidate();
                }
            };

            var fNotes = MkTxt2("„·«ÕŸ« ...");

            List<ProductDto> products = new List<ProductDto>();
            try { products = _productService.GetAll()?.ToList() ?? new List<ProductDto>(); } catch { }

            var itemsContainer = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent, Height = 0 };
            // ﬂ· row: (cboProd, cboPanel, txtQty)
            var itemRows = new List<(ComboBox cbo, Panel cboPanel, Guna2TextBox qty)>();
            const int ITEM_H2 = 140;

            void RefreshItemsHeight() => itemsContainer.Height = itemRows.Count * ITEM_H2;

            void AddReturnRow()
            {
                var card = new Panel { Dock = DockStyle.Top, Height = ITEM_H2, BackColor = Color.White, Padding = new Padding(8, 6, 8, 6) };
                card.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(ColorTranslator.FromHtml("#FED7AA"), 1.2f)) using (var path = RoundPath(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 8)) pe.Graphics.DrawPath(pen, path); };

                var pList2 = new List<ProductDto>(products); pList2.Insert(0, new ProductDto { Id = 0, Name = "«Œ — „‰ Ã" });
                var cboProdPanel = MkCboField("«Œ — „‰ Ã", out var cboProd);
                cboProd.Font = new Font("Cairo", 10.5F);
                cboProd.DisplayMember = "Name"; cboProd.ValueMember = "Id";
                cboProd.DataSource = null; cboProd.DataSource = new List<ProductDto>(pList2);

                // Õﬁ· «·ﬂ„Ì… »«·ﬁÿ⁄… ›ﬁÿ
                var txtQty = new Guna2TextBox { Width = 160, Height = 38, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#D1D5DB"), BorderThickness = 1, Font = new Font("Cairo", 10F), PlaceholderText = "«·ﬂ„Ì… «·„— Ã⁄… (ﬁÿ⁄…)", TextAlign = HorizontalAlignment.Center, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                txtQty.FocusedState.BorderColor = ColorTranslator.FromHtml("#ea580c");

                var btnDel = new Panel { Width = 30, Height = 30, BackColor = Color.Transparent, Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Left };
                btnDel.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var ph = RoundPath(new Rectangle(1, 1, 28, 28), 7)) pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), ph); using (var pen2 = new Pen(ColorTranslator.FromHtml("#EF4444"), 2f)) pe.Graphics.DrawLine(pen2, 8, 15, 22, 15); };
                btnDel.Click += (s, e) => { itemRows.RemoveAll(r => r.cbo == cboProd); itemsContainer.Controls.Remove(card); card.Dispose(); RefreshItemsHeight(); };

                var lblsRow = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = Color.Transparent };
                lblsRow.Paint += (s, pe) => { int rW = lblsRow.Width; using (var f2 = new Font("Cairo", 8F, FontStyle.Bold)) using (var br = new SolidBrush(ColorTranslator.FromHtml("#64748B"))) { pe.Graphics.DrawString("«·ﬂ„Ì… «·„— Ã⁄… (ﬁÿ⁄…)", f2, br, new RectangleF(rW - 162, 1, 160, 16), new StringFormat { Alignment = StringAlignment.Center }); } };

                var fieldsRow = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent };
                fieldsRow.Resize += (s, e) => { int rW = fieldsRow.Width, ty = 0; txtQty.SetBounds(rW - txtQty.Width, ty, txtQty.Width, 38); btnDel.SetBounds(4, ty + 4, 30, 30); };
                fieldsRow.Controls.Add(txtQty); fieldsRow.Controls.Add(btnDel);

                cboProd.SelectedIndexChanged += (s, e) => { cboProdPanel.Invalidate(); };

                card.Controls.Add(Sp(4)); card.Controls.Add(fieldsRow);
                card.Controls.Add(lblsRow); card.Controls.Add(Sp(3)); card.Controls.Add(cboProdPanel); card.Controls.Add(MkLblPanel("«·„‰ Ã *"));

                itemRows.Add((cboProd, cboProdPanel, txtQty));
                itemsContainer.Controls.Add(card);
                RefreshItemsHeight();
                body.ScrollControlIntoView(card);
            }

            var btnAddRow = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnAddRow.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, btnAddRow.Width - 1, btnAddRow.Height - 1); using (var ph = RoundPath(rc2, 8)) { pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFF7ED")), ph); pe.Graphics.DrawPath(new Pen(ColorTranslator.FromHtml("#FED7AA"), 1f), ph); } using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#ea580c"))) pe.Graphics.DrawString("+ ≈÷«›… „‰ Ã „— Ã⁄ ¬Œ—", f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            btnAddRow.Click += (s, e) => AddReturnRow();

            body.SuspendLayout();
            body.Controls.Add(Sp(6)); body.Controls.Add(btnAddRow); body.Controls.Add(Sp(6));
            body.Controls.Add(itemsContainer); body.Controls.Add(Sp(4));
            body.Controls.Add(MkLblPanel("«·„‰ Ã«  «·„— Ã⁄… *")); body.Controls.Add(Sp(6));
            body.Controls.Add(fNotes); body.Controls.Add(MkLblPanel("„·«ÕŸ«  («Œ Ì«—Ì)")); body.Controls.Add(Sp(8));
            body.Controls.Add(Div()); body.Controls.Add(Sp(8));
            var fRepWrap = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
            fRep.Dock = DockStyle.None; fRep.SetBounds(0, 0, 400, 40);
            fRepWrap.Resize += (s, e2) => fRep.SetBounds(0, 0, fRepWrap.Width, 40); fRepWrap.Controls.Add(fRep);
            body.Controls.Add(fRepWrap); body.Controls.Add(MkLblPanel("«·„‰œÊ»")); body.Controls.Add(Sp(8));
            body.Controls.Add(errD); body.Controls.Add(cboDspPanel); body.Controls.Add(MkLblPanel("√„— «·’—› «·„— Ã⁄ „‰Â *")); body.Controls.Add(Sp(8));
            body.Controls.Add(errV); body.Controls.Add(cboVehPanel); body.Controls.Add(MkLblPanel("«·”Ì«—… *")); body.Controls.Add(Sp(8));
            body.ResumeLayout(true);

            AddReturnRow();

            var btnSave = MakeSaveBtn("Õ›Ÿ «·„— Ã⁄");
            btnSave.FillColor = ColorTranslator.FromHtml("#ea580c"); btnSave.HoverState.FillColor = ColorTranslator.FromHtml("#C2410C"); btnSave.ShadowDecoration.Color = Color.FromArgb(45, 234, 88, 12);
            btnSave.Click += async (s, e) =>
            {
                errV.Visible = errD.Visible = false; errV.Height = errD.Height = 0;
                int vId = 0; try { vId = Convert.ToInt32(cboVeh.SelectedValue); } catch { }
                if (vId == 0) { errV.Text = "ï «Œ — ”Ì«—…"; errV.Visible = true; errV.Height = 18; return; }
                int dId = 0; try { dId = Convert.ToInt32(cboDsp.SelectedValue); } catch { }
                if (dId == 0) { errD.Text = "ï «Œ — √„— «·’—› «·„— Ã⁄ „‰Â"; errD.Visible = true; errD.Height = 18; return; }
                if (itemRows.Count == 0) { ShowErrorToast("√÷› „‰ Ã« ⁄·Ï «·√ﬁ·"); return; }
                DispatchOrderDto dispatchOrder = null;
                try { dispatchOrder = vehDispatches.FirstOrDefault(d => d.Id == dId) ?? _vehicleService.GetAllDispatchOrders()?.FirstOrDefault(d => d.Id == dId); } catch { }
                var items2 = new List<ReturnOrderItemDto>(); bool valid2 = true;
                foreach (var r in itemRows)
                {
                    int pid2 = 0; try { pid2 = Convert.ToInt32(r.cbo.SelectedValue); } catch { }
                    if (pid2 == 0) { valid2 = false; continue; }

                    if (!int.TryParse(r.qty.Text, out int retQty) || retQty <= 0)
                    { valid2 = false; r.qty.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }

                    if (dispatchOrder != null)
                    {
                        var dispItem = dispatchOrder.Items.FirstOrDefault(i => i.ProductId == pid2);
                        int maxQty = dispItem?.Quantity ?? 0;
                        if (retQty > maxQty)
                        { r.qty.BorderColor = ColorTranslator.FromHtml("#EF4444"); ShowErrorToast($"«·ﬂ„Ì… «·„— Ã⁄… ({retQty} ﬁÿ⁄…) > «·ﬂ„Ì… ›Ì «·√„— ({maxQty} ﬁÿ⁄…)"); return; }
                    }
                    string pName = products.FirstOrDefault(p => p.Id == pid2)?.Name ?? "";
                    items2.Add(new ReturnOrderItemDto { ProductId = pid2, ProductName = pName, Quantity = retQty });
                }
                if (!valid2) { ShowErrorToast(" Õﬁﬁ „‰ »Ì«‰«  «·„‰ Ã« "); return; }
                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                try
                {
                    var dto2 = new ReturnOrderDto { VehicleId = vId, DispatchOrderId = dId, Notes = fNotes.Text.Trim(), Items = items2 };
                    await Task.Run(() => _vehicleService.SaveReturnOrder(dto2));
                    _ = LoadDispatchesAsync(); pf.Close(); ShowSuccessToast(" „ Õ›Ÿ «·„— Ã⁄ Ê≈⁄«œ… «·ﬂ„Ì… ··„Œ“‰ ?");
                }
                catch (Exception ex) { ShowErrorToast("›‘·: " + GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = "Õ›Ÿ «·„— Ã⁄"; }
            };
            footer.Controls.Add(btnSave);
            pf.Controls.Add(body); pf.Controls.Add(footer); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        private void ShowVehiclesListPopup()
        {
            LoadVehicles();
            var overlay = ShowOverlay();
            int popH = Math.Max(420, Math.Min(700, 200 + _vehicles.Count * 52));
            var pf = CreatePopup(640, popH, "«·”Ì«—«  «·„”Ã·…", "⁄—÷ Ê ⁄œÌ· ÊÕ–› «·”Ì«—« ");
            pf.FormClosed += (s, e) => CloseOverlay(overlay);
            var body = pf.Tag as Panel; if (body == null) return;
            var footer = FindFooter(pf);

            var dgv = new Guna2DataGridView { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, AutoGenerateColumns = false, ColumnHeadersHeight = 40, EnableHeadersVisualStyles = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AllowUserToAddRows = false, ReadOnly = true, CellBorderStyle = DataGridViewCellBorderStyle.None, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, AllowUserToResizeRows = false, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
            dgv.RowTemplate.Height = 56;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1e3a6e"); dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 10F, FontStyle.Bold); dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1e3a6e");
            dgv.DefaultCellStyle.Font = new Font("Cairo", 11.5F, FontStyle.Bold); dgv.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#0F172A"); dgv.DefaultCellStyle.BackColor = Color.White; dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF"); dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgv, true); } catch { }
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "VN", HeaderText = "«”„ «·”Ì«—…", DataPropertyName = "Name", Width = 220, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "VR", HeaderText = "«·„‰œÊ»", DataPropertyName = "RepName", Width = 200, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "VA", HeaderText = "«·≈Ã—«¡« ", Width = 140 });

            dgv.CellPainting += (s, e2) =>
            {
                try
                {
                    if (e2.RowIndex == -1)
                    {
                        e2.Handled = true; var g = e2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var br = new LinearGradientBrush(e2.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical)) g.FillRectangle(br, e2.CellBounds);
                        using (var font = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) g.DrawString(e2.Value?.ToString() ?? "", font, tb, e2.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f)) { g.DrawLine(sp, e2.CellBounds.Left, e2.CellBounds.Top + 6, e2.CellBounds.Left, e2.CellBounds.Bottom - 6); g.DrawLine(sp, e2.CellBounds.Right - 1, e2.CellBounds.Top + 6, e2.CellBounds.Right - 1, e2.CellBounds.Bottom - 6); }
                        return;
                    }
                    if (e2.RowIndex < 0) return;
                    bool sel2 = dgv.Rows[e2.RowIndex].Selected;
                    Color bg2 = sel2 ? ColorTranslator.FromHtml("#EEF2FF") : (e2.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                    var g2 = e2.Graphics; g2.SmoothingMode = SmoothingMode.AntiAlias; g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    string colName2 = dgv.Columns[e2.ColumnIndex].Name;
                    if (colName2 == "VN")
                    {
                        e2.Handled = true; g2.FillRectangle(bg2 == Color.White ? _brWhite : new SolidBrush(bg2), e2.CellBounds);
                        var vDto2 = dgv.Rows[e2.RowIndex].DataBoundItem as VehicleDto;
                        using (var nf2 = new Font("Cairo", 12F, FontStyle.Bold)) using (var nb2 = new SolidBrush(ColorTranslator.FromHtml("#0F172A"))) g2.DrawString(vDto2?.Name ?? "", nf2, nb2, e2.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else if (colName2 == "VR")
                    {
                        e2.Handled = true; g2.FillRectangle(bg2 == Color.White ? _brWhite : new SolidBrush(bg2), e2.CellBounds);
                        var vDto2 = dgv.Rows[e2.RowIndex].DataBoundItem as VehicleDto; string repName = vDto2?.RepName ?? "";
                        if (!string.IsNullOrEmpty(repName))
                        {
                            int bw2 = repName.Length * 9 + 20; bw2 = Math.Min(bw2, e2.CellBounds.Width - 20); int bh2 = 28;
                            int bx2 = e2.CellBounds.Left + (e2.CellBounds.Width - bw2) / 2, by2 = e2.CellBounds.Top + (e2.CellBounds.Height - bh2) / 2;
                            using (var path2 = RoundPath(new Rectangle(bx2, by2, bw2, bh2), bh2 / 2)) { g2.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EEF2FF")), path2); g2.DrawPath(new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1f), path2); }
                            using (var f2 = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) g2.DrawString(repName, f2, tb2, new RectangleF(bx2, by2, bw2, bh2), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        }
                    }
                    else if (colName2 == "VA")
                    {
                        e2.Handled = true; g2.FillRectangle(bg2 == Color.White ? _brWhite : new SolidBrush(bg2), e2.CellBounds);
                        int bH2 = 30, bY2 = e2.CellBounds.Top + (e2.CellBounds.Height - bH2) / 2, totalW = 70 + 8 + 32, startX2 = e2.CellBounds.Left + (e2.CellBounds.Width - totalW) / 2;
                        var er2 = new Rectangle(startX2, bY2, 70, bH2);
                        using (var ph = RoundPath(er2, 8)) { g2.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), ph); g2.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), ph); }
                        using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb2 = new SolidBrush(ColorTranslator.FromHtml("#2563EB"))) g2.DrawString(" ⁄œÌ·", f2, tb2, er2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        var dr2 = new Rectangle(startX2 + 70 + 8, bY2, 32, bH2);
                        using (var ph2 = RoundPath(dr2, 8)) { g2.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), ph2); g2.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), ph2); }
                        using (var pen2 = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f)) { int dx2 = dr2.Left + dr2.Width / 2, dy2 = dr2.Top + dr2.Height / 2; g2.DrawLine(pen2, dx2 - 5, dy2 - 3, dx2 + 5, dy2 - 3); g2.DrawLine(pen2, dx2 - 2, dy2 - 5, dx2 + 2, dy2 - 5); g2.DrawRectangle(pen2, dx2 - 4, dy2 - 2, 8, 6); g2.DrawLine(pen2, dx2 - 1, dy2, dx2 - 1, dy2 + 3); g2.DrawLine(pen2, dx2 + 1, dy2, dx2 + 1, dy2 + 3); }
                    }
                    else { e2.Handled = true; g2.FillRectangle(bg2 == Color.White ? _brWhite : new SolidBrush(bg2), e2.CellBounds); e2.PaintContent(e2.CellBounds); }
                    using (var wPen = new Pen(Color.White, 2f)) { g2.DrawLine(wPen, e2.CellBounds.Left, e2.CellBounds.Top, e2.CellBounds.Left, e2.CellBounds.Bottom); g2.DrawLine(wPen, e2.CellBounds.Right - 1, e2.CellBounds.Top, e2.CellBounds.Right - 1, e2.CellBounds.Bottom); g2.DrawLine(wPen, e2.CellBounds.Left, e2.CellBounds.Bottom - 1, e2.CellBounds.Right, e2.CellBounds.Bottom - 1); }
                    using (var divPen = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f)) g2.DrawLine(divPen, e2.CellBounds.Left, e2.CellBounds.Bottom - 1, e2.CellBounds.Right, e2.CellBounds.Bottom - 1);
                }
                catch { }
            };

            dgv.CellClick += (s, e2) =>
            {
                if (e2.RowIndex < 0 || dgv.Columns[e2.ColumnIndex].Name != "VA") return;
                var vDto = dgv.Rows[e2.RowIndex].DataBoundItem as VehicleDto; if (vDto == null) return;
                var cell = dgv.GetCellDisplayRectangle(e2.ColumnIndex, e2.RowIndex, false); var mouse = dgv.PointToClient(Cursor.Position);
                int bH2 = 30, bY2 = cell.Top + (cell.Height - bH2) / 2, totalW = 70 + 8 + 32, startX2 = cell.Left + (cell.Width - totalW) / 2;
                if (new Rectangle(startX2, bY2, 70, bH2).Contains(mouse))
                    ShowVehiclePopup(vDto, () => { LoadVehicles(); dgv.DataSource = new BindingSource { DataSource = new List<VehicleDto>(_vehicles) }; });
                else if (new Rectangle(startX2 + 70 + 8, bY2, 32, bH2).Contains(mouse))
                { if (ShowDeleteConfirm(vDto.Name)) { try { _vehicleService.DeleteVehicle(vDto.Id); LoadVehicles(); dgv.DataSource = new BindingSource { DataSource = new List<VehicleDto>(_vehicles) }; ShowSuccessToast(" „ Õ–› «·”Ì«—…"); } catch (Exception ex) { ShowErrorToast(GetInner(ex)); } } }
            };

            dgv.DataSource = new BindingSource { DataSource = new List<VehicleDto>(_vehicles) };
            body.Controls.Add(dgv);
            var btnC = MakeSaveBtn("≈€·«ﬁ"); btnC.FillColor = ColorTranslator.FromHtml("#64748B"); btnC.HoverState.FillColor = ColorTranslator.FromHtml("#475569");
            btnC.Click += (s, e) => pf.Close();
            footer?.Controls.Add(btnC);
            pf.ShowDialog(this);
        }

        private void ShowVehiclePopup(VehicleDto edit = null, Action onSaved = null)
        {
            bool isEdit = edit != null;
            var overlay = ShowOverlay();
            var pf = CreatePopup(420, 380, isEdit ? " ⁄œÌ· ”Ì«—…" : "≈÷«›… ”Ì«—… ÃœÌœ…", isEdit ? "⁄œ¯· «·»Ì«‰«  À„ «÷€ÿ  ÕœÌÀ" : "√œŒ· »Ì«‰«  «·”Ì«—… Ê«·„‰œÊ»");
            pf.FormClosed += (s, e) => CloseOverlay(overlay);
            var body = pf.Tag as Panel; if (body == null) return;
            var footer = FindFooter(pf);
            var errName = MakeErrLbl(); var fName = MakeTxt("«”„ «·”Ì«—…");
            var errRep = MakeErrLbl(); var fRep = MakeTxt("«”„ «·„‰œÊ»");
            if (isEdit) { fName.Text = edit.Name; fRep.Text = edit.RepName; }
            body.Controls.Add(MakeSp()); body.Controls.Add(errRep); body.Controls.Add(fRep); body.Controls.Add(MakeLbl("«”„ «·„‰œÊ» *"));
            body.Controls.Add(MakeSp()); body.Controls.Add(errName); body.Controls.Add(fName); body.Controls.Add(MakeLbl("«”„ «·”Ì«—… *"));
            var btnSave = MakeSaveBtn(isEdit ? " ÕœÌÀ" : "Õ›Ÿ");
            btnSave.Click += async (s, e) =>
            {
                errName.Visible = errRep.Visible = false; errName.Height = errRep.Height = 0; bool ok = true;
                if (string.IsNullOrWhiteSpace(fName.Text)) { errName.Text = "ï «”„ «·”Ì«—… „ÿ·Ê»"; errName.Visible = true; errName.Height = 18; ok = false; }
                if (string.IsNullOrWhiteSpace(fRep.Text)) { errRep.Text = "ï «”„ «·„‰œÊ» „ÿ·Ê»"; errRep.Visible = true; errRep.Height = 18; ok = false; }
                if (!ok) return;
                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                try { var dto2 = new VehicleDto { Id = edit?.Id ?? 0, Name = fName.Text.Trim(), RepName = fRep.Text.Trim() }; await Task.Run(() => _vehicleService.SaveVehicle(dto2)); LoadVehicles(); onSaved?.Invoke(); pf.Close(); ShowSuccessToast(isEdit ? " „  ÕœÌÀ «·”Ì«—… ?" : " „  ≈÷«›… «·”Ì«—… ?"); }
                catch (Exception ex) { ShowErrorToast("›‘·: " + GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = isEdit ? " ÕœÌÀ" : "Õ›Ÿ"; }
            };
            footer?.Controls.Add(btnSave);
            pf.ShowDialog(this);
        }

        private void ShowLoadReportPopup()
        {
            LoadVehicles(); if (_vehicles.Count == 0) { ShowErrorToast("·«  ÊÃœ ”Ì«—«  „”Ã·…"); return; }
            var overlay = ShowOverlay();
            var sc = Screen.FromControl(this).WorkingArea;
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(480, 460), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.No, RightToLeftLayout = false };
            pf.Location = new Point(sc.Left + (sc.Width - pf.Width) / 2, sc.Top + (sc.Height - pf.Height) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 36, 36, 180, 90); rgn.AddArc(pf.Width - 36, 0, 36, 36, 270, 90); rgn.AddArc(pf.Width - 36, pf.Height - 36, 36, 36, 0, 90); rgn.AddArc(0, pf.Height - 36, 36, 36, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => CloseOverlay(overlay); overlay.Click += (s, e) => pf.Close();
            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) => { var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height); using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#064e3b"), ColorTranslator.FromHtml("#065F46"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2); using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2); using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180); using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White)) { var tsz = g.MeasureString(" Õ„Ì· «· ﬁ—Ì— «·‘Â—Ì", tf); g.DrawString(" Õ„Ì· «· ﬁ—Ì— «·‘Â—Ì", tf, tb2, pnlHead.Width - tsz.Width - 50, 16); } using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString("«Œ — «·”Ì«—… Ê«·‘Â— Ê«·”‰…", sf3); g.DrawString("«Œ — «·”Ì«—… Ê«·‘Â— Ê«·”‰…", sf3, sb3, pnlHead.Width - ssz.Width - 50, 52); } };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX.Click += (s, e) => pf.Close();
            pnlHead.Controls.Add(btnX); pnlHead.Layout += (s, e) => btnX.Location = new Point(18, 18);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s6, pe6) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe6.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#065F46"), ColorTranslator.FromHtml("#D1FAE5"), LinearGradientMode.Horizontal)) pe6.Graphics.FillRectangle(br, 0, 1, footer.Width, 2); };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = false, Padding = new Padding(20, 12, 20, 8) };
            Panel MkLblPanel(string txt) { var p = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent }; p.Paint += (s, pe) => { pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var f2 = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) { var sz2 = pe.Graphics.MeasureString(txt, f2); pe.Graphics.DrawString(txt, f2, b2, p.Width - sz2.Width - 2, p.Height - sz2.Height - 1); } }; return p; }
            Panel MkCboField(string placeholder, out ComboBox cboOut)
            {
                var cbo = new ComboBox { Height = 42, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
                cbo.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#ECFDF5") : Color.White), de.Bounds); string txt2 = cbo.GetItemText(cbo.Items[de.Index]); using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#064e3b") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(txt2, f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };
                var ov = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
                ov.Paint += (s2, pe2) => { var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.FillPath(brs, path2); using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.DrawPath(pen2, path2); int ax = 18, ay = ov.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); } string selTxt = cbo.SelectedIndex >= 0 ? cbo.GetItemText(cbo.SelectedItem) : placeholder; bool isPh = cbo.SelectedIndex < 0 || selTxt == placeholder; using (var f2 = new Font("Cairo", 11F)) using (var b2 = new SolidBrush(isPh ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A"))) g.DrawString(selTxt, f2, b2, new RectangleF(36, 0, ov.Width - 52, ov.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter }); };
                cbo.SetBounds(0, 0, 400, 42); ov.Controls.Add(cbo); ov.Resize += (s2, e2) => cbo.SetBounds(0, 0, ov.Width, 42); cbo.SelectedIndexChanged += (s2, e2) => ov.Invalidate();
                cboOut = cbo; return ov;
            }
            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            var errV = MakeErrLbl();
            var cboVehPanel = MkCboField("«Œ — ”Ì«—…", out var cboVeh);
            var vList = new List<VehicleDto>(_vehicles); vList.Insert(0, new VehicleDto { Id = 0, Name = "«Œ — ”Ì«—…" });
            cboVeh.DisplayMember = "Name"; cboVeh.ValueMember = "Id"; cboVeh.DataSource = null; cboVeh.DataSource = vList;
            string[] arabicMonths = { "Ì‰«Ì—", "›»—«Ì—", "„«—”", "√»—Ì·", "„«ÌÊ", "ÌÊ‰ÌÊ", "ÌÊ·ÌÊ", "√€”ÿ”", "”» „»—", "√ﬂ Ê»—", "‰Ê›„»—", "œÌ”„»—" };
            var cboMonthPanel = MkCboField("«Œ — «·‘Â—", out var cboMonth);
            foreach (var m in arabicMonths) cboMonth.Items.Add(m);
            cboMonth.SelectedIndex = Math.Max(0, DateTime.Now.Month - 2);
            var cboYearPanel = MkCboField("«Œ — «·”‰…", out var cboYear);
            int curYear = DateTime.Now.Year; for (int y = curYear - 3; y <= curYear + 1; y++) cboYear.Items.Add(y);
            cboYear.SelectedItem = curYear;
            body.SuspendLayout();
            body.Controls.Add(Sp(8)); body.Controls.Add(cboYearPanel); body.Controls.Add(MkLblPanel("«·”‰… *"));
            body.Controls.Add(Sp(8)); body.Controls.Add(cboMonthPanel); body.Controls.Add(MkLblPanel("«·‘Â— *"));
            body.Controls.Add(Sp(8)); body.Controls.Add(errV); body.Controls.Add(cboVehPanel); body.Controls.Add(MkLblPanel("«·”Ì«—… *")); body.Controls.Add(Sp(8));
            body.ResumeLayout(true);
            var btnGen = MakeSaveBtn("≈‰‘«¡ Ê Õ„Ì· PDF");
            btnGen.FillColor = ColorTranslator.FromHtml("#065F46"); btnGen.HoverState.FillColor = ColorTranslator.FromHtml("#047857"); btnGen.ShadowDecoration.Color = Color.FromArgb(40, 6, 95, 70);
            btnGen.Click += async (s, e) => {
                errV.Visible = false; errV.Height = 0;
                int vId = 0; try { vId = Convert.ToInt32(cboVeh.SelectedValue); } catch { }
                if (vId == 0) { errV.Text = "ï «Œ — ”Ì«—…"; errV.Visible = true; errV.Height = 18; return; }
                int selMonth = cboMonth.SelectedIndex + 1; int selYear = cboYear.SelectedItem != null ? (int)cboYear.SelectedItem : curYear;
                var vehicle = _vehicles.FirstOrDefault(v => v.Id == vId); if (vehicle == null) return;
                btnGen.Enabled = false; btnGen.Text = "Ã«—Ú «·≈‰‘«¡...";
                try
                {
                    var orders = await Task.Run(() => _vehicleService.GetAllDispatchOrders()?.Where(o => o.VehicleId == vId && o.CreatedAt.Month == selMonth && o.CreatedAt.Year == selYear).ToList() ?? new List<DispatchOrderDto>());
                    if (orders.Count == 0) { ShowErrorToast($"·«  ÊÃœ √Ê«„— ’—› ›Ì {arabicMonths[selMonth - 1]} {selYear}"); return; }
                    var pdfBytes = await Task.Run(() => _pdfService.GenerateVehicleMonthlyReport(vehicle, orders, selMonth, selYear));
                    string safeName = vehicle.Name; foreach (char c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');
                    using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ «· ﬁ—Ì— «·‘Â—Ì", Filter = "PDF|*.pdf", FileName = $" ﬁ—Ì—_{safeName}_{selYear}_{arabicMonths[selMonth - 1]}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                    { if (sfd.ShowDialog() == DialogResult.OK) { File.WriteAllBytes(sfd.FileName, pdfBytes); pf.Close(); ShowSuccessToast($" „ Õ›Ÿ «· ﬁ—Ì—  ({orders.Count} √„— ’—›) ?"); } }
                }
                catch (Exception ex) { ShowErrorToast("›‘· ≈‰‘«¡ «· ﬁ—Ì—: " + GetInner(ex)); }
                finally { btnGen.Enabled = true; btnGen.Text = "≈‰‘«¡ Ê Õ„Ì· PDF"; }
            };
            footer.Controls.Add(btnGen);
            pf.Controls.Add(body); pf.Controls.Add(footer); pf.Controls.Add(pnlHead);
            pf.ShowDialog(this);
        }

        // ???????????????????????????????????????????????????????
        //  DETAILS POPUP  ó »œÊ‰ √⁄„œ… ﬂ—« Ì‰/⁄·» ≈÷«›Ì…
        // ???????????????????????????????????????????????????????
        private void ShowDetailsPopup(DispatchOrderDto dto)
        {
            var soldQties = new Dictionary<int, int>(); var returnedQties = new Dictionary<int, int>(); var originalQties = new Dictionary<int, int>();
            try { soldQties = _vehicleService.GetSoldQuantitiesByDispatch(dto.Id); } catch { }
            try { returnedQties = _vehicleService.GetReturnedQuantitiesByDispatch(dto.Id); } catch { }
            try { originalQties = _vehicleService.GetOriginalQuantitiesByDispatch(dto.Id); } catch { }
            decimal grandTotal = dto.Items.Sum(i => { int origQty = originalQties.ContainsKey(i.ProductId) ? originalQties[i.ProductId] : i.Quantity; return (decimal)origQty * i.SalePrice; });
            string dateStr = ToLocalStr(dto.CreatedAt) + "   " + new DateTime(dto.CreatedAt.Year, dto.CreatedAt.Month, dto.CreatedAt.Day, dto.CreatedAt.Hour, dto.CreatedAt.Minute, dto.CreatedAt.Second, DateTimeKind.Local).ToString("HH:mm:ss");
            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.55, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);
            int docW = Math.Min(1020, sc.Width - 80), docH = Math.Min(sc.Height - 60, 960);
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(docW, docH), BackColor = ColorTranslator.FromHtml("#F4F6FB"), ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            pf.Location = new Point(sc.Left + (sc.Width - docW) / 2, sc.Top + (sc.Height - docH) / 2);
            using (var rgn = new GraphicsPath()) { int r = 16; rgn.AddArc(0, 0, r * 2, r * 2, 180, 90); rgn.AddArc(docW - r * 2, 0, r * 2, r * 2, 270, 90); rgn.AddArc(docW - r * 2, docH - r * 2, r * 2, r * 2, 0, 90); rgn.AddArc(0, docH - r * 2, r * 2, r * 2, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = ColorTranslator.FromHtml("#F4F6FB"), Padding = new Padding(24, 20, 24, 0) };
            pf.Controls.Add(outer);
            var doc = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            doc.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; using (var sh = new SolidBrush(Color.FromArgb(18, 0, 0, 80))) g.FillRectangle(sh, 3, 3, doc.Width - 2, doc.Height - 2); using (var path = RoundPath(new Rectangle(0, 0, doc.Width - 3, doc.Height - 3), 10)) g.FillPath(Brushes.White, path); using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) using (var path2 = RoundPath(new Rectangle(0, 0, doc.Width - 4, doc.Height - 4), 10)) g.DrawPath(pen, path2); };
            outer.Controls.Add(doc);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            doc.Controls.Add(scroll);
            int padX = 36;

            // √⁄„œ… «· ›«’Ì· ó »œÊ‰ "ﬂ—« Ì‰" Ê"⁄·» ≈÷«›Ì…"
            string[] colHdrs = { "«·„‰ Ã", "«·ﬂ„Ì… «·√’·Ì…", "„”ÕÊ» »›Ê« Ì—", "„— Ã⁄", "„ »ﬁÌ", "”⁄— «·»Ì⁄", "«·≈Ã„«·Ì" };
            float[] colW = { 0.25f, 0.13f, 0.14f, 0.11f, 0.11f, 0.13f, 0.13f };

            int headerH = 120, cardsH = 108, sep1H = 28, secTitleH = 44, tblHH = 54, rowH2 = 64, totalRowH = 78, footerH = 52;
            int totalContentH = headerH + cardsH + sep1H + secTitleH + tblHH + dto.Items.Count * rowH2 + totalRowH + footerH + 60;
            var canvas = new Panel { Width = scroll.ClientSize.Width > 0 ? scroll.ClientSize.Width : docW - 48, Height = totalContentH, BackColor = Color.White };
            scroll.Controls.Add(canvas);
            scroll.SizeChanged += (s, e) => { canvas.Width = scroll.ClientSize.Width; };
            canvas.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = canvas.Width, y = 28;
                var hrc = new Rectangle(padX, y, W - padX * 2, 80);
                using (var br = new LinearGradientBrush(hrc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal)) using (var path = RoundPath(hrc, 10)) g.FillPath(br, path);
                using (var dot = new SolidBrush(Color.FromArgb(14, 255, 255, 255))) for (int xi = hrc.Left + 8; xi < hrc.Right; xi += 18) for (int yi = hrc.Top + 4; yi < hrc.Bottom; yi += 18) g.FillEllipse(dot, xi, yi, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(10, 255, 255, 255))) { g.FillEllipse(cb, hrc.Right - 120, hrc.Top - 40, 180, 180); g.FillEllipse(cb, hrc.Left - 30, hrc.Top - 30, 130, 130); }
                using (var tf = new Font("Cairo", 24F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString("√„— ’—› »÷«⁄…", tf); g.DrawString("√„— ’—› »÷«⁄…", tf, tb, hrc.Left + (hrc.Width - tsz.Width) / 2f, hrc.Top + 12); }
                using (var nf = new Font("Cairo", 17F, FontStyle.Bold)) using (var nb = new SolidBrush(ColorTranslator.FromHtml("#93C5FD"))) g.DrawString($"#{dto.Id}", nf, nb, hrc.Left + 14, hrc.Top + 20);
                using (var df = new Font("Cairo", 11F)) using (var db2 = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) { var dsz = g.MeasureString(dateStr, df); g.DrawString(dateStr, df, db2, hrc.Right - dsz.Width - 14, hrc.Top + 26); }
                y += 88;
                string sub = $"«·”Ì«—…:  {dto.VehicleName}          «·„‰œÊ»:  {dto.RepName}          ⁄œœ «·„‰ Ã« :  {dto.Items.Count} „‰ Ã";
                using (var sf2 = new Font("Cairo", 13F)) using (var sb2 = new SolidBrush(ColorTranslator.FromHtml("#374151"))) { var ssz = g.MeasureString(sub, sf2); g.DrawString(sub, sf2, sb2, hrc.Left + (hrc.Width - ssz.Width) / 2f, y); }
                y += 36;
                var cards2 = new[] { ("«· «—ÌŒ", ToLocalStr(dto.CreatedAt), "#1a2f5e", "#EFF6FF", "#BFDBFE"), ("«·”Ì«—…", dto.VehicleName ?? "", "#1565C0", "#EFF6FF", "#93C5FD"), ("«·„‰œÊ»", dto.RepName ?? "", "#6D28D9", "#F5F3FF", "#C4B5FD"), ("≈Ã„«·Ì «·√„—", grandTotal.ToString("N2", Inv) + " Ã‰ÌÂ", "#065F46", "#ECFDF5", "#A7F3D0") };
                int cGap = 14, cw3 = (W - padX * 2 - cGap * (cards2.Length - 1)) / cards2.Length, ch3 = 90;
                for (int i = 0; i < cards2.Length; i++) { var it = cards2[i]; int cx3 = padX + i * (cw3 + cGap); var crc2 = new Rectangle(cx3, y + 4, cw3, ch3); using (var sh = new SolidBrush(Color.FromArgb(14, 0, 30, 80))) g.FillRectangle(sh, crc2.X + 3, crc2.Y + 3, crc2.Width, crc2.Height); using (var path = RoundPath(crc2, 12)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml(it.Item4)), path); g.DrawPath(new Pen(ColorTranslator.FromHtml(it.Item5), 1.2f), path); } using (var lb2 = new SolidBrush(ColorTranslator.FromHtml(it.Item3))) g.FillRectangle(lb2, crc2.X + 12, crc2.Y, crc2.Width - 24, 4); using (var lf = new Font("Cairo", 10F)) using (var lb3 = new SolidBrush(ColorTranslator.FromHtml("#64748B"))) g.DrawString(it.Item1, lf, lb3, new RectangleF(crc2.X, crc2.Y + 10, crc2.Width, 24), new StringFormat { Alignment = StringAlignment.Center }); float fs2 = it.Item2.Length > 18 ? 11.5F : 13F; using (var vf = new Font("Cairo", fs2, FontStyle.Bold)) using (var vb2 = new SolidBrush(ColorTranslator.FromHtml(it.Item3))) g.DrawString(it.Item2, vf, vb2, new RectangleF(crc2.X, crc2.Y + 36, crc2.Width, ch3 - 36), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
                y += ch3 + 20;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) g.DrawLine(pen, padX, y + 8, W - padX, y + 8); y += 24;
                using (var lb2 = new SolidBrush(ColorTranslator.FromHtml("#1565C0"))) g.FillRectangle(lb2, W - padX, y + 6, 5, 32);
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1a2f5e"))) g.DrawString(" ›«’Ì· «·„‰ Ã« ", tf, tb, new RectangleF(padX, y + 4, W - padX * 2 - 10, 36), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); y += 48;
                int tableW = W - padX * 2;
                var trc = new Rectangle(padX, y, tableW, 54);
                using (var br = new LinearGradientBrush(trc, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal)) using (var path = RoundPath(trc, 8)) g.FillPath(br, path);
                float tx = W - padX; using (var f = new Font("Cairo", 11F, FontStyle.Bold)) for (int i = 0; i < colHdrs.Length; i++) { float cw2 = tableW * colW[i]; tx -= cw2; g.DrawString(colHdrs[i], f, Brushes.White, new RectangleF(tx, y, cw2, 54), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
                y += 54;
                for (int ri = 0; ri < dto.Items.Count; ri++)
                {
                    var item = dto.Items[ri]; Color rowBg = ri % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#F8FAFF");
                    int totalBoxes = originalQties.ContainsKey(item.ProductId) ? originalQties[item.ProductId] : item.Quantity;
                    int sold = soldQties.ContainsKey(item.ProductId) ? soldQties[item.ProductId] : 0;
                    int returned = returnedQties.ContainsKey(item.ProductId) ? returnedQties[item.ProductId] : 0;
                    int remaining = Math.Max(0, totalBoxes - sold - returned);
                    decimal rowTotal = (decimal)totalBoxes * item.SalePrice;
                    Color remainColor = remaining == 0 ? ColorTranslator.FromHtml("#065F46") : ColorTranslator.FromHtml("#DC2626");
                    // 7 √⁄„œ…: «·„‰ Ã° «·ﬂ„Ì… «·√’·Ì…° „”ÕÊ»° „— Ã⁄° „ »ﬁÌ° ”⁄—° ≈Ã„«·Ì
                    string[] vals = {
                        item.ProductName ?? $"#{item.ProductId}",
                        $"{totalBoxes} ﬁÿ⁄…",
                        sold > 0 ? $"{sold} ﬁÿ⁄…" : "ó",
                        returned > 0 ? $"{returned} ﬁÿ⁄…" : "ó",
                        remaining == 0 ? "? ‰›œ" : $"{remaining} ﬁÿ⁄…",
                        item.SalePrice > 0 ? item.SalePrice.ToString("N2", Inv) + " Ã" : "ó",
                        rowTotal.ToString("N2", Inv) + " Ã"
                    };
                    Color[] colors = { ColorTranslator.FromHtml("#0F172A"), ColorTranslator.FromHtml("#059669"), ColorTranslator.FromHtml("#D97706"), ColorTranslator.FromHtml("#7C3AED"), remainColor, ColorTranslator.FromHtml("#374151"), ColorTranslator.FromHtml("#DC2626") };
                    bool[] bold2 = { true, true, true, true, true, false, true };
                    g.FillRectangle(new SolidBrush(rowBg), padX, y, tableW, rowH2);
                    using (var hlt = new SolidBrush(Color.FromArgb(remaining == 0 ? 8 : 6, remaining == 0 ? 5 : 220, remaining == 0 ? 150 : 38, remaining == 0 ? 105 : 38))) g.FillRectangle(hlt, padX, y, tableW, rowH2);
                    float rx = W - padX; for (int i = 0; i < vals.Length; i++) { float cw2 = tableW * colW[i]; rx -= cw2; float fs2 = i == 0 ? 13F : 11F; using (var f = new Font("Cairo", fs2, bold2[i] ? FontStyle.Bold : FontStyle.Regular)) using (var tb = new SolidBrush(colors[i])) g.DrawString(vals[i], f, tb, new RectangleF(rx + 3, y, cw2 - 6, rowH2), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
                    using (var lp = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) g.DrawLine(lp, padX + 8, y + rowH2 - 1, W - padX - 8, y + rowH2 - 1);
                    y += rowH2;
                }
                y += 8; var trc2 = new Rectangle(padX, y, W - padX * 2, 70);
                using (var br = new LinearGradientBrush(trc2, ColorTranslator.FromHtml("#EFF6FF"), ColorTranslator.FromHtml("#DBEAFE"), LinearGradientMode.Horizontal)) using (var path = RoundPath(trc2, 10)) g.FillPath(br, path);
                using (var pen = new Pen(ColorTranslator.FromHtml("#93C5FD"), 1.2f)) using (var path2 = RoundPath(trc2, 10)) g.DrawPath(pen, path2);
                using (var lf = new Font("Cairo", 14F, FontStyle.Bold)) using (var lb2 = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e"))) { var lsz = g.MeasureString("«·≈Ã„«·Ì «·ﬂ·Ì ·√„— «·’—›:", lf); g.DrawString("«·≈Ã„«·Ì «·ﬂ·Ì ·√„— «·’—›:", lf, lb2, trc2.Right - lsz.Width - 20, trc2.Top + (trc2.Height - lsz.Height) / 2f); }
                using (var vf = new Font("Cairo", 20F, FontStyle.Bold)) using (var vb2 = new SolidBrush(ColorTranslator.FromHtml("#1565C0"))) { var vsz = g.MeasureString(grandTotal.ToString("N2", Inv) + " Ã‰ÌÂ", vf); g.DrawString(grandTotal.ToString("N2", Inv) + " Ã‰ÌÂ", vf, vb2, trc2.Left + 20, trc2.Top + (trc2.Height - vsz.Height) / 2f); }
                y += 78; y += 16;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) g.DrawLine(pen, padX, y, W - padX, y); y += 12;
                string note2 = $" „ ≈‰‘«¡ Â–« «·√„— » «—ÌŒ  {dateStr}";
                using (var nf = new Font("Cairo", 10F)) using (var nb2 = new SolidBrush(ColorTranslator.FromHtml("#94A3B8"))) { var nsz = g.MeasureString(note2, nf); g.DrawString(note2, nf, nb2, (W - nsz.Width) / 2f, y); }
                int realH = y + 40; if (canvas.Height != realH) canvas.Height = realH;
            };
            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = ColorTranslator.FromHtml("#F4F6FB"), Padding = new Padding(40, 10, 40, 14) };
            outer.Controls.Add(btnBar);
            var btnClose = new Guna2Button { Dock = DockStyle.Fill, Text = "≈€·«ﬁ", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#1a2f5e"), ForeColor = Color.White, Font = new Font("Cairo", 13F, FontStyle.Bold), Animated = true };
            btnClose.HoverState.FillColor = ColorTranslator.FromHtml("#1565c0"); btnClose.ShadowDecoration.Enabled = true; btnClose.ShadowDecoration.Color = Color.FromArgb(40, 26, 47, 94); btnClose.ShadowDecoration.Depth = 12;
            btnClose.Click += (s, e) => pf.Close();
            btnBar.Controls.Add(btnClose); btnBar.BringToFront();
            pf.ShowDialog(this);
        }

        private Form CreatePopup(int w, int h, string title, string sub)
        {
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(w, h), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            var sc = Screen.FromControl(this).WorkingArea;
            pf.Location = new Point(sc.Left + (sc.Width - w) / 2, sc.Top + (sc.Height - h) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 40, 40, 180, 90); rgn.AddArc(w - 40, 0, 40, 40, 270, 90); rgn.AddArc(w - 40, h - 40, 40, 40, 0, 90); rgn.AddArc(0, h - 40, 40, 40, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            var popup = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White };
            popup.ShadowDecoration.Enabled = true; popup.ShadowDecoration.Depth = 32; popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            pf.Controls.Add(popup);
            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(db, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, pnlHead.Width - tsz.Width - 60, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString(sub, sf3); g.DrawString(sub, sf3, sb3, pnlHead.Width - ssz.Width - 60, 54); }
            };
            var btnClose = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnClose.HoverState.FillColor = Color.FromArgb(80, 255, 255, 255); btnClose.Click += (s7, e7) => pf.Close();
            pnlHead.Controls.Add(btnClose); pnlHead.Layout += (s8, e8) => btnClose.Location = new Point(25, 20);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 12, 22, 4), Name = "body", RightToLeft = RightToLeft.No, AutoScroll = true };
            var footer = new Panel { Dock = DockStyle.Bottom, BackColor = Color.White, Height = 68, Padding = new Padding(22, 10, 22, 14), Name = "footer" };
            footer.Paint += (s6, pe6) => { var g = pe6.Graphics; using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 2f)) g.DrawLine(pen, 0, 0, footer.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 2, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal)) g.FillRectangle(br, 0, 2, footer.Width, 2); };
            popup.Controls.Add(body); popup.Controls.Add(footer); popup.Controls.Add(pnlHead);
            pf.Tag = body; return pf;
        }

        private Form ShowOverlay()
        {
            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.55, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this); return overlay;
        }
        private void CloseOverlay(Form overlay) { try { overlay.Close(); overlay.Dispose(); } catch { } }
        private Panel FindFooter(Form pf) => pf.Controls.Find("footer", true).FirstOrDefault() as Panel;

        private bool ShowDeleteConfirm(string label)
        {
            bool result = false;
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(420, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.Transparent };
            header.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height); using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#C0392B"), ColorTranslator.FromHtml("#E74C3C"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2); using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < header.Width; x += 20) for (int y = 6; y < header.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2); int ix = header.Width - 62, iy = 22, isz = 46; using (var ip = RoundPath(new Rectangle(ix, iy, isz, isz), 12)) { g.FillPath(new SolidBrush(Color.FromArgb(40, 255, 255, 255)), ip); g.DrawPath(new Pen(Color.FromArgb(60, 255, 255, 255), 1f), ip); } using (var pen = new Pen(Color.White, 2f)) { int cx2 = ix + isz / 2, cy2 = iy + isz / 2; g.DrawLine(pen, cx2 - 10, cy2 - 8, cx2 + 10, cy2 - 8); g.DrawLine(pen, cx2 - 5, cy2 - 12, cx2 + 5, cy2 - 12); g.DrawRectangle(pen, cx2 - 9, cy2 - 6, 18, 16); g.DrawLine(pen, cx2 - 3, cy2 - 2, cx2 - 3, cy2 + 6); g.DrawLine(pen, cx2 + 3, cy2 - 2, cx2 + 3, cy2 + 6); } using (var tf = new Font("Cairo", 18F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString("Õ–› «·√„—", tf); g.DrawString("Õ–› «·√„—", tf, tb, header.Width - tsz.Width - 68, 14); } using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.DrawString("Â–« «·≈Ã—«¡ ·« Ì„ﬂ‰ «· —«Ã⁄ ⁄‰Â", sf3, sb3, header.Width - 238, 52); };
            var delBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 18, 28, 0) };
            var nameBox = new Panel { Dock = DockStyle.Top, Height = 50 };
            nameBox.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, nameBox.Width - 1, nameBox.Height - 1); using (var path = RoundPath(rc2, 10)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1.5f), path); } using (var f = new Font("Cairo", 12F, FontStyle.Bold)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#B91C1C"))) g.DrawString($"  {label}  ", f, b, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            delBody.Controls.Add(nameBox);
            delBody.Controls.Add(new Label { Text = "Â· √‰  „ √ﬂœ „‰ «·Õ–›ø ”Ì „ ⁄ﬂ” «·ﬂ„Ì«  ›Ì «·„Œ“‰", Font = new Font("Cairo", 11F), ForeColor = ColorTranslator.FromHtml("#374151"), Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            var delFooter = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(24, 12, 24, 20) };
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var btnCancel = new Guna2Button { Dock = DockStyle.Fill, Text = "≈·€«¡", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#F1F5F9"), ForeColor = ColorTranslator.FromHtml("#64748B"), BorderColor = ColorTranslator.FromHtml("#E2E8F0"), BorderThickness = 1, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(0, 0, 6, 0) };
            var btnConfirm = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ–›", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#EF4444"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Animated = true };
            btnCancel.HoverState.FillColor = ColorTranslator.FromHtml("#E2E8F0"); btnCancel.Click += (s, e) => dlg.Close();
            btnConfirm.HoverState.FillColor = ColorTranslator.FromHtml("#DC2626"); btnConfirm.ShadowDecoration.Enabled = true; btnConfirm.ShadowDecoration.Color = Color.FromArgb(40, 239, 68, 68); btnConfirm.ShadowDecoration.Depth = 8;
            btnConfirm.Click += (s, e) => { result = true; dlg.Close(); };
            tbl.Controls.Add(btnCancel, 0, 0); tbl.Controls.Add(btnConfirm, 1, 0);
            delFooter.Controls.Add(tbl); root.Controls.Add(delBody); root.Controls.Add(delFooter); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) dlg.Close(); };
            dlg.ShowDialog(this); return result;
        }

        private void ShowErrorDialog(string entityName, string rawError)
        {
            bool isFk = rawError != null && (rawError.Contains("FOREIGN KEY") || rawError.Contains("REFERENCE") || rawError.Contains("constraint"));
            string title = isFk ? "·« Ì„ﬂ‰ Õ–› «·√„—" : "ÕœÀ Œÿ√"; string line1 = isFk ? $"·« Ì„ﬂ‰ Õ–› «·√„— \"{entityName}\"" : "ÕœÀ Œÿ√ €Ì— „ Êﬁ⁄"; string line2 = isFk ? "·√‰ ·Â »‰Êœ „— »ÿ… ›Ì «·‰Ÿ«„." : rawError; string line3 = isFk ? "ÌÃ» Õ–› «·»‰Êœ «·„— »ÿ… √Ê·« À„ «·„Õ«Ê·…." : "";
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(440, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.Transparent };
            header.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height); using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#B45309"), ColorTranslator.FromHtml("#D97706"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2); using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < header.Width; x += 20) for (int y = 6; y < header.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2); int ix = header.Width - 62, iy = 18, isz = 46; using (var ip = RoundPath(new Rectangle(ix, iy, isz, isz), 23)) g.FillPath(new SolidBrush(Color.FromArgb(45, 255, 255, 255)), ip); using (var pen = new Pen(Color.White, 3f)) using (var wBr = new SolidBrush(Color.White)) { int cx2 = ix + isz / 2, cy2 = iy + isz / 2; g.DrawLine(pen, cx2, cy2 - 11, cx2, cy2 + 1); g.FillEllipse(wBr, cx2 - 3, cy2 + 7, 6, 6); } using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, header.Width - tsz.Width - 70, 12); } using (var sf3 = new Font("Cairo", 9.5F)) using (var sb3 = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.DrawString(" ⁄–¯—  ‰›Ì– ⁄„·Ì… «·Õ–›", sf3, sb3, header.Width - 222, 50); };
            var errBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 16, 22, 0) };
            var msgPanel = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            msgPanel.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, msgPanel.Width - 1, msgPanel.Height - 1); using (var path = RoundPath(rc2, 12)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFFBEB")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FDE68A"), 1.5f), path); } using (var f1 = new Font("Cairo", 11.5F, FontStyle.Bold)) using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#92400E"))) g.DrawString(line1, f1, b1, new RectangleF(12, 10, msgPanel.Width - 24, 28), new StringFormat { Alignment = StringAlignment.Far }); using (var f2 = new Font("Cairo", 10.5F)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#78350F"))) g.DrawString(line2, f2, b2, new RectangleF(12, 40, msgPanel.Width - 24, 26), new StringFormat { Alignment = StringAlignment.Far }); if (!string.IsNullOrEmpty(line3)) using (var f3 = new Font("Cairo", 9.5F, FontStyle.Italic)) using (var b3 = new SolidBrush(ColorTranslator.FromHtml("#A16207"))) g.DrawString(line3, f3, b3, new RectangleF(12, 66, msgPanel.Width - 24, 24), new StringFormat { Alignment = StringAlignment.Far }); };
            errBody.Controls.Add(msgPanel);
            var errFooter = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White, Padding = new Padding(24, 10, 24, 14) };
            var btnOk = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ”‰«° ›Â„ ", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#D97706"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Animated = true };
            btnOk.HoverState.FillColor = ColorTranslator.FromHtml("#B45309"); btnOk.ShadowDecoration.Enabled = true; btnOk.ShadowDecoration.Color = Color.FromArgb(40, 217, 119, 6); btnOk.ShadowDecoration.Depth = 8;
            btnOk.Click += (s, e) => dlg.Close();
            errFooter.Controls.Add(btnOk); root.Controls.Add(errBody); root.Controls.Add(errFooter); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Return) dlg.Close(); };
            dlg.ShowDialog(this);
        }

        private async void ShowSuccessToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#10B981"), ColorTranslator.FromHtml("#ECFDF5"));
        private async void ShowErrorToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#FEF2F2"));

        private async Task ShowToast(string msg, Color accent, Color bgc)
        {
            var t = new Panel { Size = new Size(340, 50), BackColor = bgc, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath()) { gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(t.Width - 20, 0, 20, 20, 270, 90); gp.AddArc(t.Width - 20, t.Height - 20, 20, 20, 0, 90); gp.AddArc(0, t.Height - 20, 20, 20, 90, 90); gp.CloseFigure(); t.Region = new Region(gp); }
            t.Paint += (s, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(accent, 1.5f)) using (var path = RoundPath(new Rectangle(0, 0, t.Width - 1, t.Height - 1), 10)) pe.Graphics.DrawPath(pen, path); pe.Graphics.FillRectangle(new SolidBrush(accent), 0, 7, 4, t.Height - 14); using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1F2937"))) pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, t.Width - 8, t.Height), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36);
            Controls.Add(t); t.BringToFront();
            t.Click += (s, e) => { try { Controls.Remove(t); t.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10) { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + (100 - i) / 5); await Task.Delay(7); }
            await Task.Delay(2600);
            for (int i = 0; i <= 100; i += 10) { try { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + i / 5); } catch { break; } await Task.Delay(7); }
            try { Controls.Remove(t); t.Dispose(); } catch { }
        }

        private static StringFormat SF(StringAlignment h, StringAlignment v = StringAlignment.Center) =>
            new StringFormat { Alignment = h, LineAlignment = v };

        private class DispatchDisplayItem { public int Id { get; set; } public string DisplayText { get; set; } }

        private string ToLocalStr(DateTime dt) =>
            new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, DateTimeKind.Local).ToString("yyyy/MM/dd  HH:mm");

        private Guna2TextBox MakeTxt(string ph)
        {
            var t = new Guna2TextBox { Height = 40, Dock = DockStyle.Top, BorderRadius = 10, FillColor = ColorTranslator.FromHtml("#F9FAFB"), BorderColor = ColorTranslator.FromHtml("#E5E7EB"), Font = new Font("Cairo", 10.5F), PlaceholderText = ph, PlaceholderForeColor = ColorTranslator.FromHtml("#C4C9D4"), ForeColor = ColorTranslator.FromHtml("#111827"), TextAlign = HorizontalAlignment.Right };
            t.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF"); t.FocusedState.FillColor = Color.White; return t;
        }
        private Guna2ComboBox MakeCbo()
        {
            var c = new Guna2ComboBox { Height = 40, Dock = DockStyle.Top, BorderRadius = 10, FillColor = ColorTranslator.FromHtml("#F9FAFB"), BorderColor = ColorTranslator.FromHtml("#E5E7EB"), Font = new Font("Cairo", 10.5F), ForeColor = ColorTranslator.FromHtml("#374151"), DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 32 };
            c.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF"); return c;
        }
        private Label MakeLbl(string t) => new Label { Text = t, Dock = DockStyle.Top, Height = 20, Font = new Font("Cairo", 9.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#374151"), TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent };
        private Label MakeErrLbl() => new Label { Dock = DockStyle.Top, Height = 0, Font = new Font("Cairo", 9F), ForeColor = ColorTranslator.FromHtml("#EF4444"), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Visible = false };
        private Panel MakeSp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };

        private Guna2Button MakeSaveBtn(string text)
        {
            var b = new Guna2Button { Dock = DockStyle.Fill, Text = text, BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#4E73DF"), ForeColor = Color.White, Font = new Font("Cairo", 11.5F, FontStyle.Bold), Animated = true };
            b.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9"); b.ShadowDecoration.Enabled = true; b.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223); b.ShadowDecoration.Depth = 10; return b;
        }

        private GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2; var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        private static string GetInner(Exception ex) { if (ex == null) return ""; var e = ex; while (e.InnerException != null) e = e.InnerException; return e.Message; }

        private void VehiclesForm_Load(object sender, EventArgs e) { }
    }
}