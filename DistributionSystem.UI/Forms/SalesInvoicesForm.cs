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
    public partial class SalesInvoicesForm : Form
    {
        private readonly SalesInvoiceService _invoiceService;
        private readonly CustomerService _customerService;
        private readonly ProductService _productService;
        private readonly SalesInvoicePdfService _invoicePdf = new SalesInvoicePdfService();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Guna2DataGridView dgvInvoices;
        private Label lblInvoiceCount;
        private Guna2TextBox txtSearch;
        private System.Threading.Timer _searchTimer;
        private List<SalesInvoiceDto> _invoices = new List<SalesInvoiceDto>();
        private int _page = 1;
        private const int PageSize = 6;
        private Panel _paginationBar;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_invoices.Count / (double)PageSize));

        private static readonly PropertyInfo _dbProp = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent) { foreach (Control c in parent.Controls) { try { _dbProp?.SetValue(c, true); } catch { } if (c.Controls.Count > 0) EnableDbAll(c); } }

        public SalesInvoicesForm()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            _invoiceService = new SalesInvoiceService();
            _customerService = new CustomerService();
            _productService = new ProductService();
            BuildNewUI();
            Shown += (s, e) => BeginInvoke(new Action(LoadInvoices));
            SizeChanged += (s, e) => FitColumns();
        }

        private void BuildNewUI()
        {
            this.SuspendLayout();
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5"); Padding = new Padding(0);
            foreach (Control c in Controls) if (c != null) c.Visible = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildTableCard(), 0, 1);
            Controls.Add(root); root.BringToFront();
            EnableDbAll(this);
            this.ResumeLayout(true);
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
                    string title = "›Ê« Ì— «·»Ì⁄"; string sub = "≈œ«—… ›Ê« Ì— «·»Ì⁄ Ê«·„œ›Ê⁄« ";
                    var szT = g.MeasureString(title, tf); var szS = g.MeasureString(sub, sf2);
                    float gap = 4f, block = szT.Height + gap + szS.Height, startY = (banner.Height - block) / 2f;
                    using (var tb = new SolidBrush(Color.White)) g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);
                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.DrawString(sub, sf2, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);
                }
            };
            var btnAdd = new Guna2Button { Text = "+ ›« Ê—… ÃœÌœ…", FillColor = Color.FromArgb(30, 255, 255, 255), ForeColor = Color.White, BorderRadius = 12, BorderThickness = 1, BorderColor = Color.FromArgb(60, 255, 255, 255), Font = new Font("Cairo", 10F, FontStyle.Bold), Size = new Size(148, 38), Anchor = AnchorStyles.Left | AnchorStyles.Top, Location = new Point(12, 6) };
            btnAdd.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAdd.Click += (s, e) => ShowInvoicePopup();
            banner.Controls.Add(btnAdd);
            pnl.Controls.Add(banner); return pnl;
        }

        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblInvoiceCount = new Label { Text = "0 ›« Ê—…", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblInvoiceCount.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblInvoiceCount);
            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("”Ã· ›Ê« Ì— «·»Ì⁄", tf); g.DrawString("”Ã· ›Ê« Ì— «·»Ì⁄", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
                string badge = lblInvoiceCount?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badge, bf); int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2)) using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };

            txtSearch = new Guna2TextBox { Dock = DockStyle.Fill, BorderRadius = 8, PlaceholderText = "«»ÕÀ »«”„ «·⁄„Ì·...", FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#94A3B8"), BorderThickness = 1, Font = new Font("Cairo", 10F), TextAlign = HorizontalAlignment.Right, ForeColor = ColorTranslator.FromHtml("#0F172A"), PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8") };
            txtSearch.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF");
            txtSearch.TextChanged += (s, e) =>
            {
                _searchTimer?.Dispose(); _page = 1;
                _searchTimer = new System.Threading.Timer(_ => { try { BeginInvoke(new Action(LoadInvoices)); } catch { } }, null, 350, System.Threading.Timeout.Infinite);
            };
            var searchWrapper = new Panel { Width = 200, Height = 32, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Top, Location = new Point(12, (58 - 32) / 2) };
            searchWrapper.Controls.Add(txtSearch);
            topBar.Controls.Add(searchWrapper);

            var searchSep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSep.Paint += (s, pe) =>
            { using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSep.Width, 3), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal)) pe.Graphics.FillRectangle(br, 0, 0, searchSep.Width, 3); };

            dgvInvoices = new Guna2DataGridView { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, AutoGenerateColumns = false, ColumnHeadersHeight = 48, EnableHeadersVisualStyles = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AllowUserToAddRows = false, ReadOnly = true, CellBorderStyle = DataGridViewCellBorderStyle.None, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, AllowUserToResizeRows = false, ScrollBars = ScrollBars.Vertical, GridColor = Color.White, BackColor = Color.White };
            dgvInvoices.RowTemplate.Height = 70;
            dgvInvoices.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvInvoices.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvInvoices.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgvInvoices.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgvInvoices.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvInvoices.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgvInvoices.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");
            dgvInvoices.DefaultCellStyle.BackColor = Color.White;
            dgvInvoices.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvInvoices.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvInvoices.DefaultCellStyle.Font = new Font("Cairo", 12F, FontStyle.Bold);
            dgvInvoices.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgvInvoices.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgvInvoices, true); } catch { }

            BuildColumns();
            dgvInvoices.CellPainting += Dgv_CellPainting;
            dgvInvoices.CellClick += Dgv_CellClick;
            dgvInvoices.Resize += (s, e) => FitColumns();

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0); };

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrapper.Controls.Add(dgvInvoices);
            container.Controls.Add(dgvWrapper); container.Controls.Add(_paginationBar); container.Controls.Add(searchSep); container.Controls.Add(topBar);
            card.Controls.Add(container); return card;
        }

        private void BuildColumns()
        {
            dgvInvoices.Columns.Clear();
            void Add(string n, string h, string p, int w) => dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            Add("IId", "—ﬁ„", "Id", 55);
            Add("ICustomer", "«·⁄„Ì·", "CustomerName", 130);
            Add("ITotal", "«·≈Ã„«·Ì", "TotalAmount", 110);
            Add("IPaid", "«·„œ›Ê⁄", "PaidAmount", 110);
            Add("IRemain", "«·„ »ﬁÌ", "Remaining", 100);
            Add("IType", "‰Ê⁄ «·œ›⁄", "PaymentType", 100);
            Add("IStatus", "«·Õ«·…", "Status", 90);
            Add("IDate", "«· «—ÌŒ", "CreatedAt", 145);
            Add("IActions", "«·≈Ã—«¡« ", "", 200);
        }

        private void FitColumns()
        {
            if (dgvInvoices == null || dgvInvoices.Columns.Count == 0) return;
            int w = dgvInvoices.ClientSize.Width; if (w <= 0) return;
            int wId = 55, wC = 130, wTot = 100, wPaid = 100, wRem = 95, wType = 95, wS = 85, wD = 135;
            dgvInvoices.Columns["IId"].Width = wId;
            dgvInvoices.Columns["ICustomer"].Width = wC;
            dgvInvoices.Columns["ITotal"].Width = wTot;
            dgvInvoices.Columns["IPaid"].Width = wPaid;
            dgvInvoices.Columns["IRemain"].Width = wRem;
            dgvInvoices.Columns["IType"].Width = wType;
            dgvInvoices.Columns["IStatus"].Width = wS;
            dgvInvoices.Columns["IDate"].Width = wD;
            dgvInvoices.Columns["IActions"].Width = Math.Max(200, w - wId - wC - wTot - wPaid - wRem - wType - wS - wD);
        }

        private void LoadInvoices()
        {
            try
            {
                string q = txtSearch?.Text?.Trim() ?? "";
                var all = (_invoiceService.GetAllInvoices() ?? Enumerable.Empty<SalesInvoiceDto>()).ToList();
                if (!string.IsNullOrEmpty(q))
                    all = all.Where(x =>
                        (x.CustomerName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        x.Id.ToString().Contains(q)).ToList();
                _invoices = all;
                _page = Math.Min(_page, TotalPages);
                var page = _invoices.Skip((_page - 1) * PageSize).Take(PageSize).ToList();
                dgvInvoices.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                if (lblInvoiceCount != null) lblInvoiceCount.Text = $"{_invoices.Count} ›« Ê—…";
                RenderPagination();
            }
            catch (Exception ex) { ShowErrorToast("›‘·  Õ„Ì· «·›Ê« Ì—: " + GetInner(ex)); }
        }

        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = TotalPages;
            _paginationBar.Controls.Add(new Label { Text = $"⁄—÷ {Math.Min(_invoices.Count, (_page - 1) * PageSize + 1)}-{Math.Min(_invoices.Count, _page * PageSize)} „‰ {_invoices.Count}", Font = new Font("Cairo", 9.5F), ForeColor = ColorTranslator.FromHtml("#64748B"), AutoSize = false, Width = 180, Height = 56, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Right, BackColor = Color.Transparent });
            var pnlPages = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, WrapContents = false, Padding = new Padding(0) };
            pnlPages.Controls.Add(MkNav("õ", _page < total, () => { _page++; LoadInvoices(); }));
            for (int i = total; i >= 1; i--)
            {
                int pg = i; bool cur = pg == _page;
                var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = cur ? Cursors.Default : Cursors.Hand, Margin = new Padding(3, 10, 3, 10) };
                btn.Paint += (s, pe) =>
                {
                    var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    if (cur) { using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical)) using (var path = RoundPath(rc, 8)) g.FillPath(br, path); using (var f = new Font("Cairo", 10F, FontStyle.Bold)) g.DrawString(pg.ToString(), f, Brushes.White, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
                    else { using (var path = RoundPath(rc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); } using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151"))) g.DrawString(pg.ToString(), f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
                };
                if (!cur) btn.Click += (s, e) => { _page = pg; LoadInvoices(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MkNav("ã", _page > 1, () => { _page--; LoadInvoices(); }));
            _paginationBar.Controls.Add(pnlPages);
        }

        private Panel MkNav(string text, bool enabled, Action onClick)
        {
            var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(3, 10, 3, 10) };
            btn.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1); using (var path = RoundPath(rc, 8)) { g.FillPath(new SolidBrush(enabled ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#F1F5F9")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); } using (var f = new Font("Segoe UI", 13F)) using (var tb = new SolidBrush(enabled ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#CBD5E1"))) g.DrawString(text, f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            if (enabled) btn.Click += (s, e) => onClick();
            return btn;
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
                    using (var f = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); g.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
                    return;
                }
                if (e.RowIndex < 0) return;
                bool sel = dgvInvoices.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                var col = dgvInvoices.Columns[e.ColumnIndex].Name;
                if (col == "IStatus") PaintStatusCell(e, bg);
                else if (col == "IType") PaintTypeCell(e, bg);
                else if (col == "ITotal" || col == "IPaid" || col == "IRemain") PaintAmountCell(e, bg, col);
                else if (col == "IDate") PaintDateCell(e, bg);
                else if (col == "IActions") PaintActionsCell(e, bg);
                else { e.Handled = true; e.Graphics.FillRectangle(new SolidBrush(bg), e.CellBounds); e.PaintContent(e.CellBounds); }
                using (var wPen = new Pen(Color.White, 2f))
                { e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom); e.Graphics.DrawLine(wPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom); e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1); }
                using (var divPen = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f)) e.Graphics.DrawLine(divPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
            catch { }
        }

        private void PaintStatusCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds); g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            bool completed = (e.Value?.ToString() ?? "") == "Completed";
            Color fc = completed ? ColorTranslator.FromHtml("#065F46") : ColorTranslator.FromHtml("#92400E");
            Color bc = completed ? ColorTranslator.FromHtml("#ECFDF5") : ColorTranslator.FromHtml("#FFFBEB");
            Color brd = completed ? ColorTranslator.FromHtml("#A7F3D0") : ColorTranslator.FromHtml("#FDE68A");
            string txt = completed ? "„ﬂ „·…" : "„⁄·ﬁ…";
            int pw = 72, ph = 28, px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2, py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
            using (var path = RoundPath(new Rectangle(px, py, pw, ph), ph / 2)) { g.FillPath(new SolidBrush(bc), path); g.DrawPath(new Pen(brd, 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(fc)) g.DrawString(txt, f, tb, new RectangleF(px, py, pw, ph), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            g.ResetClip();
        }

        private void PaintTypeCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds); g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            bool cash = (e.Value?.ToString() ?? "") == "Cash";
            string txt = cash ? "ﬂ«‘" : "¬Ã·";
            Color fc = cash ? ColorTranslator.FromHtml("#1d4ed8") : ColorTranslator.FromHtml("#7c3aed");
            Color bc = cash ? ColorTranslator.FromHtml("#DBEAFE") : ColorTranslator.FromHtml("#EDE9FE");
            Color brd = cash ? ColorTranslator.FromHtml("#BFDBFE") : ColorTranslator.FromHtml("#DDD6FE");
            int pw = 64, ph = 28, px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2, py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
            using (var path = RoundPath(new Rectangle(px, py, pw, ph), ph / 2)) { g.FillPath(new SolidBrush(bc), path); g.DrawPath(new Pen(brd, 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(fc)) g.DrawString(txt, f, tb, new RectangleF(px, py, pw, ph), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            g.ResetClip();
        }

        private void PaintAmountCell(DataGridViewCellPaintingEventArgs e, Color bg, string col)
        {
            e.Handled = true; var g = e.Graphics; g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            decimal val = 0m; try { val = Convert.ToDecimal(e.Value); } catch { }
            Color fc = col == "IRemain" && val > 0 ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#0F172A");
            using (var f = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(fc))
                g.DrawString(val.ToString("N2", Inv) + " Ã", f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintDateCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; e.Graphics.FillRectangle(new SolidBrush(bg), e.CellBounds);
            string dateText = ""; DateTime dt2 = DateTime.MinValue;
            if (e.Value is DateTime dv) dt2 = dv; else if (e.Value != null) DateTime.TryParse(e.Value.ToString(), out dt2);
            if (dt2 != DateTime.MinValue) { if (dt2.Kind == DateTimeKind.Utc) dt2 = dt2.ToLocalTime(); else if (dt2.Kind == DateTimeKind.Unspecified) dt2 = DateTime.SpecifyKind(dt2, DateTimeKind.Local); dateText = dt2.ToString("yyyy/MM/dd  HH:mm"); }
            using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                e.Graphics.DrawString(dateText, f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private const int BtnH = 32, EyeW = 32, EditW = 58, PrintW = 58, DelW = 32, BtnGap = 5;
        private static int ActionsTotal => EyeW + BtnGap + EditW + BtnGap + PrintW + BtnGap + DelW;

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds); g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int btnY = e.CellBounds.Top + (e.CellBounds.Height - BtnH) / 2;
            int sx = e.CellBounds.Left + (e.CellBounds.Width - ActionsTotal) / 2;
            var eyeR = new Rectangle(sx, btnY, EyeW, BtnH);
            using (var path = RoundPath(eyeR, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F0FDF4")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#86EFAC"), 1f), path); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#16A34A"), 1.8f)) { int cx = eyeR.Left + eyeR.Width / 2, cy = eyeR.Top + eyeR.Height / 2; g.DrawArc(pen, cx - 7, cy - 5, 14, 10, 0, 180); g.DrawArc(pen, cx - 7, cy - 5, 14, 10, 180, 180); using (var br = new SolidBrush(ColorTranslator.FromHtml("#16A34A"))) g.FillEllipse(br, cx - 3, cy - 3, 6, 6); }
            var editR = new Rectangle(sx + EyeW + BtnGap, btnY, EditW, BtnH);
            using (var path = RoundPath(editR, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB"))) { var sz = g.MeasureString("œ›⁄…", f); g.DrawString("œ›⁄…", f, tb, editR.Left + (editR.Width - sz.Width) / 2f, editR.Top + (editR.Height - sz.Height) / 2f); }
            var printR = new Rectangle(sx + EyeW + BtnGap + EditW + BtnGap, btnY, PrintW, BtnH);
            using (var path = RoundPath(printR, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFF7ED")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FED7AA"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#EA580C"))) { var sz = g.MeasureString("ÿ»«⁄…", f); g.DrawString("ÿ»«⁄…", f, tb, printR.Left + (printR.Width - sz.Width) / 2f, printR.Top + (printR.Height - sz.Height) / 2f); }
            var delR = new Rectangle(sx + EyeW + BtnGap + EditW + BtnGap + PrintW + BtnGap, btnY, DelW, BtnH);
            using (var path2 = RoundPath(delR, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path2); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path2); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f)) { int dx = delR.Left + delR.Width / 2, dy = delR.Top + delR.Height / 2; g.DrawLine(pen, dx - 5, dy - 4, dx + 5, dy - 4); g.DrawLine(pen, dx - 2, dy - 6, dx + 2, dy - 6); g.DrawRectangle(pen, dx - 4, dy - 3, 8, 7); g.DrawLine(pen, dx - 1, dy - 1, dx - 1, dy + 3); g.DrawLine(pen, dx + 1, dy - 1, dx + 1, dy + 3); }
            g.ResetClip();
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvInvoices.Columns[e.ColumnIndex].Name != "IActions") return;
            var dto = dgvInvoices.Rows[e.RowIndex].DataBoundItem as SalesInvoiceDto; if (dto == null) return;
            var cell = dgvInvoices.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var mouse = dgvInvoices.PointToClient(Cursor.Position);
            int btnY = cell.Top + (cell.Height - BtnH) / 2, sx = cell.Left + (cell.Width - ActionsTotal) / 2;
            if (new Rectangle(sx, btnY, EyeW, BtnH).Contains(mouse)) ShowInvoiceDetails(dto);
            else if (new Rectangle(sx + EyeW + BtnGap, btnY, EditW, BtnH).Contains(mouse))
            { if (dto.Status == "Completed") { ShowErrorToast("«·›« Ê—… „ﬂ „·… »«·›⁄·"); return; } ShowPaymentPopup(dto); }
            else if (new Rectangle(sx + EyeW + BtnGap + EditW + BtnGap, btnY, PrintW, BtnH).Contains(mouse)) PrintInvoiceAsync(dto);
            else if (new Rectangle(sx + EyeW + BtnGap + EditW + BtnGap + PrintW + BtnGap, btnY, DelW, BtnH).Contains(mouse))
            {
                if (MessageBox.Show("Õ–› «·›« Ê—…ø", " √ﬂÌœ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                { try { _invoiceService.DeleteInvoice(dto.Id); LoadInvoices(); ShowSuccessToast(" „ «·Õ–›"); } catch (Exception ex) { ShowErrorToast(GetInner(ex)); } }
            }
        }

        private async void PrintInvoiceAsync(SalesInvoiceDto dto)
        {
            ShowSuccessToast("Ã«—Ú  Õ÷Ì— «·›« Ê—…...");
            try
            {
                byte[] pdfBytes = await Task.Run(() => _invoicePdf.GenerateInvoicePdf(dto));
                using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ ›« Ê—… PDF", Filter = "PDF|*.pdf", FileName = $"›« Ê—…_{dto.Id}_{(dto.CustomerName ?? "⁄„Ì·").Replace("/", "-")}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                {
                    if (sfd.ShowDialog() != DialogResult.OK) return;
                    File.WriteAllBytes(sfd.FileName, pdfBytes);
                    ShowSuccessToast($" „ Õ›Ÿ ›« Ê—… #{dto.Id}");
                    try { var psi = new System.Diagnostics.ProcessStartInfo { FileName = sfd.FileName, Verb = "print", UseShellExecute = true, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden }; System.Diagnostics.Process.Start(psi); } catch { }
                }
            }
            catch (Exception ex) { ShowErrorToast("›‘· ≈‰‘«¡ «·›« Ê—…: " + GetInner(ex)); }
        }

        private void ShowPaymentPopup(SalesInvoiceDto editDto)
        {
            var popup = CreatePopupForm(500, 320, "≈÷«›… œ›⁄…", "√÷› «·œ›⁄… «·ÃœÌœ… ··›« Ê—…");
            var body = popup.Tag as Panel; if (body == null) return;

            Guna2TextBox MkTxt(string ph) => new Guna2TextBox { Height = 42, Dock = DockStyle.Top, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#C7D2FE"), BorderThickness = 1, Font = new Font("Cairo", 10.5F), PlaceholderText = ph, PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8"), ForeColor = ColorTranslator.FromHtml("#0F172A"), TextAlign = HorizontalAlignment.Right };
            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            Label MkLbl(string t) => new Label { Text = t, Dock = DockStyle.Top, Height = 22, Font = new Font("Cairo", 9.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#1e3a6e"), TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent };

            var lblInfo = new Label { Text = $"›« Ê—… #{editDto.Id} ó {editDto.CustomerName}", Font = new Font("Cairo", 11F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#0F172A"), Dock = DockStyle.Top, Height = 32, TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };
            var lblRem = new Label { Text = $"«·„ »ﬁÌ: {editDto.Remaining.ToString("N2", Inv)} Ã", Font = new Font("Cairo", 10.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#EF4444"), Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };
            var fAmt = MkTxt("0.00");
            var fNote = MkTxt("„·«ÕŸ« ...");
            var errAmt = MakeErrLbl();

            body.Controls.Add(Sp(8)); body.Controls.Add(fNote); body.Controls.Add(MkLbl("„·«ÕŸ« "));
            body.Controls.Add(Sp(6)); body.Controls.Add(errAmt); body.Controls.Add(fAmt); body.Controls.Add(MkLbl("«·„»·€ «·„œ›Ê⁄ *"));
            body.Controls.Add(Sp(4)); body.Controls.Add(lblRem); body.Controls.Add(lblInfo);

            var footer = popup.Controls.Find("footer", true).FirstOrDefault() as Panel;
            var btnP = MakeSaveBtn(" ”ÃÌ· «·œ›⁄…");
            btnP.Click += async (sndr, ev) =>
            {
                errAmt.Visible = false; errAmt.Height = 0;
                if (!decimal.TryParse(fAmt.Text, NumberStyles.Any, Inv, out decimal amt) || amt <= 0)
                { errAmt.Text = "ï √œŒ· „»·€« ’ÕÌÕ«"; errAmt.Visible = true; errAmt.Height = 18; return; }
                btnP.Enabled = false; btnP.Text = "Ã«—Ú «·Õ›Ÿ...";
                try { await Task.Run(() => _invoiceService.AddPayment(editDto.Id, amt, fNote.Text.Trim())); LoadInvoices(); popup.Close(); ShowSuccessToast(" „  ”ÃÌ· «·œ›⁄…"); }
                catch (Exception ex) { ShowErrorToast("›‘·: " + GetInner(ex)); }
                finally { btnP.Enabled = true; btnP.Text = " ”ÃÌ· «·œ›⁄…"; }
            };
            footer?.Controls.Add(btnP);
            popup.ShowDialog(this);
        }

        private void ShowInvoicePopup()
        {
            var customers = new List<CustomerDto>();
            var products = new List<ProductDto>();
            try { customers = _customerService.GetAll()?.ToList() ?? new List<CustomerDto>(); } catch { }
            try { products = _productService.GetAll()?.ToList() ?? new List<ProductDto>(); } catch { }

            var popup = CreatePopupForm(520, 700, "›« Ê—… »Ì⁄ ÃœÌœ…", "«Œ — «·⁄„Ì· Ê√÷› «·„‰ Ã« ");
            var body = popup.Tag as Panel; if (body == null) return;

            Panel MkCboField(string placeholder, out ComboBox cboOut)
            {
                var cbo = new ComboBox { Height = 42, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 11F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 34, RightToLeft = RightToLeft.No, Dock = DockStyle.Top };
                cbo.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#EEF2FF") : Color.White), de.Bounds); using (var f2 = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#1e3a6e") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(cbo.GetItemText(cbo.Items[de.Index]), f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };
                var ov = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent };
                ov.Paint += (s2, pe2) =>
                {
                    var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.FillPath(brs, path2);
                    using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, ov.Width - 1, ov.Height - 1), 8)) g.DrawPath(pen2, path2);
                    int ax = 18, ay = ov.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 2f)) { g.DrawLine(ap, ax + 5, ay - 3, ax, ay + 3); g.DrawLine(ap, ax, ay + 3, ax - 5, ay - 3); }
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
            Label MkLbl(string t) => new Label { Text = t, Dock = DockStyle.Top, Height = 22, Font = new Font("Cairo", 9.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#1e3a6e"), TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };

            var errC = MakeErrLbl();
            var cboCPanel = MkCboField("«Œ — ⁄„Ì·", out var cboC);
            var cl = new List<CustomerDto>(customers); cl.Insert(0, new CustomerDto { Id = 0, Name = "«Œ — ⁄„Ì·" });
            cboC.DisplayMember = "Name"; cboC.ValueMember = "Id"; cboC.DataSource = null; cboC.DataSource = cl;
            cboC.SelectedIndexChanged += (s, e) => cboCPanel.Invalidate();

            var rbCash = new RadioButton { Text = "ﬂ«‘", Font = new Font("Cairo", 11F), ForeColor = ColorTranslator.FromHtml("#374151"), AutoSize = true, Checked = true, RightToLeft = RightToLeft.Yes };
            var rbCred = new RadioButton { Text = "¬Ã· /  ﬁ”Ìÿ", Font = new Font("Cairo", 11F), ForeColor = ColorTranslator.FromHtml("#374151"), AutoSize = true, RightToLeft = RightToLeft.Yes };
            var pnlRB = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.Transparent };
            pnlRB.Controls.Add(rbCred); pnlRB.Controls.Add(rbCash);
            pnlRB.Resize += (s, e) => { rbCash.Location = new Point(pnlRB.Width - rbCash.Width - 4, 6); rbCred.Location = new Point(rbCash.Left - rbCred.Width - 20, 6); };
            var lblPaid = MkLbl("«·„œ›Ê⁄ «·¬‰"); lblPaid.Visible = false;
            var fPaid = new Guna2TextBox { Height = 40, Dock = DockStyle.Top, BorderRadius = 8, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#C7D2FE"), BorderThickness = 1, Font = new Font("Cairo", 10.5F), PlaceholderText = "0.00", TextAlign = HorizontalAlignment.Right, Visible = false };
            rbCred.CheckedChanged += (s, e) => { lblPaid.Visible = rbCred.Checked; fPaid.Visible = rbCred.Checked; };

            var lblTotalShow = new Label { Text = "≈Ã„«·Ì «·›« Ê—…: 0.00 Ã", Font = new Font("Cairo", 12F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#1a2f5e"), Dock = DockStyle.Top, Height = 32, TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent };

            const int ROW_H = 140;
            var itemsContainer = new Panel { Dock = DockStyle.Top, BackColor = Color.Transparent, Height = ROW_H };
            var itemRows = new List<(ComboBox cbo, Guna2TextBox txtQty, Guna2TextBox txtPrice)>();

            void RecalcTotal()
            {
                decimal t = 0;
                foreach (var r in itemRows)
                { if (int.TryParse(r.txtQty.Text, out int q) && decimal.TryParse(r.txtPrice.Text, NumberStyles.Any, Inv, out decimal sp)) t += q * sp; }
                lblTotalShow.Text = $"≈Ã„«·Ì «·›« Ê—…: {t.ToString("N2", Inv)} Ã";
            }

            void AddRow()
            {
                var pl = new List<ProductDto>(products); pl.Insert(0, new ProductDto { Id = 0, Name = "«Œ — „‰ Ã" });
                var cboInner = new ComboBox { Height = 36, FlatStyle = FlatStyle.Flat, Font = new Font("Cairo", 10.5F), BackColor = Color.White, ForeColor = Color.Transparent, DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 32, RightToLeft = RightToLeft.No };
                cboInner.DrawItem += (s2, de) => { if (de.Index < 0) return; bool hot = (de.State & DrawItemState.Selected) != 0; de.Graphics.FillRectangle(new SolidBrush(hot ? ColorTranslator.FromHtml("#EEF2FF") : Color.White), de.Bounds); using (var f2 = new Font("Cairo", 10F, hot ? FontStyle.Bold : FontStyle.Regular)) using (var b2 = new SolidBrush(hot ? ColorTranslator.FromHtml("#1e3a6e") : ColorTranslator.FromHtml("#111827"))) de.Graphics.DrawString(cboInner.GetItemText(cboInner.Items[de.Index]), f2, b2, de.Bounds, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center }); };
                cboInner.DisplayMember = "Name"; cboInner.ValueMember = "Id"; cboInner.DataSource = null; cboInner.DataSource = new List<ProductDto>(pl);
                var cboOv = new Panel { BackColor = Color.Transparent };
                cboOv.Paint += (s2, pe2) =>
                {
                    var g2 = pe2.Graphics; g2.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brs = new SolidBrush(Color.White)) using (var path2 = RoundPath(new Rectangle(0, 0, cboOv.Width - 1, cboOv.Height - 1), 8)) g2.FillPath(brs, path2);
                    using (var pen2 = new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f)) using (var path2 = RoundPath(new Rectangle(0, 0, cboOv.Width - 1, cboOv.Height - 1), 8)) g2.DrawPath(pen2, path2);
                    int ay = cboOv.Height / 2; using (var ap = new Pen(ColorTranslator.FromHtml("#64748B"), 1.8f)) { g2.DrawLine(ap, 22, ay - 3, 17, ay + 3); g2.DrawLine(ap, 17, ay + 3, 12, ay - 3); }
                    string selTxt2 = cboInner.SelectedIndex >= 0 ? cboInner.GetItemText(cboInner.SelectedItem) : "«Œ — „‰ Ã";
                    bool isPlh2 = cboInner.SelectedIndex < 0 || selTxt2 == "«Œ — „‰ Ã";
                    using (var f2 = new Font("Cairo", 10.5F)) using (var b2 = new SolidBrush(isPlh2 ? ColorTranslator.FromHtml("#94A3B8") : ColorTranslator.FromHtml("#0F172A")))
                        g2.DrawString(selTxt2, f2, b2, new RectangleF(32, 0, cboOv.Width - 44, cboOv.Height), new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
                };
                cboInner.SetBounds(0, 0, 400, 36); cboOv.Controls.Add(cboInner);
                cboOv.Resize += (s2, e2) => cboInner.SetBounds(0, 0, cboOv.Width, cboOv.Height);
                cboInner.SelectedIndexChanged += (s2, e2) => cboOv.Invalidate();

                Guna2TextBox MkNum(string ph) { var t = new Guna2TextBox { Height = 34, BorderRadius = 7, FillColor = Color.White, BorderColor = ColorTranslator.FromHtml("#C7D2FE"), BorderThickness = 1, Font = new Font("Cairo", 10.5F, FontStyle.Bold), PlaceholderText = ph, TextAlign = HorizontalAlignment.Center, ForeColor = ColorTranslator.FromHtml("#0F172A"), PlaceholderForeColor = ColorTranslator.FromHtml("#94A3B8") }; t.FocusedState.BorderColor = ColorTranslator.FromHtml("#4E73DF"); t.FocusedState.FillColor = ColorTranslator.FromHtml("#F5F8FF"); return t; }

                var txtQty = MkNum("«·ﬂ„Ì… (ﬁÿ⁄…)");
                var txtPrice = MkNum("”⁄— «·»Ì⁄");
                var lblAvail = new Label { Font = new Font("Cairo", 9F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#059669"), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, RightToLeft = RightToLeft.No, Text = "", AutoSize = false, Height = 18 };

                void UpdateAvail()
                {
                    int pid = 0; try { pid = Convert.ToInt32(cboInner.SelectedValue); } catch { }
                    if (pid <= 0) { lblAvail.Text = ""; return; }
                    try
                    {
                        int avail = _invoiceService.GetWarehouseProductBalance(pid);
                        int.TryParse(txtQty.Text, out int req);
                        if (avail <= 0) { lblAvail.Text = "·« ÌÊÃœ ›Ì «·„Œ“‰"; lblAvail.ForeColor = ColorTranslator.FromHtml("#EF4444"); }
                        else if (req > avail) { lblAvail.Text = $"Ì Ã«Ê“ «·„ «Õ! «·„ «Õ: {avail} ﬁÿ⁄…"; lblAvail.ForeColor = ColorTranslator.FromHtml("#EF4444"); }
                        else { lblAvail.Text = $"„ «Õ ›Ì «·„Œ“‰: {avail} ﬁÿ⁄…"; lblAvail.ForeColor = ColorTranslator.FromHtml("#059669"); }
                    }
                    catch { lblAvail.Text = ""; }
                }

                var btnDel = new Panel { Size = new Size(28, 28), BackColor = Color.Transparent, Cursor = Cursors.Hand };
                btnDel.Paint += (sndr, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    if (itemRows.Count <= 1) return;
                    using (var path = RoundPath(new Rectangle(0, 0, 27, 27), 7)) pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path);
                    using (var p2 = new Pen(ColorTranslator.FromHtml("#EF4444"), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { pe.Graphics.DrawLine(p2, 8, 8, 19, 19); pe.Graphics.DrawLine(p2, 19, 8, 8, 19); }
                };
                btnDel.Click += (sndr, ev) =>
                {
                    if (itemRows.Count <= 1) { ShowErrorToast("ÌÃ» √‰  Õ ÊÌ «·›« Ê—… ⁄·Ï „‰ Ã Ê«Õœ ⁄·Ï «·√ﬁ·"); return; }
                    itemRows.RemoveAll(r => r.cbo == cboInner);
                    itemsContainer.Controls.Remove(btnDel.Parent);
                    RecalcTotal();
                    itemsContainer.Height = Math.Max(ROW_H, itemRows.Count * ROW_H);
                };

                var rowCard = new Panel { Dock = DockStyle.Top, Height = ROW_H, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 4) };
                var inner = new Panel { BackColor = Color.White };
                inner.Paint += (sndr, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = RoundPath(new Rectangle(0, 0, inner.Width - 1, inner.Height - 1), 10)) { pe.Graphics.FillPath(new SolidBrush(Color.White), path); pe.Graphics.DrawPath(new Pen(ColorTranslator.FromHtml("#CBD5E1"), 1.5f), path); } };
                inner.Controls.AddRange(new Control[] { cboOv, txtQty, txtPrice, lblAvail, btnDel });
                rowCard.Controls.Add(inner);
                rowCard.Resize += (sndr, ev) =>
                {
                    int iw = rowCard.Width, pad = 8;
                    inner.SetBounds(pad, 4, iw - pad * 2, ROW_H - 8);
                    int w2 = inner.Width, gap = 6, delW2 = 30;
                    btnDel.SetBounds(4, 6, 26, 26);
                    cboOv.SetBounds(delW2 + 6, 8, w2 - delW2 - 10, 36);
                    int halfW = (w2 - gap) / 2;
                    txtQty.SetBounds(w2 - halfW, 52, halfW, 34);
                    txtPrice.SetBounds(0, 52, halfW, 34);
                    lblAvail.SetBounds(0, 94, w2, 18);
                };

                cboInner.SelectedIndexChanged += (s2, e2) =>
                {
                    int pid = 0; try { pid = Convert.ToInt32(cboInner.SelectedValue); } catch { }
                    if (pid > 0)
                    {
                        bool dup = itemRows.Where(r => r.cbo != cboInner).Any(r => { try { return Convert.ToInt32(r.cbo.SelectedValue ?? 0) == pid; } catch { return false; } });
                        if (dup) { ShowErrorToast("Â–« «·„‰ Ã „Œ «— »«·›⁄·"); BeginInvoke(new Action(() => { try { cboInner.SelectedIndex = 0; } catch { } cboOv.Invalidate(); })); return; }
                        var prod = products.FirstOrDefault(p => p.Id == pid);
                        if (prod != null && prod.SalePrice > 0 && string.IsNullOrWhiteSpace(txtPrice.Text))
                            txtPrice.Text = prod.SalePrice.ToString("N2", Inv);
                    }
                    UpdateAvail();
                };
                txtQty.TextChanged += (s2, e2) => { RecalcTotal(); UpdateAvail(); };
                txtPrice.TextChanged += (s2, e2) => RecalcTotal();

                itemRows.Add((cboInner, txtQty, txtPrice));
                itemsContainer.Controls.Add(rowCard);
                itemsContainer.Height = itemRows.Count * ROW_H;
            }

            AddRow();

            var btnAddRow = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            btnAddRow.Paint += (sndr, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; var rc = new Rectangle(0, 0, btnAddRow.Width - 1, btnAddRow.Height - 1); using (var path = RoundPath(rc, 8)) { pe.Graphics.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); pe.Graphics.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); } using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB"))) pe.Graphics.DrawString("≈÷«›… „‰ Ã ¬Œ— +", f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            btnAddRow.Click += (sndr, ev) => { AddRow(); popup.Height = Math.Min(Screen.FromControl(this).WorkingArea.Height - 40, popup.Height + ROW_H); };

            body.Controls.Add(Sp(4)); body.Controls.Add(fPaid); body.Controls.Add(lblPaid);
            body.Controls.Add(Sp(4)); body.Controls.Add(pnlRB); body.Controls.Add(MkLbl("‰Ê⁄ «·œ›⁄"));
            body.Controls.Add(Sp(4)); body.Controls.Add(lblTotalShow);
            body.Controls.Add(Sp(4)); body.Controls.Add(btnAddRow);
            body.Controls.Add(itemsContainer); body.Controls.Add(MkLbl("«·„‰ Ã«  *"));
            body.Controls.Add(Sp(6)); body.Controls.Add(Div()); body.Controls.Add(Sp(6));
            body.Controls.Add(errC); body.Controls.Add(cboCPanel); body.Controls.Add(MkLbl("«·⁄„Ì· *"));

            var footer = popup.Controls.Find("footer", true).FirstOrDefault() as Panel;
            var btnSave = MakeSaveBtn("Õ›Ÿ «·›« Ê—…");
            btnSave.Click += async (sndr, ev) =>
            {
                errC.Visible = false; errC.Height = 0;
                int cid = 0; try { cid = Convert.ToInt32(cboC.SelectedValue); } catch { }
                if (cid == 0) { errC.Text = "ï «Œ — ⁄„Ì·"; errC.Visible = true; errC.Height = 18; return; }
                if (itemRows.Count == 0) { ShowErrorToast("√÷› „‰ Ã« Ê«Õœ« ⁄·Ï «·√ﬁ·"); return; }

                var items = new List<SalesInvoiceItemDto>(); bool valid = true;
                foreach (var r in itemRows)
                {
                    int pid = 0; try { pid = Convert.ToInt32(r.cbo.SelectedValue); } catch { }
                    if (pid == 0) { valid = false; continue; }
                    if (items.Any(x => x.ProductId == pid)) { ShowErrorToast($"«·„‰ Ã '{products.FirstOrDefault(p => p.Id == pid)?.Name ?? $"#{pid}"}' „ﬂ——"); return; }
                    if (!int.TryParse(r.txtQty.Text, out int q) || q <= 0) { valid = false; r.txtQty.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }
                    if (!decimal.TryParse(r.txtPrice.Text, NumberStyles.Any, Inv, out decimal sp) || sp <= 0) { valid = false; r.txtPrice.BorderColor = ColorTranslator.FromHtml("#EF4444"); continue; }
                    items.Add(new SalesInvoiceItemDto { ProductId = pid, ProductName = products.FirstOrDefault(p => p.Id == pid)?.Name ?? "", Quantity = q, SalePrice = sp, BoxesPerCarton = 1 });
                }
                if (!valid || items.Count == 0) { ShowErrorToast(" Õﬁﬁ „‰ »Ì«‰«  «·„‰ Ã« "); return; }

                string payType = rbCash.Checked ? "Cash" : "Credit";
                decimal paid = 0m;
                if (rbCred.Checked && !decimal.TryParse(fPaid.Text, NumberStyles.Any, Inv, out paid)) { ShowErrorToast("√œŒ· «·„»·€ «·„œ›Ê⁄"); return; }

                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                try
                {
                    var dto2 = new SalesInvoiceDto { CustomerId = cid, PaymentType = payType, PaidAmount = paid, Items = items };
                    await Task.Run(() => _invoiceService.SaveInvoice(dto2));
                    LoadInvoices(); popup.Close(); ShowSuccessToast(" „ Õ›Ÿ «·›« Ê—… ÊŒ’„ «·ﬂ„Ì… „‰ «·„Œ“‰");
                }
                catch (Exception ex) { ShowErrorToast(GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = "Õ›Ÿ «·›« Ê—…"; }
            };
            footer?.Controls.Add(btnSave);
            popup.Shown += (sndr, ev) => cboC.SelectedIndex = 0;
            popup.ShowDialog(this);
        }


        // ???????????????????????????????????????????????????????????????????????
        //  FAST + SMOOTH + NO LAG VERSION
        // ???????????????????????????????????????????????????????????????????????

        private void ShowInvoiceDetails(SalesInvoiceDto dto)
        {
            var items2 = dto.Items ?? new List<SalesInvoiceItemDto>();

            decimal grandTotal = items2.Sum(i => i.Quantity * i.SalePrice);

            decimal remaining = dto.TotalAmount - dto.PaidAmount;

            bool isCompleted = dto.Status == "Completed";

            bool isCash = dto.PaymentType == "Cash";

            DateTime dtLocal =
                dto.CreatedAt.Kind == DateTimeKind.Utc
                ? dto.CreatedAt.ToLocalTime()
                : DateTime.SpecifyKind(dto.CreatedAt, DateTimeKind.Local);

            string dateStr = dtLocal.ToString("yyyy/MM/dd  HH:mm");

            List<InvoicePaymentDto> payments =
                new List<InvoicePaymentDto>();

            try
            {
                payments =
                    _invoiceService.GetInvoicePayments(dto.Id)
                    ?? new List<InvoicePaymentDto>();
            }
            catch { }

            // ??????????????????????????????????????????????????????????????
            // SIZES
            // ??????????????????????????????????????????????????????????????

            const int colHeaderH = 44;

            const int rowH2 = 48;

            const int titleBarH = 52;

            const int pad = 16;

            int itemsGridH =
                colHeaderH + items2.Count * rowH2 + 2;

            int itemsPanelH =
                titleBarH + itemsGridH;

            int payGridH =
                payments.Count > 0
                ? colHeaderH + payments.Count * rowH2 + 2
                : 0;

            int payPanelH =
                payments.Count > 0
                ? titleBarH + payGridH
                : 0;

            int contentH =
                130 + 8 +
                100 + 8 +
                itemsPanelH +
                (payPanelH > 0 ? 8 + payPanelH : 0) +
                pad * 2;

            var screen2 =
                Screen.FromControl(this).WorkingArea;

            int formH =
                Math.Min(contentH + 40, screen2.Height - 40);

            int formW = 920;

            // ??????????????????????????????????????????????????????????????
            // FORM
            // ??????????????????????????????????????????????????????????????

            var dlg = new Form
            {
                Text = " ›«’Ì· «·›« Ê—…",

                Size = new Size(formW, formH),

                MinimumSize = new Size(700, 500),

                StartPosition = FormStartPosition.CenterParent,

                Font = new Font("Cairo", 10F),

                RightToLeft = RightToLeft.Yes,

                RightToLeftLayout = true,

                BackColor =
                    ColorTranslator.FromHtml("#EEF0F5"),

                FormBorderStyle = FormBorderStyle.Sizable,

                ShowInTaskbar = false
            };

            // DOUBLE BUFFER
            try
            {
                typeof(Control)
                    .GetProperty(
                        "DoubleBuffered",
                        BindingFlags.NonPublic |
                        BindingFlags.Instance)
                    ?.SetValue(dlg, true);
            }
            catch { }

            // ??????????????????????????????????????????????????????????????
            // SCROLL PANEL
            // ??????????????????????????????????????????????????????????????

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,

                AutoScroll = true,

                BackColor = Color.Transparent
            };

            dlg.Controls.Add(scrollPanel);

            var innerPanel = new Panel
            {
                BackColor = Color.Transparent,

                AutoSize = false
            };

            scrollPanel.Controls.Add(innerPanel);

            int y = pad;

            // ??????????????????????????????????????????????????????????????
            // HEADER
            // ??????????????????????????????????????????????????????????????

            var bannerPanel = new Panel
            {
                Location = new Point(pad, y),

                Height = 130,

                BackColor =
                    ColorTranslator.FromHtml("#1565c0")
            };

            var lblInvoice = new Label
            {
                AutoSize = false,

                Text = $"›« Ê—… #{dto.Id:D6}",

                Font = new Font("Cairo", 20F, FontStyle.Bold),

                ForeColor = Color.White,

                Size = new Size(350, 40),

                TextAlign = ContentAlignment.MiddleRight,

                Location = new Point(520, 12)
            };

            var lblDate = new Label
            {
                AutoSize = false,

                Text = $"«· «—ÌŒ: {dateStr}",

                Font = new Font("Cairo", 10F),

                ForeColor = Color.White,

                Size = new Size(320, 30),

                TextAlign = ContentAlignment.MiddleRight,

                Location = new Point(550, 52)
            };

            var lblCustomer = new Label
            {
                AutoSize = false,

                Text = $"«·⁄„Ì·: {dto.CustomerName}",

                Font = new Font("Cairo", 10F, FontStyle.Bold),

                ForeColor = Color.White,

                Size = new Size(320, 30),

                TextAlign = ContentAlignment.MiddleRight,

                Location = new Point(550, 76)
            };

            var btnClose2 = new Guna2Button
            {
                Text = "≈€·«ﬁ",

                Size = new Size(120, 36),

                Location = new Point(16, 45),

                BorderRadius = 10,

                FillColor =
                    Color.FromArgb(45, 255, 255, 255),

                ForeColor = Color.White,

                Font = new Font("Cairo", 10F, FontStyle.Bold)
            };

            btnClose2.Click += (s, e) => dlg.Close();

            bannerPanel.Controls.Add(lblInvoice);
            bannerPanel.Controls.Add(lblDate);
            bannerPanel.Controls.Add(lblCustomer);
            bannerPanel.Controls.Add(btnClose2);

            innerPanel.Controls.Add(bannerPanel);

            y += 130 + 10;

            // ??????????????????????????????????????????????????????????????
            // METRICS
            // ??????????????????????????????????????????????????????????????

            var metricsPanel = new FlowLayoutPanel
            {
                Location = new Point(pad, y),

                Height = 100,

                FlowDirection = FlowDirection.RightToLeft,

                WrapContents = false,

                BackColor = Color.Transparent
            };

            Panel MakeCard(
                string title,
                string value,
                Color color)
            {
                var card = new Panel
                {
                    Width = 200,

                    Height = 90,

                    BackColor = Color.White,

                    Margin = new Padding(5)
                };

                var top = new Panel
                {
                    Dock = DockStyle.Top,

                    Height = 5,

                    BackColor = color
                };

                var lblVal = new Label
                {
                    Dock = DockStyle.Top,

                    Height = 42,

                    Text = value,

                    Font = new Font("Cairo", 14F, FontStyle.Bold),

                    ForeColor = color,

                    TextAlign = ContentAlignment.MiddleCenter
                };

                var lblTitle2 = new Label
                {
                    Dock = DockStyle.Fill,

                    Text = title,

                    Font = new Font("Cairo", 9.5F),

                    ForeColor =
                        ColorTranslator.FromHtml("#64748B"),

                    TextAlign = ContentAlignment.TopCenter
                };

                card.Controls.Add(lblTitle2);
                card.Controls.Add(lblVal);
                card.Controls.Add(top);

                return card;
            }
            metricsPanel.Controls.Add(
                MakeCard(
                    "«·≈Ã„«·Ì «·ﬂ·Ì",
                    grandTotal.ToString("N2", Inv) + " Ã",
                    ColorTranslator.FromHtml("#1565c0")));

            metricsPanel.Controls.Add(
                MakeCard(
                    "«·„ »ﬁÌ",
                    remaining.ToString("N2", Inv) + " Ã",
                    remaining > 0
                        ? ColorTranslator.FromHtml("#DC2626")
                        : ColorTranslator.FromHtml("#16A34A")));

            metricsPanel.Controls.Add(
                MakeCard(
                    "«·„œ›Ê⁄",
                    dto.PaidAmount.ToString("N2", Inv) + " Ã",
                    ColorTranslator.FromHtml("#16A34A")));

            metricsPanel.Controls.Add(
                MakeCard(
                    "⁄œœ «·√’‰«›",
                    items2.Count.ToString(),
                    ColorTranslator.FromHtml("#1a2f5e")));

            innerPanel.Controls.Add(metricsPanel);

            y += 110;

            // ??????????????????????????????????????????????????????????????
            // GRID HELPER
            // ??????????????????????????????????????????????????????????????

            Panel MakeGridPanel2(
                string titleTxt,
                string titleColor2,
                int gridH2,
                Action<Guna2DataGridView> buildCols2,
                out Guna2DataGridView dgvOut2)
            {
                int panelH = titleBarH + gridH2;

                var pnl = new Panel
                {
                    Height = panelH,

                    BackColor = Color.White
                };

                var title = new Label
                {
                    Text = titleTxt,

                    Dock = DockStyle.Top,

                    Height = titleBarH,

                    Font = new Font("Cairo", 13F, FontStyle.Bold),

                    ForeColor =
                        ColorTranslator.FromHtml(titleColor2),

                    TextAlign = ContentAlignment.MiddleCenter
                };

                var dgv = new Guna2DataGridView
                {
                    Dock = DockStyle.Fill,

                    ReadOnly = true,

                    AllowUserToAddRows = false,

                    AllowUserToDeleteRows = false,

                    AllowUserToResizeRows = false,

                    AllowUserToResizeColumns = false,

                    AutoGenerateColumns = false,

                    BackgroundColor = Color.White,

                    BorderStyle = BorderStyle.None,

                    RowHeadersVisible = false,

                    ScrollBars = ScrollBars.None,

                    SelectionMode =
                        DataGridViewSelectionMode.FullRowSelect,

                    AutoSizeColumnsMode =
                        DataGridViewAutoSizeColumnsMode.None,

                    ColumnHeadersHeight = colHeaderH
                };

                try
                {
                    typeof(DataGridView)
                        .GetProperty(
                            "DoubleBuffered",
                            BindingFlags.NonPublic |
                            BindingFlags.Instance)
                        ?.SetValue(dgv, true);
                }
                catch { }

                dgv.RowTemplate.Height = rowH2;

                dgv.ColumnHeadersDefaultCellStyle.BackColor =
                    ColorTranslator.FromHtml("#1e3a6e");

                dgv.ColumnHeadersDefaultCellStyle.ForeColor =
                    Color.White;

                dgv.ColumnHeadersDefaultCellStyle.Font =
                    new Font("Cairo", 11F, FontStyle.Bold);

                dgv.EnableHeadersVisualStyles = false;

                dgv.DefaultCellStyle.Font =
                    new Font("Cairo", 10.5F);

                dgv.DefaultCellStyle.SelectionBackColor =
                    ColorTranslator.FromHtml("#EEF2FF");

                dgv.DefaultCellStyle.SelectionForeColor =
                    Color.Black;

                dgv.AlternatingRowsDefaultCellStyle.BackColor =
                    ColorTranslator.FromHtml("#F8FAFF");

                buildCols2(dgv);

                pnl.Controls.Add(dgv);
                pnl.Controls.Add(title);

                dgvOut2 = dgv;

                return pnl;
            }

            // ??????????????????????????????????????????????????????????????
            // ITEMS GRID
            // ??????????????????????????????????????????????????????????????

            Guna2DataGridView dgvItems2;

            var pnlItems2 = MakeGridPanel2(
                $"«·„‰ Ã«  ({items2.Count} ’‰›)",
                "#1a2f5e",
                itemsGridH,

                dgv =>
                {
                    dgv.Columns.Add(
                        new DataGridViewTextBoxColumn
                        {
                            HeaderText = "#",
                            Width = 60
                        });

                    dgv.Columns.Add(
                        new DataGridViewTextBoxColumn
                        {
                            HeaderText = "«·„‰ Ã",
                            Width = 320
                        });

                    dgv.Columns.Add(
                        new DataGridViewTextBoxColumn
                        {
                            HeaderText = "«·ﬂ„Ì…",
                            Width = 160
                        });

                    dgv.Columns.Add(
                        new DataGridViewTextBoxColumn
                        {
                            HeaderText = "”⁄— «·ÊÕœ…",
                            Width = 180
                        });

                    dgv.Columns.Add(
                        new DataGridViewTextBoxColumn
                        {
                            HeaderText = "«·≈Ã„«·Ì",
                            Width = 180
                        });
                },

                out dgvItems2);

            pnlItems2.Location = new Point(pad, y);

            innerPanel.Controls.Add(pnlItems2);

            for (int ri = 0; ri < items2.Count; ri++)
            {
                var item = items2[ri];

                decimal rt =
                    item.Quantity * item.SalePrice;

                dgvItems2.Rows.Add(
                    (ri + 1).ToString(),
                    item.ProductName,
                    $"{item.Quantity} ﬁÿ⁄…",
                    item.SalePrice.ToString("N2", Inv) + " Ã",
                    rt.ToString("N2", Inv) + " Ã");
            }

            y += itemsPanelH + 8;

            // ??????????????????????????????????????????????????????????????
            // PAYMENTS GRID
            // ??????????????????????????????????????????????????????????????
            // ??????????????????????????????????????????????????????????????
            // PAYMENTS GRID
            // ??????????????????????????????????????????????????????????????

            Panel pnlPay2 = null;

            if (payments.Count > 0)
            {
                Guna2DataGridView dgvPay2;

                pnlPay2 = MakeGridPanel2(
                    $"”Ã· «·œ›⁄«  ({payments.Count} œ›⁄…)",
                    "#059669",
                    payGridH,

                    dgv =>
                    {
                        dgv.Columns.Add(
                            new DataGridViewTextBoxColumn
                            {
                                HeaderText = "#",
                                Width = 60
                            });

                        dgv.Columns.Add(
                            new DataGridViewTextBoxColumn
                            {
                                HeaderText = "«·„»·€",
                                Width = 180
                            });

                        dgv.Columns.Add(
                            new DataGridViewTextBoxColumn
                            {
                                HeaderText = "«· «—ÌŒ",
                                Width = 250
                            });

                        dgv.Columns.Add(
                            new DataGridViewTextBoxColumn
                            {
                                HeaderText = "«·≈Ã„«·Ì",
                                Width = 180
                            });

                        dgv.Columns.Add(
                            new DataGridViewTextBoxColumn
                            {
                                HeaderText = "„·«ÕŸ« ",
                                Width = 220
                            });

                        //  ‰”Ìﬁ ≈÷«›Ì
                        dgv.Columns[1].DefaultCellStyle.ForeColor =
                            ColorTranslator.FromHtml("#059669");

                        dgv.Columns[1].DefaultCellStyle.Font =
                            new Font("Cairo", 10.5F, FontStyle.Bold);

                        dgv.Columns[3].DefaultCellStyle.ForeColor =
                            ColorTranslator.FromHtml("#1565c0");
                    },

                    out dgvPay2);

                pnlPay2.Location =
                    new Point(pad, y);

                innerPanel.Controls.Add(pnlPay2);

                decimal runTot = 0;

                int pn = 0;

                foreach (var pay in payments)
                {
                    pn++;

                    runTot += pay.Amount;

                    dgvPay2.Rows.Add(
                        pn.ToString(),
                        pay.Amount.ToString("N2", Inv) + " Ã",
                        pay.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                        runTot.ToString("N2", Inv) + " Ã",
                        string.IsNullOrWhiteSpace(pay.Notes)
                            ? "œ›⁄…"
                            : pay.Notes);
                }

                y += payPanelH + 8;
            }

            // ??????????????????????????????????????????????????????????????
            // WIDTHS
            // ??????????????????????????????????????????????????????????????

            innerPanel.Height = y + pad;

            void AdjustWidths2(int clientW)
            {
                innerPanel.Width = clientW;

                int innerW = clientW - pad * 2;

                bannerPanel.Width = innerW;

                metricsPanel.Width = innerW;

                pnlItems2.Width = innerW;

                // «·Õ· Â‰«
                if (pnlPay2 != null)
                    pnlPay2.Width = innerW;
            }

            scrollPanel.SizeChanged +=
                (s, e) =>
                AdjustWidths2(scrollPanel.ClientSize.Width);

            AdjustWidths2(formW - 20);

            // ESC
            dlg.KeyPreview = true;

            dlg.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    dlg.Close();
            };

            dlg.ShowDialog(this);
        }
        private Form CreatePopupForm(int w, int h, string title, string sub)
        {
            var sc = Screen.FromControl(this).WorkingArea;
            var overlay = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = sc.Location, Size = sc.Size, BackColor = Color.Black, Opacity = 0.55, ShowInTaskbar = false, TopMost = true };
            overlay.Show(this);
            var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Size = new Size(w, h), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.No, RightToLeftLayout = false };
            pf.Location = new Point(sc.Left + (sc.Width - w) / 2, sc.Top + (sc.Height - h) / 2);
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 40, 40, 180, 90); rgn.AddArc(w - 40, 0, 40, 40, 270, 90); rgn.AddArc(w - 40, h - 40, 40, 40, 0, 90); rgn.AddArc(0, h - 40, 40, 40, 90, 90); rgn.CloseFigure(); pf.Region = new Region(rgn); }
            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();
            var popup2 = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White };
            popup2.ShadowDecoration.Enabled = true; popup2.ShadowDecoration.Depth = 30; popup2.ShadowDecoration.Color = Color.FromArgb(60, 0, 0, 0);
            pf.Controls.Add(popup2);
            var head = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            head.Paint += (sndr, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, head.Width, head.Height), ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal)) g.FillRectangle(br, head.ClientRectangle);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) for (int x = 8; x < head.Width; x += 20) for (int y = 6; y < head.Height; y += 20) g.FillEllipse(db, x, y, 2, 2);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White)) { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb2, head.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) { var ssz = g.MeasureString(sub, sf3); g.DrawString(sub, sf3, sb3, head.Width - ssz.Width - 50, 52); }
            };
            var btnX = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255); btnX.Click += (s, e) => pf.Close();
            head.Controls.Add(btnX); head.Layout += (s, e) => btnX.Location = new Point(18, 18);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Padding = new Padding(20, 8, 20, 4), Name = "body" };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14), Name = "footer" };
            footer.Paint += (s, pe) => { using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal)) pe.Graphics.FillRectangle(br, 0, 1, footer.Width, 2); };
            popup2.Controls.Add(body); popup2.Controls.Add(footer); popup2.Controls.Add(head);
            pf.Tag = body; return pf;
        }

        private Label MakeErrLbl() => new Label { Dock = DockStyle.Top, Height = 0, Font = new Font("Cairo", 9F), ForeColor = ColorTranslator.FromHtml("#EF4444"), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Visible = false, RightToLeft = RightToLeft.No };
        private Guna2Button MakeSaveBtn(string text) { var b = new Guna2Button { Dock = DockStyle.Fill, Text = text, BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#4E73DF"), ForeColor = Color.White, Font = new Font("Cairo", 11.5F, FontStyle.Bold), Animated = true }; b.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9"); b.ShadowDecoration.Enabled = true; b.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223); b.ShadowDecoration.Depth = 10; return b; }

        private async void ShowSuccessToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#10B981"), ColorTranslator.FromHtml("#ECFDF5"));
        private async void ShowErrorToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#FEF2F2"));
        private async Task ShowToast(string msg, Color accent, Color bg2)
        {
            var t = new Panel { Size = new Size(340, 50), BackColor = bg2, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath()) { gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(t.Width - 20, 0, 20, 20, 270, 90); gp.AddArc(t.Width - 20, t.Height - 20, 20, 20, 0, 90); gp.AddArc(0, t.Height - 20, 20, 20, 90, 90); gp.CloseFigure(); t.Region = new Region(gp); }
            t.Paint += (sndr, pe) => { pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var pen = new Pen(accent, 1.5f)) using (var path = RoundPath(new Rectangle(0, 0, t.Width - 1, t.Height - 1), 10)) pe.Graphics.DrawPath(pen, path); pe.Graphics.FillRectangle(new SolidBrush(accent), 0, 7, 4, t.Height - 14); using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1F2937"))) pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, t.Width - 8, t.Height), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36); Controls.Add(t); t.BringToFront();
            t.Click += (sndr, ev) => { try { Controls.Remove(t); t.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10) { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + (100 - i) / 5); await Task.Delay(7); }
            await Task.Delay(2600);
            for (int i = 0; i <= 100; i += 10) { try { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + i / 5); } catch { break; } await Task.Delay(7); }
            try { Controls.Remove(t); t.Dispose(); } catch { }
        }

        private static string GetInner(Exception ex) { if (ex == null) return ""; var e = ex; while (e.InnerException != null) e = e.InnerException; return e.Message; }
        private GraphicsPath RoundPath(Rectangle r, int radius) { int d = radius * 2; var path = new GraphicsPath(); path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90); path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path; }

        private void SalesInvoicesForm_Load(object sender, EventArgs e) { }
    }
}