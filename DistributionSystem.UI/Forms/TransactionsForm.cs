using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public class TransactionsForm : Form
    {
        private readonly WarehouseService _service = new WarehouseService();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Guna2DataGridView dgvTransactions;
        private Label lblCountBadge;
        private Panel _searchPanel;
        private System.Windows.Forms.TextBox _searchBox;

        private List<WarehouseTransactionViewDto> _allData = new List<WarehouseTransactionViewDto>();
        private List<WarehouseTransactionViewDto> _filtered = new List<WarehouseTransactionViewDto>();
        private int _currentPage = 1;
        private const int PageSize = 10;
        private Panel _paginationBar;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));

        private static readonly Dictionary<string, string> TypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Inbound",         "Ê«—œ „Œ“‰"    }, { "CarLoad",       " Õ„Ì· ”Ì«—…"  },
            { "Return",          "„— Ã⁄"         }, { "CarReturn",     "≈—Ã«⁄ ”Ì«—…"  },
            { "Outbound",        "’«œ—"           }, { "SaleRevenue",   "≈Ì—«œ »Ì⁄"    },
            { "OpeningBalance",  "—’Ìœ «›  «ÕÌ"  }, { "Ê«—œ",          "Ê«—œ „Œ“‰"    },
            { "—’Ìœ «›  «ÕÌ",   "—’Ìœ «›  «ÕÌ"  }, { "EmployeeExpense","„’—Ê› „ÊŸ›"   },
            { "AdminExpense",    "„’—Ê› ≈œ«—Ì"   }, { "CashDeposit",   "≈Ìœ«⁄ Œ“‰…"  },
            { "CashWithdraw",    "Œ’„ Œ“‰…"      }, { "SalaryPayment", "’—› —« »"     },
        };
        private static string Translate(string raw)
            => TypeMap.TryGetValue(raw ?? "", out var v) ? v : (raw ?? "ó");

        public TransactionsForm()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(20);
            MinimumSize = new Size(1000, 700);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();
            BuildLayout();
            Load += (s, e) => LoadTransactions();
            SizeChanged += (s, e) => FitColumns();
        }

        private static readonly PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            { try { _dbProp?.SetValue(ctrl, true); } catch { } if (ctrl.Controls.Count > 0) EnableDbAll(ctrl); }
        }

        // ??????????????????????????????????????????????????????????
        //  BUILD LAYOUT
        // ??????????????????????????????????????????????????????????
        private void BuildLayout()
        {
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildPageHeader(), 0, 0);
            root.Controls.Add(BuildTableCard(), 0, 1);
            root.ResumeLayout(false);
            Controls.Add(root);
            EnableDbAll(this);
            this.ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????????
        //  PAGE HEADER
        // ??????????????????????????????????????????????????????????
        private Panel BuildPageHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 88, Padding = new Padding(0, 0, 0, 12), BackColor = Color.Transparent };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            banner.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
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
            };

            // ?? ⁄‰Ê«‰ Ì„Ì‰ ????????????????????????????????????
            var pnlTitle = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = Color.Transparent, Padding = new Padding(0, 0, 20, 0) };
            var lblMain = new Label
            {
                Text = "Õ—ﬂ… «· ‰ﬁ·« ",
                Font = new Font("Cairo", 24F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 46,
                TextAlign = ContentAlignment.BottomRight,
                BackColor = Color.Transparent
            };
            var pnlAccent = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = Color.Transparent };
            pnlAccent.Paint += (s, e) =>
            {
                var g = e.Graphics;
                int right = pnlAccent.Width;
                using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                    g.FillRectangle(b1, right - 40, 7, 38, 3);
                using (var b2 = new SolidBrush(Color.FromArgb(120, 100, 181, 246)))
                    g.FillRectangle(b2, right - 58, 7, 14, 3);
                using (var sf = new Font("Cairo", 10F))
                using (var brush = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
                    g.DrawString("⁄—÷ Ã„Ì⁄ Õ—ﬂ«  «·„Œ“‰ Ê«·Œ“‰…", sf, brush, right - 230, 1);
            };
            pnlTitle.Controls.Add(pnlAccent);
            pnlTitle.Controls.Add(lblMain);
            banner.Controls.Add(pnlTitle);

            // ?? “— «· ﬁ—Ì— «·ÌÊ„Ì (Ì”«—) ?????????????????????
            var btnDailyReport = new Guna2Button
            {
                Text = "??   ﬁ—Ì— ÌÊ„Ì",
                Size = new Size(148, 40),
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#059669"),
                ForeColor = Color.White,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(16, 22)
            };
            btnDailyReport.HoverState.FillColor = ColorTranslator.FromHtml("#047857");
            btnDailyReport.ShadowDecoration.Enabled = true;
            btnDailyReport.ShadowDecoration.Color = Color.FromArgb(40, 5, 150, 105);
            btnDailyReport.ShadowDecoration.Depth = 8;

            // ??????????????????????????????????????????????????
            //  “— «· ﬁ—Ì— ó Ì› Õ DatePickerForm À„ ÌÊ·œ PDF
            // ??????????????????????????????????????????????????
            btnDailyReport.Click += (s, e) =>
            {
                using (var picker = new DatePickerForm())
                {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;

                    DateTime selectedDate = picker.SelectedDate;

                    try
                    {
                        var pdfService = new TransactionsReportPdfService();
                        // _allData ÂÌ ﬂ· «·Õ—ﬂ«  «·„Õ„·… ó «·‹ service ÂÌ›· —Â« »«· «—ÌŒ
                        pdfService.GenerateAndOpen(selectedDate, _allData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message,
                            "Œÿ√",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            };

            banner.Controls.Add(btnDailyReport);

            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????????
        //  TABLE CARD
        // ??????????????????????????????????????????????????????????
        private Control BuildTableCard()
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 12, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true;
            container.ShadowDecoration.Depth = 20;
            container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            // ?? TopBar ????????????????????????????????????????
            var topBar = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.White };

            lblCountBadge = new Label
            {
                Text = "0 Õ—ﬂ…",
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                Size = new Size(90, 34),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblCountBadge.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, lblCountBadge.Width, lblCountBadge.Height);
                using (var br = new LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#4E73DF"),
                    ColorTranslator.FromHtml("#3B5DC9"),
                    LinearGradientMode.Vertical))
                using (var path = RoundPath(rc, 17))
                    g.FillPath(br, path);
                using (var f = new Font("Cairo", 11F, FontStyle.Bold))
                    g.DrawString(lblCountBadge.Text, f, Brushes.White, rc,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            lblCountBadge.TextChanged += (s, e) =>
            {
                using (var g = lblCountBadge.CreateGraphics())
                using (var f = new Font("Cairo", 11F, FontStyle.Bold))
                    lblCountBadge.Width = (int)g.MeasureString(lblCountBadge.Text, f).Width + 28;
                lblCountBadge.Invalidate();
                lblCountBadge.Location = new Point(
                    topBar.Width - lblCountBadge.Width - 16,
                    (topBar.Height - lblCountBadge.Height) / 2);
            };
            topBar.Controls.Add(lblCountBadge);

            var lblTitle = new Label
            {
                Text = "”Ã· Õ—ﬂ… «· ‰ﬁ·« ",
                Font = new Font("Cairo", 15F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                BackColor = Color.White,
                AutoSize = true
            };
            topBar.Controls.Add(lblTitle);

            // ?? Search Box ????????????????????????????????????
            _searchBox = new System.Windows.Forms.TextBox
            {
                Width = 220,
                Height = 32,
                Font = new Font("Cairo", 10F),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = ColorTranslator.FromHtml("#374151"),
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                Text = "«»ÕÀ ⁄‰ „‰ Ã √Ê ‰Ê⁄ Õ—ﬂ…..."
            };
            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchBox.Text == "«»ÕÀ ⁄‰ „‰ Ã √Ê ‰Ê⁄ Õ—ﬂ…...")
                { _searchBox.Text = ""; _searchBox.ForeColor = ColorTranslator.FromHtml("#374151"); }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_searchBox.Text))
                { _searchBox.Text = "«»ÕÀ ⁄‰ „‰ Ã √Ê ‰Ê⁄ Õ—ﬂ…..."; _searchBox.ForeColor = ColorTranslator.FromHtml("#94A3B8"); }
            };
            _searchBox.TextChanged += (s, e) =>
            {
                string q = _searchBox.Text.Trim();
                if (q == "«»ÕÀ ⁄‰ „‰ Ã √Ê ‰Ê⁄ Õ—ﬂ…..." || string.IsNullOrEmpty(q))
                    _filtered = new List<WarehouseTransactionViewDto>(_allData);
                else
                    _filtered = _allData.Where(x =>
                        (x.ProductName?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (x.TransactionType?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    ).ToList();
                _currentPage = 1;
                BindPage();
            };
            topBar.Controls.Add(_searchBox);

            topBar.Resize += (s, e) =>
            {
                lblTitle.Location = new Point(
                    (topBar.Width - lblTitle.Width) / 2,
                    (topBar.Height - lblTitle.Height) / 2);
                lblCountBadge.Location = new Point(
                    topBar.Width - lblCountBadge.Width - 16,
                    (topBar.Height - lblCountBadge.Height) / 2);
                _searchBox.Location = new Point(16, (topBar.Height - _searchBox.Height) / 2);
            };

            // ?? Separator ?????????????????????????????????????
            var sep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            sep.Paint += (s, pe) =>
            {
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, sep.Width, 3),
                    ColorTranslator.FromHtml("#4E73DF"),
                    ColorTranslator.FromHtml("#E8EDFF"),
                    LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 0, sep.Width, 3);
            };

            // ?? DataGridView ??????????????????????????????????
            dgvTransactions = new Guna2DataGridView
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
            dgvTransactions.RowTemplate.Height = 64;
            dgvTransactions.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgvTransactions.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1a2f5e");
            dgvTransactions.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvTransactions.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 10.5F, FontStyle.Bold);
            dgvTransactions.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvTransactions.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1a2f5e");
            dgvTransactions.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgvTransactions.DefaultCellStyle.BackColor = Color.White;
            dgvTransactions.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgvTransactions.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgvTransactions.DefaultCellStyle.Font = new Font("Cairo", 11F);
            dgvTransactions.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#374151");
            dgvTransactions.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try
            {
                typeof(DataGridView).GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgvTransactions, true);
            }
            catch { }

            BuildColumns();
            dgvTransactions.CellPainting += Dgv_CellPainting;
            dgvTransactions.Resize += (s, e) => FitColumns();

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
            dgvWrapper.Controls.Add(dgvTransactions);

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.White, Padding = new Padding(16, 0, 16, 0) };
            _paginationBar.Paint += (s, pe) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f))
                    pe.Graphics.DrawLine(pen, 0, 0, _paginationBar.Width, 0);
            };

            container.Controls.Add(dgvWrapper);
            container.Controls.Add(_paginationBar);
            container.Controls.Add(sep);
            container.Controls.Add(topBar);
            card.Controls.Add(container);
            return card;
        }

        // ??????????????????????????????????????????????????????????
        //  COLUMNS
        // ??????????????????????????????????????????????????????????
        private void BuildColumns()
        {
            dgvTransactions.Columns.Clear();
            void Add(string name, string hdr, string prop, int w)
                => dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn
                { Name = name, HeaderText = hdr, DataPropertyName = prop, Width = w });
            Add("ProductName", "«·„‰ Ã / «·»Ì«‰", "ProductName", 180);
            Add("TransactionType", "‰Ê⁄ «·Õ—ﬂ…", "TransactionType", 130);
            Add("Quantity", "«·ﬂ„Ì…", "Quantity", 90);
            Add("UnitCost", "”⁄— «·ÊÕœ…", "UnitCost", 120);
            Add("TotalValue", "«·≈Ã„«·Ì", "TotalValue", 120);
            Add("CreatedAt", "«· «—ÌŒ Ê«·Êﬁ ", "CreatedAt", 160);
            foreach (DataGridViewColumn c in dgvTransactions.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        // ??????????????????????????????????????????????????????????
        //  LOAD
        // ??????????????????????????????????????????????????????????
        private void LoadTransactions()
        {
            try
            {
                var all = _service.GetAllTransactions()?.ToList() ?? new List<WarehouseTransactionViewDto>();
                foreach (var item in all)
                {
                    item.TransactionType = Translate(item.TransactionType);
                    if (item.CreatedAt.Kind == DateTimeKind.Utc)
                        item.CreatedAt = item.CreatedAt.ToLocalTime();
                    else if (item.CreatedAt.Kind == DateTimeKind.Unspecified)
                        item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Local);
                }
                _allData = all;
                _filtered = new List<WarehouseTransactionViewDto>(all);
                _currentPage = Math.Min(_currentPage, TotalPages);
                BindPage();
                if (lblCountBadge != null) lblCountBadge.Text = $"{_allData.Count} Õ—ﬂ…";
            }
            catch (Exception ex)
            {
                MessageBox.Show("›‘·  Õ„Ì· «· ‰ﬁ·« : " + ex.Message, "Œÿ√",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindPage()
        {
            var page = _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
            dgvTransactions.DataSource = new BindingSource { DataSource = page };
            FitColumns();
            RenderPagination();
        }

        // ??????????????????????????????????????????????????????????
        //  PAGINATION
        // ??????????????????????????????????????????????????????????
        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = TotalPages;
            int shown1 = _filtered.Count == 0 ? 0 : Math.Min(_filtered.Count, (_currentPage - 1) * PageSize + 1);
            int shown2 = Math.Min(_filtered.Count, _currentPage * PageSize);
            _paginationBar.Controls.Add(new Label
            {
                Text = $"⁄—÷ {shown1}-{shown2} „‰ {_filtered.Count}",
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Width = 180,
                Height = 56,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            });
            var pnlPages = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(0)
            };
            pnlPages.Controls.Add(MakeNavBtn("õ", _currentPage < total, () => { _currentPage++; BindPage(); }));
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
                        using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                            g.DrawString(pg.ToString(), f, Brushes.White, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else
                    {
                        using (var path = RoundPath(rc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                            g.DrawString(pg.ToString(), f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!isCurrent) btn.Click += (s, e) => { _currentPage = pg; BindPage(); };
                pnlPages.Controls.Add(btn);
            }
            pnlPages.Controls.Add(MakeNavBtn("ã", _currentPage > 1, () => { _currentPage--; BindPage(); }));
            _paginationBar.Controls.Add(pnlPages);
        }

        private Panel MakeNavBtn(string text, bool enabled, Action onClick)
        {
            var btn = new Panel { Size = new Size(36, 36), BackColor = Color.Transparent, Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(3, 10, 3, 10) };
            btn.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = RoundPath(rc, 8))
                {
                    g.FillPath(new SolidBrush(enabled ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#F1F5F9")), path);
                    g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path);
                }
                using (var f = new Font("Segoe UI", 13F))
                using (var tb = new SolidBrush(enabled ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#CBD5E1")))
                    g.DrawString(text, f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            if (enabled) btn.Click += (s, e) => onClick();
            return btn;
        }

        // ??????????????????????????????????????????????????????????
        //  FIT COLUMNS
        // ??????????????????????????????????????????????????????????
        private void FitColumns()
        {
            if (dgvTransactions == null || dgvTransactions.Columns.Count == 0) return;
            int w = dgvTransactions.ClientSize.Width; if (w <= 0) return;
            int wProd = (int)(w * 0.22);
            int wType = (int)(w * 0.16);
            int wQty = (int)(w * 0.10);
            int wCost = (int)(w * 0.14);
            int wTotal = (int)(w * 0.14);
            int wDate = w - wProd - wType - wQty - wCost - wTotal;
            dgvTransactions.Columns["ProductName"].Width = Math.Max(100, wProd);
            dgvTransactions.Columns["TransactionType"].Width = Math.Max(90, wType);
            dgvTransactions.Columns["Quantity"].Width = Math.Max(60, wQty);
            dgvTransactions.Columns["UnitCost"].Width = Math.Max(90, wCost);
            dgvTransactions.Columns["TotalValue"].Width = Math.Max(90, wTotal);
            dgvTransactions.Columns["CreatedAt"].Width = Math.Max(130, wDate);
        }

        // ??????????????????????????????????????????????????????????
        //  CELL PAINTING
        // ??????????????????????????????????????????????????????????
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1)
                {
                    e.Handled = true;
                    var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(e.CellBounds,
                        ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1e3a7a"),
                        LinearGradientMode.Vertical))
                        g.FillRectangle(br, e.CellBounds);
                    using (var font = new Font("Cairo", 10.5F, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds,
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sep2 = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    {
                        g.DrawLine(sep2, e.CellBounds.Left, e.CellBounds.Top + 8, e.CellBounds.Left, e.CellBounds.Bottom - 8);
                        g.DrawLine(sep2, e.CellBounds.Right - 1, e.CellBounds.Top + 8, e.CellBounds.Right - 1, e.CellBounds.Bottom - 8);
                    }
                    return;
                }
                if (e.RowIndex < 0) return;

                bool sel = dgvTransactions.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF")
                               : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                string col = dgvTransactions.Columns[e.ColumnIndex].Name;

                if (col == "ProductName") PaintProductCell(e, bg);
                else if (col == "TransactionType") PaintTypeCell(e, bg);
                else if (col == "Quantity") PaintQtyCell(e, bg);
                else if (col == "UnitCost" || col == "TotalValue") PaintPriceCell(e, bg, col);
                else if (col == "CreatedAt") PaintDateCell(e, bg);
                else { e.Handled = true; e.Graphics.FillRectangle(new SolidBrush(bg), e.CellBounds); e.PaintContent(e.CellBounds); }

                using (var wPen = new Pen(Color.White, 2f))
                {
                    e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom);
                    e.Graphics.DrawLine(wPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
                    e.Graphics.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }
                using (var linePen = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f))
                    e.Graphics.DrawLine(linePen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
            catch { }
        }

        private void PaintProductCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(bg), e.CellBounds);
            string name = e.Value?.ToString() ?? ""; if (string.IsNullOrEmpty(name)) return;
            Color[] palette = {
                ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#2563EB"),
                ColorTranslator.FromHtml("#7C3AED"), ColorTranslator.FromHtml("#0891B2"),
                ColorTranslator.FromHtml("#059669"), ColorTranslator.FromHtml("#D97706")
            };
            Color avatarColor = palette[e.RowIndex % palette.Length];
            int aw = 34, ah = 34;
            int ax = e.CellBounds.Right - 12 - aw;
            int ay = e.CellBounds.Top + (e.CellBounds.Height - ah) / 2;
            using (var br = new SolidBrush(avatarColor)) g.FillEllipse(br, ax, ay, aw, ah);
            string letter = name[0].ToString();
            using (var lf = new Font("Cairo", 12F, FontStyle.Bold))
            {
                var lsz = g.MeasureString(letter, lf);
                g.DrawString(letter, lf, Brushes.White, ax + (aw - lsz.Width) / 2f, ay + (ah - lsz.Height) / 2f);
            }
            using (var f = new Font("Cairo", 11F, FontStyle.Bold))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, f, tb,
                    new RectangleF(e.CellBounds.Left + 8, e.CellBounds.Top, ax - 10 - e.CellBounds.Left, e.CellBounds.Height),
                    new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter });
        }

        private static (Color text, Color bg, Color border) GetTypeBadge(string val)
        {
            switch (val)
            {
                case "Ê«—œ „Œ“‰": return (ColorTranslator.FromHtml("#059669"), ColorTranslator.FromHtml("#ECFDF5"), ColorTranslator.FromHtml("#A7F3D0"));
                case " Õ„Ì· ”Ì«—…": return (ColorTranslator.FromHtml("#D97706"), ColorTranslator.FromHtml("#FFFBEB"), ColorTranslator.FromHtml("#FDE68A"));
                case "„— Ã⁄": return (ColorTranslator.FromHtml("#7C3AED"), ColorTranslator.FromHtml("#F5F3FF"), ColorTranslator.FromHtml("#DDD6FE"));
                case "≈—Ã«⁄ ”Ì«—…": return (ColorTranslator.FromHtml("#9333EA"), ColorTranslator.FromHtml("#FAF5FF"), ColorTranslator.FromHtml("#E9D5FF"));
                case "’«œ—": return (ColorTranslator.FromHtml("#DC2626"), ColorTranslator.FromHtml("#FEF2F2"), ColorTranslator.FromHtml("#FECACA"));
                case "≈Ì—«œ »Ì⁄": return (ColorTranslator.FromHtml("#0369a1"), ColorTranslator.FromHtml("#F0F9FF"), ColorTranslator.FromHtml("#BAE6FD"));
                case "—’Ìœ «›  «ÕÌ": return (ColorTranslator.FromHtml("#0F172A"), ColorTranslator.FromHtml("#F8FAFC"), ColorTranslator.FromHtml("#CBD5E1"));
                case "„’—Ê› „ÊŸ›": return (ColorTranslator.FromHtml("#B45309"), ColorTranslator.FromHtml("#FFF7ED"), ColorTranslator.FromHtml("#FED7AA"));
                case "„’—Ê› ≈œ«—Ì": return (ColorTranslator.FromHtml("#BE185D"), ColorTranslator.FromHtml("#FDF2F8"), ColorTranslator.FromHtml("#FBCFE8"));
                case "≈Ìœ«⁄ Œ“‰…": return (ColorTranslator.FromHtml("#047857"), ColorTranslator.FromHtml("#ECFDF5"), ColorTranslator.FromHtml("#6EE7B7"));
                case "Œ’„ Œ“‰…": return (ColorTranslator.FromHtml("#991B1B"), ColorTranslator.FromHtml("#FFF1F2"), ColorTranslator.FromHtml("#FECDD3"));
                case "’—› —« »": return (ColorTranslator.FromHtml("#1D4ED8"), ColorTranslator.FromHtml("#EFF6FF"), ColorTranslator.FromHtml("#BFDBFE"));
                default: return (ColorTranslator.FromHtml("#6B7280"), ColorTranslator.FromHtml("#F9FAFB"), ColorTranslator.FromHtml("#E5E7EB"));
            }
        }

        private void PaintTypeCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(bg), e.CellBounds);
            string val = e.Value?.ToString() ?? "";
            var (tc, tbg, tbd) = GetTypeBadge(val);
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold))
            {
                var tsz = g.MeasureString(val, f);
                int pw = Math.Max(110, (int)tsz.Width + 24), ph = 28;
                int px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2;
                int py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
                var pill = new Rectangle(px, py, pw, ph);
                using (var path = RoundPath(pill, ph / 2)) { g.FillPath(new SolidBrush(tbg), path); g.DrawPath(new Pen(tbd, 1f), path); }
                g.FillEllipse(new SolidBrush(tc), px + 10, py + (ph - 7) / 2, 7, 7);
                g.DrawString(val, f, new SolidBrush(tc), new RectangleF(px, py, pw, ph),
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        }

        private void PaintQtyCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(new SolidBrush(bg), e.CellBounds);
            int qty = 0; try { qty = Convert.ToInt32(e.Value); } catch { }
            string typeVal = dgvTransactions.Rows[e.RowIndex].Cells["TransactionType"].Value?.ToString() ?? "";
            bool isMoney = typeVal == "≈Ì—«œ »Ì⁄" || typeVal == "≈Ìœ«⁄ Œ“‰…" || typeVal == "Œ’„ Œ“‰…"
                        || typeVal == "„’—Ê› „ÊŸ›" || typeVal == "„’—Ê› ≈œ«—Ì" || typeVal == "’—› —« »";
            int pw = 72, ph = 28;
            int px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2;
            int py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
            var pill = new Rectangle(px, py, pw, ph);
            if (isMoney)
            {
                using (var path = RoundPath(pill, ph / 2)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                using (var f = new Font("Cairo", 12F, FontStyle.Bold))
                using (var tb = new SolidBrush(ColorTranslator.FromHtml("#94A3B8")))
                    g.DrawString("ó", f, tb, new RectangleF(px, py, pw, ph),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
            else
            {
                Color bc = qty >= 0 ? ColorTranslator.FromHtml("#1d4ed8") : ColorTranslator.FromHtml("#dc2626");
                Color bbg = qty >= 0 ? ColorTranslator.FromHtml("#EFF6FF") : ColorTranslator.FromHtml("#FEF2F2");
                Color bbd = qty >= 0 ? ColorTranslator.FromHtml("#BFDBFE") : ColorTranslator.FromHtml("#FECACA");
                using (var path = RoundPath(pill, ph / 2)) { g.FillPath(new SolidBrush(bbg), path); g.DrawPath(new Pen(bbd, 1f), path); }
                using (var f = new Font("Cairo", 10.5F, FontStyle.Bold))
                using (var tb = new SolidBrush(bc))
                    g.DrawString(Math.Abs(qty).ToString("N0", Inv), f, tb, new RectangleF(px, py, pw, ph),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        }

        private void PaintPriceCell(DataGridViewCellPaintingEventArgs e, Color bg, string colName)
        {
            e.Handled = true;
            var g = e.Graphics; g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            decimal val = 0m; try { val = Convert.ToDecimal(e.Value); } catch { }
            string typeVal = dgvTransactions.Rows[e.RowIndex].Cells["TransactionType"].Value?.ToString() ?? "";
            bool isOut = typeVal == "’«œ—" || typeVal == " Õ„Ì· ”Ì«—…" || typeVal == "Œ’„ Œ“‰…"
                      || typeVal == "„’—Ê› „ÊŸ›" || typeVal == "„’—Ê› ≈œ«—Ì" || typeVal == "’—› —« »";
            Color fc = colName == "TotalValue"
                ? (isOut ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#059669"))
                : ColorTranslator.FromHtml("#374151");
            using (var f = new Font("Cairo", 11F, FontStyle.Bold))
            using (var tb = new SolidBrush(fc))
                g.DrawString(val.ToString("N2", Inv) + " Ã", f, tb, e.CellBounds,
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private void PaintDateCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.FillRectangle(new SolidBrush(bg), e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            DateTime dt = DateTime.MinValue;
            try { if (e.Value is DateTime dv) dt = dv; else if (e.Value != null) DateTime.TryParse(e.Value.ToString(), out dt); } catch { }
            if (dt != DateTime.MinValue)
            {
                if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
                else if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            }
            string display = dt == DateTime.MinValue ? "" : dt.ToString("yyyy/MM/dd  HH:mm", Inv);
            using (var f = new Font("Cairo", 10F))
            using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                g.DrawString(display, f, tb, e.CellBounds,
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
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

    // ??????????????????????????????????????????????????????????
    //  DATE PICKER FORM ó popup «Œ Ì«— «· «—ÌŒ
    // ??????????????????????????????????????????????????????????
    public class DatePickerForm : Form
    {
        public DateTime SelectedDate { get; private set; } = DateTime.Today;
        private DateTimePicker dtPicker;

        public DatePickerForm()
        {
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            Text = "ÿ»«⁄…  ﬁ—Ì— «·”Ã·";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(360, 180);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = ColorTranslator.FromHtml("#F8FAFC");

            var lblDate = new Label
            {
                Text = "«Œ —  «—ÌŒ «· ﬁ—Ì—:",
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                Location = new Point(12, 16),
                AutoSize = true
            };

            dtPicker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Cairo", 11F),
                Location = new Point(12, 42),
                Width = 320
            };

            var btnOk = new Button
            {
                Text = "ÿ»«⁄… / Õ›Ÿ PDF",
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                Size = new Size(150, 36),
                Location = new Point(182, 90),
                BackColor = ColorTranslator.FromHtml("#1a2f5e"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                SelectedDate = dtPicker.Value.Date;
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = new Button
            {
                Text = "≈·€«¡",
                Font = new Font("Cairo", 10F),
                Size = new Size(80, 36),
                Location = new Point(94, 90),
                BackColor = ColorTranslator.FromHtml("#E2E8F0"),
                ForeColor = ColorTranslator.FromHtml("#374151"),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(lblDate);
            Controls.Add(dtPicker);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }
}