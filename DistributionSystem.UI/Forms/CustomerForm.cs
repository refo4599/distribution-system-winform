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
    public partial class CustomerForm : Form
    {
        private readonly CustomerService _service = new CustomerService();
        private Guna2DataGridView dgvNew;
        private Guna2TextBox txtSearchNew;
        private Label lblCountBadge;
        private Guna2Button btnAddNew;
        private System.Threading.Timer _searchTimer;

        private List<CustomerDto> _allCustomers = new List<CustomerDto>();
        private int _currentPage = 1;
        private const int PageSize = 6;
        private Panel _paginationBar;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_allCustomers.Count / (double)PageSize));

        public CustomerForm()
        {
            InitializeComponent();
            HideOldControls();
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            BuildNewUI();
            Shown += (s, e) => BeginInvoke(new Action(async () => await LoadCustomersAsync()));
        }

        private void HideOldControls()
        {
            try
            {
                lblName.Visible = false; txtName.Visible = false;
                lblPhone.Visible = false; txtPhone.Visible = false;
                lblAddress.Visible = false; txtAddress.Visible = false;
                dgvCustomers.Visible = false;
                btnAdd.Visible = false; btnUpdate.Visible = false;
                btnDelete.Visible = false; btnClear.Visible = false;
            }
            catch { }
        }

        private void BuildNewUI()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(0);
            this.SuspendLayout();
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.SuspendLayout();
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // ? 70 ÂÌœ— ’€Ì—
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildTableCard(), 0, 1);
            root.ResumeLayout(false);
            EnableDbAll(root);
            Controls.Add(root); root.BringToFront();
            this.ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private static readonly PropertyInfo _dbProp = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent) { foreach (Control ctrl in parent.Controls) { try { _dbProp?.SetValue(ctrl, true); } catch { } if (ctrl.Controls.Count > 0) EnableDbAll(ctrl); } }
        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

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

        // ??????????????????????????????????????????????????????
        //  HEADER ó ‰›”  ’„Ì„ ProductForm
        // ??????????????????????????????????????????????????????
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

                // ? FillRectangle »œÊ‰ rounded corners “Ì «·„‰ Ã« 
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
                    string title = "≈œ«—… «·⁄„·«¡";
                    string sub = "⁄—÷ Ê≈œ«—… Ã„Ì⁄ «·⁄„·«¡";
                    var szT = g.MeasureString(title, tf);
                    var szS = g.MeasureString(sub, sf2f);
                    float gap = 4f;
                    float block = szT.Height + gap + szS.Height;
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

            btnAddNew = new Guna2Button
            {
                Text = "+ ≈÷«›… ⁄„Ì·",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(148, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(20, 10) // ? „ „—ﬂ“ —√”Ì«
            };
            btnAddNew.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAddNew.Click += (s, e) => ShowCustomerPopup();
            banner.Controls.Add(btnAddNew);
            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  TABLE CARD ó ‰›”  ’„Ì„ ProductForm
        // ??????????????????????????????????????????????????????
        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            // ?? Top bar ??????????????????????????????????????
            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };

            lblCountBadge = new Label { Text = "0 ⁄„Ì·", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblCountBadge.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblCountBadge);

            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;

                using (var tf = new Font("Cairo", 15F, FontStyle.Bold))
                using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                {
                    var sz = g.MeasureString("ﬁ«∆„… «·⁄„·«¡", tf);
                    g.DrawString("ﬁ«∆„… «·⁄„·«¡", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f);
                }

                string badge = lblCountBadge?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badge, bf);
                    int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2))
                    using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        g.FillPath(br, path);
                    g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };

            // ?? Õﬁ· «·»ÕÀ ›Ì Wrapper Panel ??????????????????
            txtSearchNew = new Guna2TextBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 8,
                PlaceholderText = "«»ÕÀ ⁄‰ ⁄„Ì·...",
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
                { try { await (Task)Invoke(new Func<Task>(LoadCustomersAsync)); } catch { } },
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
            searchWrapper.Controls.Add(txtSearchNew);
            topBar.Controls.Add(searchWrapper);

            // ›«’· ·Ê‰Ì
            var searchSeparator = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSeparator.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSeparator.Width, 3),
                    ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, searchSeparator.Width, 3);
            };

            // ?? DataGridView ?????????????????????????????????
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

        // ?? Columns ??????????????????????????????????????????
        private void BuildColumns()
        {
            dgvNew.Columns.Clear();
            void Add(string n, string h, string p, int w) =>
                dgvNew.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w });
            Add("Name", "«”„ «·⁄„Ì·", "Name", 200);
            Add("Phone", "—ﬁ„ «·Â« ›", "Phone", 160);
            Add("Address", "«·⁄‰Ê«‰", "Address", 200);
            dgvNew.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "«·‰Ê⁄", DataPropertyName = "CustomerType", Width = 90, ReadOnly = true });
            Add("Actions", "«·≈Ã—«¡« ", "", 130);
            dgvNew.Columns.Add(new DataGridViewTextBoxColumn { Name = "View", HeaderText = "⁄—÷", Width = 70, ReadOnly = true });
            foreach (DataGridViewColumn c in dgvNew.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void CustomerForm_Load(object sender, EventArgs e) { }

        // ??????????????????????????????????????????????????????
        //  DATA
        // ??????????????????????????????????????????????????????
        private async Task LoadCustomersAsync()
        {
            try
            {
                var q = txtSearchNew?.Text?.Trim() ?? "";
                var all = await Task.Run(() =>
                {
                    var list = (_service.GetAll() ?? Enumerable.Empty<CustomerDto>()).ToList();
                    if (!string.IsNullOrEmpty(q))
                        list = list.Where(x => (x.Name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                               (x.Phone?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                    return list;
                });
                _allCustomers = all;
                _currentPage = Math.Min(_currentPage, TotalPages);
                var page = _allCustomers.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
                dgvNew.DataSource = new BindingSource { DataSource = page };
                FitColumns();
                if (lblCountBadge != null) lblCountBadge.Text = $"{_allCustomers.Count} ⁄„Ì·";
                RenderPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"›‘·  Õ„Ì· «·⁄„·«¡: {ex.Message}", "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FitColumns()
        {
            if (dgvNew == null || dgvNew.Columns.Count == 0) return;
            int w = dgvNew.ClientSize.Width; if (w <= 0) return;
            int wType = 90, wView = 70, wAct = 130, rest = w - wType - wView - wAct;
            dgvNew.Columns["Name"].Width = Math.Max(120, (int)(rest * 0.35));
            dgvNew.Columns["Phone"].Width = Math.Max(100, (int)(rest * 0.27));
            dgvNew.Columns["Address"].Width = Math.Max(100, rest - dgvNew.Columns["Name"].Width - dgvNew.Columns["Phone"].Width);
            dgvNew.Columns["Type"].Width = wType;
            dgvNew.Columns["Actions"].Width = wAct;
            dgvNew.Columns["View"].Width = wView;
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
                Text = $"⁄—÷ {Math.Min(_allCustomers.Count, (_currentPage - 1) * PageSize + 1)}-{Math.Min(_allCustomers.Count, _currentPage * PageSize)} „‰ {_allCustomers.Count}",
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
            pnlPages.Controls.Add(MakeNavBtn("õ", _currentPage < total, () => { _currentPage++; _ = LoadCustomersAsync(); }));
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
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151"))) g.DrawString(pg.ToString(), f, tb, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!cur) btn.Click += (s2, e2) => { _currentPage = pg; _ = LoadCustomersAsync(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MakeNavBtn("ã", _currentPage > 1, () => { _currentPage--; _ = LoadCustomersAsync(); }));
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
            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as CustomerDto;
            if (dto == null) return;

            if (colName == "View")
            {
                try { new CustomerDetailsForm(dto.Id).ShowDialog(this); }
                catch (Exception ex) { MessageBox.Show("›‘· › Õ  ›«’Ì· «·⁄„Ì·: " + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }
            if (colName != "Actions") return;

            var cell = dgvNew.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var mouse = dgvNew.PointToClient(Cursor.Position);
            int btnH = 32, btnY = cell.Top + (cell.Height - btnH) / 2, editW = 62, delW = 32, gap = 8;
            int startX = cell.Left + (cell.Width - editW - gap - delW) / 2;
            if (new Rectangle(startX, btnY, editW, btnH).Contains(mouse))
                ShowCustomerPopup(dto);
            else if (new Rectangle(startX + editW + gap, btnY, delW, btnH).Contains(mouse))
                if (ShowDeleteConfirm(dto.Name))
                    try { _service.Delete(dto.Id); _ = LoadCustomersAsync(); }
                    catch (Exception ex) { ShowErrorDialog(dto.Name, ex.Message); }
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
                    e.Handled = true;
                    var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(e.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical))
                        g.FillRectangle(br, e.CellBounds);
                    using (var font = new Font("Cairo", 11F, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); g.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
                    return;
                }
                if (e.RowIndex < 0) return;

                bool sel = dgvNew.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                var col = dgvNew.Columns[e.ColumnIndex].Name;

                if (col == "Name") PaintNameCell(e, bg);
                else if (col == "Type") PaintTypeCell(e, bg);
                else if (col == "View") PaintViewCell(e, bg);
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

        private void PaintNameCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics;
            g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as CustomerDto;
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
                g.DrawString(letter, lf, Brushes.White, avX + (avSize - ls.Width) / 2f, avY + (avSize - ls.Height) / 2f);
            }

            float textW = (avX - 8f) - e.CellBounds.Left - 4f;
            using (var nf = new Font("Cairo", 13F, FontStyle.Bold))
            using (var nb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, nf, nb, new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, textW, e.CellBounds.Height), _sfCenter);
            g.ResetClip();
        }

        private void PaintTypeCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);

            var dto = dgvNew.Rows[e.RowIndex].DataBoundItem as CustomerDto;
            if (dto == null) return;

            bool isInv = dto.CustomerType == CustomerType.Invoices;
            string txt = isInv ? "›Ê« Ì—" : "Ê«—œ« ";
            Color badgeBg = isInv ? ColorTranslator.FromHtml("#D1FAE5") : ColorTranslator.FromHtml("#DBEAFE");
            Color badgeFg = isInv ? ColorTranslator.FromHtml("#065F46") : ColorTranslator.FromHtml("#1E40AF");
            Color border = isInv ? ColorTranslator.FromHtml("#6EE7B7") : ColorTranslator.FromHtml("#93C5FD");

            int bw = 68, bh = 28;
            int bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2;
            int by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var brc = new Rectangle(bx, by, bw, bh);
            using (var path = RoundPath(brc, bh / 2)) { g.FillPath(new SolidBrush(badgeBg), path); g.DrawPath(new Pen(border, 1f), path); }
            using (var f = new Font("Cairo", 10F, FontStyle.Bold))
            using (var tb = new SolidBrush(badgeFg))
                g.DrawString(txt, f, tb, brc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintViewCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);

            int bw = 50, bh = 28;
            int bx = e.CellBounds.Left + (e.CellBounds.Width - bw) / 2;
            int by = e.CellBounds.Top + (e.CellBounds.Height - bh) / 2;
            var brc = new Rectangle(bx, by, bw, bh);
            using (var path = RoundPath(brc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F0FDF4")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#86EFAC"), 1f), path); }
            int cx = brc.Left + brc.Width / 2, cy = brc.Top + brc.Height / 2;
            using (var pen = new Pen(ColorTranslator.FromHtml("#16A34A"), 1.6f))
            { g.DrawArc(pen, cx - 7, cy - 4, 14, 9, 0, 180); g.DrawArc(pen, cx - 7, cy - 4, 14, 9, 180, 180); }
            using (var br = new SolidBrush(ColorTranslator.FromHtml("#16A34A")))
                g.FillEllipse(br, cx - 2, cy - 2, 5, 5);
        }

        private void PaintActionsCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int btnH = 32, btnY = e.CellBounds.Top + (e.CellBounds.Height - btnH) / 2;
            int editW = 62, delW = 32, gap = 8;
            int startX = e.CellBounds.Left + (e.CellBounds.Width - editW - gap - delW) / 2;

            var editRect = new Rectangle(startX, btnY, editW, btnH);
            using (var path = RoundPath(editRect, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
            { var sz = g.MeasureString(" ⁄œÌ·", f); g.DrawString(" ⁄œÌ·", f, tb, editRect.Left + (editRect.Width - sz.Width) / 2f, editRect.Top + (editRect.Height - sz.Height) / 2f); }

            var delRect = new Rectangle(startX + editW + gap, btnY, delW, btnH);
            using (var path2 = RoundPath(delRect, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path2); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path2); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
            {
                int cx = delRect.Left + delRect.Width / 2, cy = delRect.Top + delRect.Height / 2;
                g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4); g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6);
                g.DrawRectangle(pen, cx - 4, cy - 3, 8, 7); g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 3); g.DrawLine(pen, cx + 1, cy - 1, cx + 1, cy + 3);
            }
            g.ResetClip();
        }

        // ??????????????????????????????????????????????????????
        //  POPUP
        // ??????????????????????????????????????????????????????
        private void btnAdd_Click(object sender, EventArgs e) => ShowCustomerPopup();

        private async void ShowCustomerPopup(CustomerDto editDto = null)
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
            popupForm.Location = new Point(sc.Left + (sc.Width - popupForm.Width) / 2, sc.Top + (sc.Height - popupForm.Height) / 2);

            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 40, 40, 180, 90); rgn.AddArc(popupForm.Width - 40, 0, 40, 40, 270, 90);
                rgn.AddArc(popupForm.Width - 40, popupForm.Height - 40, 40, 40, 0, 90); rgn.AddArc(0, popupForm.Height - 40, 40, 40, 90, 90);
                rgn.CloseFigure(); popupForm.Region = new Region(rgn);
            }

            popupForm.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => popupForm.Close();

            var popup = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White, BackColor = Color.White };
            popup.ShadowDecoration.Enabled = true; popup.ShadowDecoration.Depth = 32; popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            popupForm.Controls.Add(popup);
            Action closePopup = () => { try { popupForm.Close(); popupForm.Dispose(); } catch { } };

            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            pnlHead.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc2 = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc2);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 8; x < pnlHead.Width; x += 20) for (int y = 6; y < pnlHead.Height; y += 20) g.FillEllipse(db, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                    g.FillEllipse(cb2, pnlHead.Width - 100, -40, 180, 180);
                string ht = isEdit ? " ⁄œÌ· »Ì«‰«  «·⁄„Ì·" : "≈÷«›… ⁄„Ì· ÃœÌœ";
                string sub = isEdit ? "⁄œ¯· «·»Ì«‰«  À„ «÷€ÿ  ÕœÌÀ" : "√œŒ· »Ì«‰«  «·⁄„Ì· «·ÃœÌœ À„ «÷€ÿ Õ›Ÿ";
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb2 = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(ht, tf); g.DrawString(ht, tf, tb2, pnlHead.Width - tsz.Width - 60, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString(sub, sf3); g.DrawString(sub, sf3, sb3, pnlHead.Width - ssz.Width - 60, 54); }
            };

            var btnClose = new Guna2Button { Size = new Size(30, 30), Text = "X", FillColor = Color.FromArgb(50, 255, 255, 255), ForeColor = Color.White, BorderRadius = 8, BorderThickness = 1, BorderColor = Color.FromArgb(70, 255, 255, 255), Font = new Font("Segoe UI", 11F, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            btnClose.HoverState.FillColor = Color.FromArgb(80, 255, 255, 255);
            btnClose.Click += (s7, e7) => closePopup();
            pnlHead.Controls.Add(btnClose);
            pnlHead.Layout += (s8, e8) => btnClose.Location = new Point(25, 20);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8), RightToLeft = RightToLeft.Yes };

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
            Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };
            Panel Div() => new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorTranslator.FromHtml("#E2E8F0") };

            var wrapName = MakeTxtWrapped("„À«·: „’ÿ›Ì »’Ê’", out var fName);
            var wrapPhone = MakeTxtWrapped("„À«·: 01012345678", out var fPhone);
            var wrapAddress = MakeTxtWrapped("„À«·: ”ÊÂ«Ã° √Œ„Ì„", out var fAddress);

            var lblType = MakeLbl("‰Ê⁄ «·⁄„Ì· *");
            var pnlType = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.Transparent };
            pnlType.Paint += (s6, pe6) =>
            {
                var g = pe6.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc6 = new Rectangle(0, 2, pnlType.Width - 1, pnlType.Height - 4);
                using (var path = RoundPath(rc6, 8)) { g.FillPath(new SolidBrush(Color.White), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#C7D2FE"), 1.5f), path); }
            };
            var rbInvoices = new RadioButton { Text = "›Ê« Ì— „»Ì⁄« ", Font = new Font("Cairo", 10.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#065F46"), Checked = true, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.Transparent };
            var rbInbounds = new RadioButton { Text = "Ê«—œ« ", Font = new Font("Cairo", 10.5F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#1E40AF"), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, BackColor = Color.Transparent };
            pnlType.Controls.Add(rbInvoices); pnlType.Controls.Add(rbInbounds);
            pnlType.Layout += (s6, e6) =>
            {
                rbInvoices.Location = new Point(pnlType.Width - rbInvoices.Width - 16, (pnlType.Height - rbInvoices.Height) / 2);
                rbInbounds.Location = new Point(20, (pnlType.Height - rbInbounds.Height) / 2);
            };

            body.Controls.Add(Sp(16));
            body.Controls.Add(wrapAddress); body.Controls.Add(MakeLbl("«·⁄‰Ê«‰"));
            body.Controls.Add(Sp(6)); body.Controls.Add(Div()); body.Controls.Add(Sp(6));
            body.Controls.Add(pnlType); body.Controls.Add(lblType);
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapPhone); body.Controls.Add(MakeLbl("—ﬁ„ «·Â« › *"));
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapName); body.Controls.Add(MakeLbl("«”„ «·⁄„Ì· *"));

            if (isEdit)
            {
                fName.Text = editDto.Name;
                fPhone.Text = editDto.Phone;
                fAddress.Text = editDto.Address;
                rbInvoices.Checked = editDto.CustomerType == CustomerType.Invoices;
                rbInbounds.Checked = editDto.CustomerType == CustomerType.Inbounds;
            }

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = ColorTranslator.FromHtml("#F8FAFF"), Padding = new Padding(24, 10, 24, 14) };
            footer.Paint += (s6, pe6) =>
            {
                var g = pe6.Graphics;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 2f)) g.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 2, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 2, footer.Width, 2);
            };

            var btnSave = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = isEdit ? " ÕœÌÀ «·⁄„Ì·" : "Õ›Ÿ «·⁄„Ì·",
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
                fName.BorderColor = fPhone.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                bool valid = true;
                if (string.IsNullOrWhiteSpace(fName.Text)) { fName.BorderColor = ColorTranslator.FromHtml("#EF4444"); valid = false; }
                if (string.IsNullOrWhiteSpace(fPhone.Text)) { fPhone.BorderColor = ColorTranslator.FromHtml("#EF4444"); valid = false; }
                if (!valid) return;
                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                try
                {
                    var dto = new CustomerDto
                    {
                        Name = fName.Text.Trim(),
                        Phone = fPhone.Text.Trim(),
                        Address = fAddress.Text.Trim(),
                        CustomerType = rbInvoices.Checked ? CustomerType.Invoices : CustomerType.Inbounds
                    };
                    await Task.Run(() => { if (!isEdit) _service.Insert(dto); else { dto.Id = editDto.Id; _service.Update(dto); } });
                    _ = LoadCustomersAsync();
                    closePopup();
                }
                catch (Exception ex)
                {
                    btnSave.Enabled = true;
                    btnSave.Text = isEdit ? " ÕœÌÀ «·⁄„Ì·" : "Õ›Ÿ «·⁄„Ì·";
                    MessageBox.Show("Œÿ√: " + ex.Message);
                }
            };

            footer.Controls.Add(btnSave);
            popup.Controls.Add(body); popup.Controls.Add(footer); popup.Controls.Add(pnlHead);
            popupForm.Shown += (s5, e5) => fName.Focus();
            popupForm.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  ERROR + DELETE DIALOGS
        // ??????????????????????????????????????????????????????
        private void ShowErrorDialog(string entityName, string rawError)
        {
            bool isFk = rawError != null && (rawError.Contains("FOREIGN KEY") || rawError.Contains("REFERENCE") || rawError.Contains("constraint"));
            string title = isFk ? "·« Ì„ﬂ‰ Õ–› «·⁄„Ì·" : "ÕœÀ Œÿ√";
            string line1 = isFk ? $"·« Ì„ﬂ‰ Õ–› «·⁄„Ì· \"{entityName}\"" : "ÕœÀ Œÿ√ €Ì— „ Êﬁ⁄";
            string line2 = isFk ? "·√‰ ·œÌÂ ›Ê« Ì— „»Ì⁄«  „”Ã¯·… ›Ì «·‰Ÿ«„." : rawError;
            string line3 = isFk ? "ÌÃ» Õ–› «·›Ê« Ì— «·„— »ÿ… √Ê·« À„ «·„Õ«Ê·… „Ãœœ«." : "";
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(440, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.Transparent };
            header.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height); using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#B45309"), ColorTranslator.FromHtml("#D97706"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2); using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < header.Width; x += 20) for (int y = 6; y < header.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2); int ix = header.Width - 62, iy = 18, isz = 46; using (var ip = RoundPath(new Rectangle(ix, iy, isz, isz), 23)) g.FillPath(new SolidBrush(Color.FromArgb(45, 255, 255, 255)), ip); using (var pen = new Pen(Color.White, 3f)) using (var wBr = new SolidBrush(Color.White)) { int cx2 = ix + isz / 2, cy2 = iy + isz / 2; g.DrawLine(pen, cx2, cy2 - 11, cx2, cy2 + 1); g.FillEllipse(wBr, cx2 - 3, cy2 + 7, 6, 6); } using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, header.Width - tsz.Width - 70, 12); } using (var sf3 = new Font("Cairo", 9.5F)) using (var sb3 = new SolidBrush(Color.FromArgb(210, 255, 255, 255))) g.DrawString(" ⁄–¯—  ‰›Ì– ⁄„·Ì… «·Õ–›", sf3, sb3, header.Width - 222, 50); };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 16, 22, 0) };
            var msgPanel = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            msgPanel.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, msgPanel.Width - 1, msgPanel.Height - 1); using (var path = RoundPath(rc2, 12)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FFFBEB")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FDE68A"), 1.5f), path); } using (var f1 = new Font("Cairo", 11.5F, FontStyle.Bold)) using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#92400E"))) g.DrawString(line1, f1, b1, new RectangleF(12, 10, msgPanel.Width - 24, 28), new StringFormat { Alignment = StringAlignment.Far }); using (var f2 = new Font("Cairo", 10.5F)) using (var b2 = new SolidBrush(ColorTranslator.FromHtml("#78350F"))) g.DrawString(line2, f2, b2, new RectangleF(12, 40, msgPanel.Width - 24, 26), new StringFormat { Alignment = StringAlignment.Far }); if (!string.IsNullOrEmpty(line3)) using (var f3 = new Font("Cairo", 9.5F, FontStyle.Italic)) using (var b3 = new SolidBrush(ColorTranslator.FromHtml("#A16207"))) g.DrawString(line3, f3, b3, new RectangleF(12, 66, msgPanel.Width - 24, 24), new StringFormat { Alignment = StringAlignment.Far }); };
            body.Controls.Add(msgPanel);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = Color.White, Padding = new Padding(24, 10, 24, 14) };
            var btnOk = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ”‰«° ›Â„ ", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#D97706"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Animated = true };
            btnOk.HoverState.FillColor = ColorTranslator.FromHtml("#B45309"); btnOk.ShadowDecoration.Enabled = true; btnOk.ShadowDecoration.Color = Color.FromArgb(40, 217, 119, 6); btnOk.ShadowDecoration.Depth = 8;
            btnOk.Click += (s, e) => dlg.Close();
            footer.Controls.Add(btnOk); root.Controls.Add(body); root.Controls.Add(footer); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Return) dlg.Close(); }; dlg.ShowDialog(this);
        }

        private bool ShowDeleteConfirm(string customerName)
        {
            bool result = false;
            var dlg = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent, Size = new Size(420, 260), BackColor = Color.White, ShowInTaskbar = false, TopMost = true, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            using (var rgn = new GraphicsPath()) { rgn.AddArc(0, 0, 32, 32, 180, 90); rgn.AddArc(dlg.Width - 32, 0, 32, 32, 270, 90); rgn.AddArc(dlg.Width - 32, dlg.Height - 32, 32, 32, 0, 90); rgn.AddArc(0, dlg.Height - 32, 32, 32, 90, 90); rgn.CloseFigure(); dlg.Region = new Region(rgn); }
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var header = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Color.Transparent };
            header.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, header.Width, header.Height); using (var br = new LinearGradientBrush(rc2, ColorTranslator.FromHtml("#C0392B"), ColorTranslator.FromHtml("#E74C3C"), LinearGradientMode.Horizontal)) g.FillRectangle(br, rc2); using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) for (int x = 8; x < header.Width; x += 20) for (int y = 6; y < header.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2); int ix = header.Width - 62, iy = 22, isz = 46; using (var ip = RoundPath(new Rectangle(ix, iy, isz, isz), 12)) { g.FillPath(new SolidBrush(Color.FromArgb(40, 255, 255, 255)), ip); g.DrawPath(new Pen(Color.FromArgb(60, 255, 255, 255), 1f), ip); } using (var pen = new Pen(Color.White, 2f)) { int cx2 = ix + isz / 2, cy2 = iy + isz / 2; g.DrawLine(pen, cx2 - 10, cy2 - 8, cx2 + 10, cy2 - 8); g.DrawLine(pen, cx2 - 5, cy2 - 12, cx2 + 5, cy2 - 12); g.DrawRectangle(pen, cx2 - 9, cy2 - 6, 18, 16); g.DrawLine(pen, cx2 - 3, cy2 - 2, cx2 - 3, cy2 + 6); g.DrawLine(pen, cx2 + 3, cy2 - 2, cx2 + 3, cy2 + 6); } using (var tf = new Font("Cairo", 18F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White)) { var tsz = g.MeasureString("Õ–› «·⁄„Ì·", tf); g.DrawString("Õ–› «·⁄„Ì·", tf, tb, header.Width - tsz.Width - 68, 14); } using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255))) g.DrawString("Â–« «·≈Ã—«¡ ·« Ì„ﬂ‰ «· —«Ã⁄ ⁄‰Â", sf3, sb3, header.Width - 238, 52); };
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 18, 28, 0) };
            var nameBox = new Panel { Dock = DockStyle.Top, Height = 50 };
            nameBox.Paint += (s, pe) => { var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var rc2 = new Rectangle(0, 0, nameBox.Width - 1, nameBox.Height - 1); using (var path = RoundPath(rc2, 10)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1.5f), path); } using (var f = new Font("Cairo", 12F, FontStyle.Bold)) using (var b = new SolidBrush(ColorTranslator.FromHtml("#B91C1C"))) g.DrawString($"  {customerName}  ", f, b, rc2, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); };
            body.Controls.Add(nameBox);
            body.Controls.Add(new Label { Text = "Â· √‰  „ √ﬂœ „‰ Õ–› Â–« «·⁄„Ì·ø", Font = new Font("Cairo", 12F), ForeColor = ColorTranslator.FromHtml("#374151"), Dock = DockStyle.Top, Height = 36, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.White, Padding = new Padding(24, 12, 24, 20) };
            var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var btnCancel = new Guna2Button { Dock = DockStyle.Fill, Text = "≈·€«¡", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#F1F5F9"), ForeColor = ColorTranslator.FromHtml("#64748B"), BorderColor = ColorTranslator.FromHtml("#E2E8F0"), BorderThickness = 1, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(0, 0, 6, 0) };
            btnCancel.HoverState.FillColor = ColorTranslator.FromHtml("#E2E8F0"); btnCancel.Click += (s, e) => dlg.Close();
            var btnConfirm = new Guna2Button { Dock = DockStyle.Fill, Text = "Õ–›", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#EF4444"), ForeColor = Color.White, Font = new Font("Cairo", 11F, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Animated = true };
            btnConfirm.HoverState.FillColor = ColorTranslator.FromHtml("#DC2626"); btnConfirm.ShadowDecoration.Enabled = true; btnConfirm.ShadowDecoration.Color = Color.FromArgb(40, 239, 68, 68); btnConfirm.ShadowDecoration.Depth = 8;
            btnConfirm.Click += (s, e) => { result = true; dlg.Close(); };
            tbl.Controls.Add(btnCancel, 0, 0); tbl.Controls.Add(btnConfirm, 1, 0); footer.Controls.Add(tbl);
            root.Controls.Add(body); root.Controls.Add(footer); root.Controls.Add(header); dlg.Controls.Add(root);
            dlg.KeyPreview = true; dlg.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) dlg.Close(); };
            dlg.ShowDialog(this); return result;
        }

        private void btnUpdate_Click(object sender, EventArgs e) { }
        private void btnDelete_Click(object sender, EventArgs e) { }
        private void btnClear_Click(object sender, EventArgs e) { }
        private void dgvCustomers_SelectionChanged(object sender, EventArgs e) { }
    }
}