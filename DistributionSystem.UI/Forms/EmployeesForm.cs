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
    public partial class EmployeesForm : Form
    {
        private readonly EmployeeService _service;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private List<EmployeeDto> _employees = new List<EmployeeDto>();
        private List<AdminExpenseDto> _expenses = new List<AdminExpenseDto>();

        private Guna2DataGridView dgvEmployees;
        private Guna2DataGridView dgvExpenses;
        private Label lblEmpCount;
        private Label lblExpCount;

        private decimal _totalPaid;
        private decimal _totalExpenses;
        private Panel _summaryPanel;

        private static readonly PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent)
        {
            foreach (Control c in parent.Controls)
            { try { _dbProp?.SetValue(c, true); } catch { } if (c.Controls.Count > 0) EnableDbAll(c); }
        }
        private static readonly SolidBrush _brWhite = new SolidBrush(Color.White);
        private static readonly StringFormat _sfCenter = new StringFormat
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        private GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2; var p = new GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        public EmployeesForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            UpdateStyles();
            _service = new EmployeeService();
            BuildUI();
            Shown += (s, e) => BeginInvoke(new Action(LoadAll));
        }

        // ??????????????????????????????????????????????????????
        //  BUILD UI
        // ??????????????????????????????????????????????????????
        private void BuildUI()
        {
            SuspendLayout();
            RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(0);
            foreach (Control c in Controls) c.Visible = false;

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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildSummary(), 0, 1);
            root.Controls.Add(BuildTables(), 0, 2);
            root.ResumeLayout(false);
            EnableDbAll(root);
            Controls.Add(root); root.BringToFront();
            ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  HEADER
        // ??????????????????????????????????????????????????????
        private Panel BuildHeader()
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
                    for (int x = 10; x < banner.Width; x += 22)
                        for (int y = 8; y < banner.Height; y += 22)
                            g.FillEllipse(dot, x, y, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                { g.FillEllipse(cb, banner.Width - 130, -50, 220, 220); g.FillEllipse(cb, banner.Width - 30, 20, 160, 160); }

                using (var tf = new Font("Cairo", 17F, FontStyle.Bold))
                using (var sf2 = new Font("Cairo", 10.5F))
                {
                    string title = "≈œ«—… «·„ÊŸ›Ì‰"; string sub = "⁄—÷ Ê≈œ«—… «·„ÊŸ›Ì‰ Ê«·”·› Ê«·„’«—Ì›";
                    var szT = g.MeasureString(title, tf); var szS = g.MeasureString(sub, sf2);
                    float gap = 4f, block = szT.Height + gap + szS.Height, startY = (banner.Height - block) / 2f;
                    using (var tb = new SolidBrush(Color.White)) g.DrawString(title, tf, tb, banner.Width - szT.Width - 20, startY);
                    using (var sb2 = new SolidBrush(Color.FromArgb(220, 255, 255, 255))) g.DrawString(sub, sf2, sb2, banner.Width - szS.Width - 20, startY + szT.Height + gap);
                    float lineY = startY + szT.Height + gap + szS.Height + 4f;
                    using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6"))) g.FillRectangle(b1, banner.Width - 44, lineY, 40, 3);
                    using (var b2 = new SolidBrush(Color.FromArgb(140, 100, 181, 246))) g.FillRectangle(b2, banner.Width - 62, lineY, 14, 3);
                }
            };

            var btnAddEmp = new Guna2Button
            {
                Text = "+ „ÊŸ› ÃœÌœ",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(140, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(20, 3)
            };
            btnAddEmp.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAddEmp.Click += (s, e) => ShowEmployeePopup();

            var btnAddExp = new Guna2Button
            {
                Text = "+ „’—Ê› «œ«—Ì",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(152, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(170, 3)
            };
            btnAddExp.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnAddExp.Click += (s, e) => ShowExpensePopup();

            banner.Controls.Add(btnAddEmp);
            banner.Controls.Add(btnAddExp);
            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  SUMMARY
        // ??????????????????????????????????????????????????????
        private Panel BuildSummary()
        {
            _summaryPanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorTranslator.FromHtml("#EEF0F5"), Padding = new Padding(10, 6, 10, 0) };
            _summaryPanel.Paint += SummaryPaint;
            return _summaryPanel;
        }

        private void SummaryPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = _summaryPanel.ClientSize.Width, H = _summaryPanel.ClientSize.Height;
            int gap = 10, cw = (W - gap * 4) / 3;

            (string lbl, string val, string fg, string bg, string border)[] cards =
            {
                ("«Ã„«·Ì „œ›Ê⁄ ··„ÊŸ›Ì‰",   _totalPaid.ToString("N2", Inv) + " Ã",   "#1a2f5e", "#EFF6FF", "#BFDBFE"),
                ("«Ã„«·Ì «·„’«—Ì› «·«œ«—Ì…", _totalExpenses.ToString("N2", Inv) + " Ã","#065F46", "#ECFDF5", "#A7F3D0"),
                ("«·«Ã„«·Ì «·ﬂ·Ì",           (_totalPaid + _totalExpenses).ToString("N2", Inv) + " Ã", "#7C3AED", "#F5F3FF", "#C4B5FD"),
            };

            for (int i = 0; i < cards.Length; i++)
            {
                var it = cards[i];
                int cx = gap + i * (cw + gap);
                var rc = new Rectangle(cx, 2, cw, H - 4);
                using (var sh = new SolidBrush(Color.FromArgb(10, 0, 0, 80)))
                    g.FillRectangle(sh, rc.X + 2, rc.Y + 2, rc.Width, rc.Height);
                using (var path = RoundPath(rc, 10))
                { g.FillPath(new SolidBrush(ColorTranslator.FromHtml(it.bg)), path); g.DrawPath(new Pen(ColorTranslator.FromHtml(it.border), 1f), path); }
                using (var lf = new Font("Cairo", 9F)) using (var lb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                    g.DrawString(it.lbl, lf, lb, new RectangleF(rc.X, rc.Y + 4, rc.Width, 18), _sfCenter);
                using (var vf = new Font("Cairo", 13F, FontStyle.Bold)) using (var vb = new SolidBrush(ColorTranslator.FromHtml(it.fg)))
                    g.DrawString(it.val, vf, vb, new RectangleF(rc.X, rc.Y + 22, rc.Width, rc.Height - 22), _sfCenter);
            }
        }

        // ??????????????????????????????????????????????????????
        //  TABLES
        // ??????????????????????????????????????????????????????
        private Control BuildTables()
        {
            var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = ColorTranslator.FromHtml("#EEF0F5"), Padding = new Padding(10, 4, 10, 10) };
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            split.Controls.Add(BuildEmpCard(), 0, 0);
            split.Controls.Add(BuildExpCard(), 1, 0);
            wrapper.Controls.Add(split);
            return wrapper;
        }

        // ??????????????????????????????????????????????????????
        //  EMPLOYEES CARD
        // ??????????????????????????????????????????????????????
        private Control BuildEmpCard()
        {
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 5, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblEmpCount = new Label { Text = "0 „ÊŸ›", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblEmpCount.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblEmpCount);
            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("ﬁ«∆„… «·„ÊŸ›Ì‰", tf); g.DrawString("ﬁ«∆„… «·„ÊŸ›Ì‰", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
                string badge = lblEmpCount?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                { var bsz = g.MeasureString(badge, bf); int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2; var brc = new Rectangle(bx, by, bw, bh); using (var path = RoundPath(brc, bh / 2)) using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical)) g.FillPath(br, path); g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
            };

            var searchSep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSep.Paint += (s, pe) =>
            { using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSep.Width, 3), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal)) pe.Graphics.FillRectangle(br, 0, 0, searchSep.Width, 3); };

            dgvEmployees = MakeDgv();
            dgvEmployees.RowTemplate.Height = 76;

            dgvEmployees.Columns.Clear();
            void AE(string n, string h, string p, int w) =>
                dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Cairo", 13F, FontStyle.Bold) } });
            AE("EId", "„", "Id", 0);
            AE("EName", "«·«”„", "Name", 160);
            AE("EJob", "«·„”„Ï", "JobTitle", 110); // ? ⁄„Êœ «·„”„Ï «·ÊŸÌ›Ì
            AE("ESal", "«·„— »", "Salary", 90);
            AE("EBal", "«·—’Ìœ", "RemainingBalance", 90);
            AE("ELoan", "«·”·›", "TotalLoans", 80);
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "EAct", HeaderText = "«·«Ã—«¡« ", Width = 165, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvEmployees.Columns["EId"].Visible = false;

            dgvEmployees.CellPainting += DgvEmp_CellPainting;
            dgvEmployees.CellClick += DgvEmp_CellClick;
            dgvEmployees.Resize += (s, e) => FitEmpCols();

            var dgvWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrap.Controls.Add(dgvEmployees);

            container.Controls.Add(dgvWrap);
            container.Controls.Add(searchSep);
            container.Controls.Add(topBar);
            outer.Controls.Add(container);
            return outer;
        }

        // ??????????????????????????????????????????????????????
        //  EXPENSES CARD
        // ??????????????????????????????????????????????????????
        private Control BuildExpCard()
        {
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(5, 0, 0, 0) };
            var container = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 18, BorderThickness = 0 };
            container.ShadowDecoration.Enabled = true; container.ShadowDecoration.Depth = 20; container.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White };
            lblExpCount = new Label { Text = "0 „’—Ê›", BackColor = Color.Transparent, ForeColor = Color.Transparent, AutoSize = false, Size = new Size(1, 1), Location = new Point(-100, -100) };
            lblExpCount.TextChanged += (s, e) => topBar.Invalidate();
            topBar.Controls.Add(lblExpCount);
            topBar.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int W = topBar.Width, H = topBar.Height;
                using (var tf = new Font("Cairo", 15F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                { var sz = g.MeasureString("«·„’«—Ì› «·«œ«—Ì…", tf); g.DrawString("«·„’«—Ì› «·«œ«—Ì…", tf, tb, (W - sz.Width) / 2f, (H - sz.Height) / 2f); }
                string badge = lblExpCount?.Text ?? "";
                using (var bf = new Font("Cairo", 11F, FontStyle.Bold))
                { var bsz = g.MeasureString(badge, bf); int bw = (int)bsz.Width + 24, bh = 34, bx = W - bw - 20, by = (H - bh) / 2; var brc = new Rectangle(bx, by, bw, bh); using (var path = RoundPath(brc, bh / 2)) using (var br = new LinearGradientBrush(brc, ColorTranslator.FromHtml("#059669"), ColorTranslator.FromHtml("#047857"), LinearGradientMode.Vertical)) g.FillPath(br, path); g.DrawString(badge, bf, Brushes.White, new RectangleF(bx, by, bw, bh), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); }
            };

            var searchSep = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            searchSep.Paint += (s, pe) =>
            { using (var br = new LinearGradientBrush(new Rectangle(0, 0, searchSep.Width, 3), ColorTranslator.FromHtml("#059669"), ColorTranslator.FromHtml("#E2E8F0"), LinearGradientMode.Horizontal)) pe.Graphics.FillRectangle(br, 0, 0, searchSep.Width, 3); };

            dgvExpenses = MakeDgv();
            dgvExpenses.RowTemplate.Height = 76;

            dgvExpenses.Columns.Clear();
            void AX(string n, string h, string p, int w) =>
                dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = n, HeaderText = h, DataPropertyName = p, Width = w, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Cairo", 13F, FontStyle.Bold) } });
            AX("XId", "„", "Id", 0);
            AX("XDesc", "«·»‰œ", "Description", 150);
            AX("XAmt", "«·„»·€", "Amount", 110);
            AX("XDate", "«· «—ÌŒ", "CreatedAt", 110);
            dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "XAct", HeaderText = "Õ–›", Width = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgvExpenses.Columns["XId"].Visible = false;

            dgvExpenses.CellPainting += DgvExp_CellPainting;
            dgvExpenses.CellClick += DgvExp_CellClick;
            dgvExpenses.Resize += (s, e) => FitExpCols();

            var dgvWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrap.Controls.Add(dgvExpenses);

            container.Controls.Add(dgvWrap);
            container.Controls.Add(searchSep);
            container.Controls.Add(topBar);
            outer.Controls.Add(container);
            return outer;
        }

        // ??????????????????????????????????????????????????????
        //  DATA
        // ??????????????????????????????????????????????????????
        private void LoadAll()
        {
            try
            {
                _employees = _service.GetAllEmployees() ?? new List<EmployeeDto>();
                _expenses = _service.GetAllExpenses() ?? new List<AdminExpenseDto>();
                _totalPaid = _service.GetTotalPaidToEmployees();
                _totalExpenses = _service.GetTotalAdminExpenses();

                dgvEmployees.DataSource = new BindingSource { DataSource = new List<EmployeeDto>(_employees) };
                dgvExpenses.DataSource = new BindingSource { DataSource = new List<AdminExpenseDto>(_expenses) };

                FitEmpCols(); FitExpCols();
                if (lblEmpCount != null) lblEmpCount.Text = $"{_employees.Count} „ÊŸ›";
                if (lblExpCount != null) lblExpCount.Text = $"{_expenses.Count} „’—Ê›";
                _summaryPanel?.Invalidate();
            }
            catch (Exception ex) { ShowErrorToast("›‘·  Õ„Ì· «·»Ì«‰« : " + GetInner(ex)); }
        }

        private void FitEmpCols()
        {
            if (dgvEmployees == null || dgvEmployees.Columns.Count == 0) return;
            int w = dgvEmployees.ClientSize.Width; if (w <= 0) return;
            // ?  Ê“Ì⁄ «·√⁄„œ… „⁄ ≈÷«›… EJob
            dgvEmployees.Columns["EName"].Width = Math.Max(90, (int)(w * 0.22));
            dgvEmployees.Columns["EJob"].Width = Math.Max(80, (int)(w * 0.18)); // ?
            dgvEmployees.Columns["ESal"].Width = Math.Max(60, (int)(w * 0.14));
            dgvEmployees.Columns["EBal"].Width = Math.Max(60, (int)(w * 0.14));
            dgvEmployees.Columns["ELoan"].Width = Math.Max(50, (int)(w * 0.11));
            dgvEmployees.Columns["EAct"].Width = w
                - dgvEmployees.Columns["EName"].Width
                - dgvEmployees.Columns["EJob"].Width
                - dgvEmployees.Columns["ESal"].Width
                - dgvEmployees.Columns["EBal"].Width
                - dgvEmployees.Columns["ELoan"].Width - 2;
        }

        private void FitExpCols()
        {
            if (dgvExpenses == null || dgvExpenses.Columns.Count == 0) return;
            int w = dgvExpenses.ClientSize.Width; if (w <= 0) return;
            dgvExpenses.Columns["XDesc"].Width = Math.Max(90, (int)(w * 0.38));
            dgvExpenses.Columns["XAmt"].Width = Math.Max(70, (int)(w * 0.24));
            dgvExpenses.Columns["XDate"].Width = Math.Max(70, (int)(w * 0.24));
            dgvExpenses.Columns["XAct"].Width = w - dgvExpenses.Columns["XDesc"].Width - dgvExpenses.Columns["XAmt"].Width - dgvExpenses.Columns["XDate"].Width - 2;
        }

        // ??????????????????????????????????????????????????????
        //  CELL PAINTING ó Employees
        // ??????????????????????????????????????????????????????
        private void DgvEmp_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1) { PaintHeader(e); return; }
                if (e.RowIndex < 0) return;
                bool sel = dgvEmployees.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                string col = dgvEmployees.Columns[e.ColumnIndex].Name;

                if (col == "EName") PaintNameCell(e, bg, dgvEmployees);
                else if (col == "EJob") PaintJobCell(e, bg);   // ?  ·ÊÌ‰ «·„”„Ï «·ÊŸÌ›Ì
                else if (col == "EBal") PaintBalanceCell(e, bg);
                else if (col == "EAct") PaintEmpActions(e, bg);
                else
                {
                    e.Handled = true;
                    e.Graphics.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
                    e.PaintContent(e.CellBounds);
                }

                using (var wp = new Pen(Color.White, 2f))
                { e.Graphics.DrawLine(wp, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom); e.Graphics.DrawLine(wp, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom); e.Graphics.DrawLine(wp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1); }
                using (var dp = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f))
                    e.Graphics.DrawLine(dp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
            catch { }
        }

        private void PaintHeader(DataGridViewCellPaintingEventArgs e)
        {
            e.Handled = true;
            using (var br = new LinearGradientBrush(e.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(br, e.CellBounds);
            using (var f = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                e.Graphics.DrawString(e.Value?.ToString() ?? "", f, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
            { e.Graphics.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); e.Graphics.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
        }

        private void PaintNameCell(DataGridViewCellPaintingEventArgs e, Color bg, Guna2DataGridView dgv)
        {
            e.Handled = true;
            var g = e.Graphics; g.SetClip(e.CellBounds);
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var dto = dgv.Rows[e.RowIndex].DataBoundItem as EmployeeDto;
            string name = dto?.Name ?? ""; if (string.IsNullOrEmpty(name)) { g.ResetClip(); return; }

            var avColors = new[] { "#4E73DF", "#10B981", "#F59E0B", "#8B5CF6", "#EF4444", "#0891B2", "#DC2626" };
            int avSize = 36, pad = 14;
            int avX = e.CellBounds.Right - avSize - pad;
            int avY = e.CellBounds.Top + (e.CellBounds.Height - avSize) / 2;
            using (var sh = new SolidBrush(Color.FromArgb(20, 0, 0, 0))) g.FillEllipse(sh, avX + 2, avY + 2, avSize, avSize);
            using (var avBr = new SolidBrush(ColorTranslator.FromHtml(avColors[e.RowIndex % avColors.Length]))) g.FillEllipse(avBr, avX, avY, avSize, avSize);
            string letter = name[0].ToString();
            using (var lf = new Font("Cairo", 13F, FontStyle.Bold)) { var ls = g.MeasureString(letter, lf); g.DrawString(letter, lf, Brushes.White, avX + (avSize - ls.Width) / 2f, avY + (avSize - ls.Height) / 2f); }
            float textW = (avX - 8f) - e.CellBounds.Left - 4f;
            using (var nf = new Font("Cairo", 13F, FontStyle.Bold)) using (var nb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                g.DrawString(name, nf, nb, new RectangleF(e.CellBounds.Left + 4, e.CellBounds.Top, textW, e.CellBounds.Height), _sfCenter);
            g.ResetClip();
        }

        // ? Œ·Ì… «·„”„Ï «·ÊŸÌ›Ì ó badge »·Ê‰ »‰›”ÃÌ ›« Õ
        private void PaintJobCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);

            string job = e.Value?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(job))
            {
                using (var f = new Font("Cairo", 11F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#CBD5E1")))
                    g.DrawString("ó", f, tb, e.CellBounds, _sfCenter);
                return;
            }

            using (var f = new Font("Cairo", 10F, FontStyle.Bold))
            {
                var sz = g.MeasureString(job, f);
                int pw = Math.Min((int)sz.Width + 14, e.CellBounds.Width - 8);
                int ph = 26;
                int px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2;
                int py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2;
                using (var path = RoundPath(new Rectangle(px, py, pw, ph), ph / 2))
                {
                    g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EDE9FE")), path);
                    g.DrawPath(new Pen(ColorTranslator.FromHtml("#C4B5FD"), 1f), path);
                }
                g.DrawString(job, f, new SolidBrush(ColorTranslator.FromHtml("#6D28D9")),
                    new RectangleF(px, py, pw, ph), _sfCenter);
            }
        }

        private void PaintBalanceCell(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            var dto = dgvEmployees.Rows[e.RowIndex].DataBoundItem as EmployeeDto; if (dto == null) return;
            bool zero = dto.RemainingBalance <= 0;
            string text = dto.RemainingBalance.ToString("N2", Inv);
            Color fg = zero ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#059669");
            Color bgBadge = zero ? ColorTranslator.FromHtml("#FEE2E2") : ColorTranslator.FromHtml("#DCFCE7");
            using (var f = new Font("Cairo", 13F, FontStyle.Bold))
            { var sz = g.MeasureString(text, f); int pw = (int)sz.Width + 16, ph = 30, px = e.CellBounds.Left + (e.CellBounds.Width - pw) / 2, py = e.CellBounds.Top + (e.CellBounds.Height - ph) / 2; using (var path = RoundPath(new Rectangle(px, py, pw, ph), ph / 2)) g.FillPath(new SolidBrush(bgBadge), path); g.DrawString(text, f, new SolidBrush(fg), new RectangleF(px, py, pw, ph), _sfCenter); }
        }

        private void PaintEmpActions(DataGridViewCellPaintingEventArgs e, Color bg)
        {
            e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
            int bH = 32, bY = e.CellBounds.Top + (e.CellBounds.Height - bH) / 2;
            int editW = 62, loanW = 52, delW = 32, gap = 7;
            int total = editW + gap + loanW + gap + delW;
            int sx = e.CellBounds.Left + (e.CellBounds.Width - total) / 2;

            var editRc = new Rectangle(sx, bY, editW, bH);
            using (var path = RoundPath(editRc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#EFF6FF")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#2563EB")))
            { var sz = g.MeasureString(" ⁄œÌ·", f); g.DrawString(" ⁄œÌ·", f, tb, editRc.Left + (editRc.Width - sz.Width) / 2f, editRc.Top + (editRc.Height - sz.Height) / 2f); }

            var loanRc = new Rectangle(sx + editW + gap, bY, loanW, bH);
            using (var path = RoundPath(loanRc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#ECFDF5")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#A7F3D0"), 1f), path); }
            using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#065F46")))
            { var sz = g.MeasureString("”·›…", f); g.DrawString("”·›…", f, tb, loanRc.Left + (loanRc.Width - sz.Width) / 2f, loanRc.Top + (loanRc.Height - sz.Height) / 2f); }

            var delRc = new Rectangle(sx + editW + gap + loanW + gap, bY, delW, bH);
            using (var path = RoundPath(delRc, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path); }
            using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
            { int cx = delRc.Left + delRc.Width / 2, cy = delRc.Top + delRc.Height / 2; g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4); g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6); g.DrawRectangle(pen, cx - 4, cy - 3, 8, 7); g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 3); g.DrawLine(pen, cx + 1, cy - 1, cx + 1, cy + 3); }
            g.ResetClip();
        }

        private void DgvEmp_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvEmployees.Columns[e.ColumnIndex].Name != "EAct") return;
            var dto = dgvEmployees.Rows[e.RowIndex].DataBoundItem as EmployeeDto; if (dto == null) return;
            var cell = dgvEmployees.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var mouse = dgvEmployees.PointToClient(Cursor.Position);
            int bH = 32, bY = cell.Top + (cell.Height - bH) / 2;
            int editW = 62, loanW = 52, delW = 32, gap = 7;
            int total = editW + gap + loanW + gap + delW;
            int sx = cell.Left + (cell.Width - total) / 2;

            if (new Rectangle(sx, bY, editW, bH).Contains(mouse)) ShowEmployeePopup(dto);
            else if (new Rectangle(sx + editW + gap, bY, loanW, bH).Contains(mouse)) ShowLoanPopup(dto);
            else if (new Rectangle(sx + editW + gap + loanW + gap, bY, delW, bH).Contains(mouse))
            {
                if (MessageBox.Show($"Õ–› «·„ÊŸ› \"{dto.Name}\"ø", " «ﬂÌœ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                { try { _service.DeleteEmployee(dto.Id); LoadAll(); ShowSuccessToast(" „ Õ–› «·„ÊŸ›"); } catch (Exception ex) { ShowErrorToast(GetInner(ex)); } }
            }
        }

        // ??????????????????????????????????????????????????????
        //  CELL PAINTING ó Expenses
        // ??????????????????????????????????????????????????????
        private void DgvExp_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            try
            {
                if (e.RowIndex == -1) { PaintHeader(e); return; }
                if (e.RowIndex < 0) return;
                bool sel = dgvExpenses.Rows[e.RowIndex].Selected;
                Color bg = sel ? ColorTranslator.FromHtml("#EEF2FF") : (e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#FAFBFF"));
                string col = dgvExpenses.Columns[e.ColumnIndex].Name;

                if (col == "XAct")
                {
                    e.Handled = true; var g = e.Graphics; g.SetClip(e.CellBounds); g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
                    int bW = 36, bH = 32, bx = e.CellBounds.Left + (e.CellBounds.Width - bW) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bH) / 2;
                    var dr = new Rectangle(bx, by, bW, bH);
                    using (var path = RoundPath(dr, 8)) { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#FEF2F2")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#FECACA"), 1f), path); }
                    using (var pen = new Pen(ColorTranslator.FromHtml("#EF4444"), 1.6f))
                    { int cx = dr.Left + dr.Width / 2, cy = dr.Top + dr.Height / 2; g.DrawLine(pen, cx - 5, cy - 4, cx + 5, cy - 4); g.DrawLine(pen, cx - 2, cy - 6, cx + 2, cy - 6); g.DrawRectangle(pen, cx - 4, cy - 3, 8, 7); g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 3); g.DrawLine(pen, cx + 1, cy - 1, cx + 1, cy + 3); }
                    g.ResetClip();
                }
                else if (col == "XDate")
                {
                    e.Handled = true; e.Graphics.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds);
                    string dt = e.Value is DateTime d ? d.ToString("yyyy/MM/dd") : e.Value?.ToString() ?? "";
                    using (var f = new Font("Cairo", 13F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                        e.Graphics.DrawString(dt, f, tb, e.CellBounds, _sfCenter);
                }
                else { e.Handled = true; e.Graphics.FillRectangle(bg == Color.White ? _brWhite : new SolidBrush(bg), e.CellBounds); e.PaintContent(e.CellBounds); }

                using (var wp = new Pen(Color.White, 2f))
                { e.Graphics.DrawLine(wp, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom); e.Graphics.DrawLine(wp, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom); e.Graphics.DrawLine(wp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1); }
                using (var dp = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f))
                    e.Graphics.DrawLine(dp, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }
            catch { }
        }

        private void DgvExp_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvExpenses.Columns[e.ColumnIndex].Name != "XAct") return;
            var dto = dgvExpenses.Rows[e.RowIndex].DataBoundItem as AdminExpenseDto; if (dto == null) return;
            if (MessageBox.Show($"Õ–› «·„’—Ê› \"{dto.Description}\"ø", " «ﬂÌœ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            { try { _service.DeleteExpense(dto.Id); LoadAll(); ShowSuccessToast(" „ Õ–› «·„’—Ê›"); } catch (Exception ex) { ShowErrorToast(GetInner(ex)); } }
        }

        // ??????????????????????????????????????????????????????
        //  POPUP ó Employee (Add / Edit) ? ? √÷›‰« Õﬁ· JobTitle
        // ??????????????????????????????????????????????????????
        private void ShowEmployeePopup(EmployeeDto edit = null)
        {
            bool isEdit = edit != null;
            var (pf, popup, overlay) = CreatePopup(500, 490); // ? “Êœ‰« «·«— ›«⁄ ‘ÊÌ… ··Õﬁ· «·ÃœÌœ
            Action close = () => { try { pf.Close(); pf.Dispose(); } catch { } };

            var head = BuildPopupHeader(pf.Width, isEdit ? " ⁄œÌ· »Ì«‰«  „ÊŸ›" : "«÷«›… „ÊŸ› ÃœÌœ",
                isEdit ? "⁄œ· «·»Ì«‰«  À„ «÷€ÿ  ÕœÌÀ" : "«œŒ· »Ì«‰«  «·„ÊŸ› «·ÃœÌœ À„ «÷€ÿ Õ›Ÿ", close);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8), RightToLeft = RightToLeft.Yes };

            var wrapName = MakeTxtWrapped("„À«·: «Õ„œ „Õ„œ", out var fName);
            var wrapJob = MakeTxtWrapped("„À«·: „‰œÊ» „»Ì⁄« ", out var fJob);  // ? Õﬁ· «·„”„Ï «·ÊŸÌ›Ì
            var wrapSal = MakeTxtWrapped("«·„— » »«·Ã‰ÌÂ", out var fSal);
            var wrapNotes = MakeTxtWrapped("„·«ÕŸ«  («Œ Ì«—Ì)", out var fNotes);

            if (isEdit)
            {
                fName.Text = edit.Name;
                fJob.Text = edit.JobTitle;  // ?
                fSal.Text = edit.Salary.ToString("N2", Inv);
                fNotes.Text = edit.Notes;
            }

            body.Controls.Add(Sp(16));
            body.Controls.Add(wrapNotes); body.Controls.Add(MakeLbl("„·«ÕŸ« "));
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapSal); body.Controls.Add(MakeLbl("«·„— » *"));
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapJob); body.Controls.Add(MakeLbl("«·„”„Ï «·ÊŸÌ›Ì")); // ?
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapName); body.Controls.Add(MakeLbl("«”„ «·„ÊŸ› *"));

            var (footer, btnSave) = BuildPopupFooter(isEdit ? " ÕœÌÀ «·„ÊŸ›" : "Õ›Ÿ «·„ÊŸ›");
            btnSave.Click += async (s, e) =>
            {
                fName.BorderColor = fSal.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                bool ok = true;
                if (string.IsNullOrWhiteSpace(fName.Text)) { fName.BorderColor = ColorTranslator.FromHtml("#EF4444"); ok = false; }
                if (!decimal.TryParse(fSal.Text, NumberStyles.Any, Inv, out decimal sal) || sal <= 0) { fSal.BorderColor = ColorTranslator.FromHtml("#EF4444"); ok = false; }
                if (!ok) return;
                btnSave.Enabled = false; btnSave.Text = "Ã«— «·Õ›Ÿ...";
                try
                {
                    var dto = new EmployeeDto
                    {
                        Id = edit?.Id ?? 0,
                        Name = fName.Text.Trim(),
                        JobTitle = fJob.Text.Trim(),   // ?
                        Salary = sal,
                        Notes = fNotes.Text.Trim()
                    };
                    if (!isEdit) await Task.Run(() => _service.AddEmployee(dto));
                    else await Task.Run(() => _service.UpdateEmployee(dto));
                    LoadAll(); close(); ShowSuccessToast(isEdit ? " „  ÕœÌÀ «·„ÊŸ›" : " „  «÷«›… «·„ÊŸ›");
                }
                catch (Exception ex) { ShowErrorToast(GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = isEdit ? " ÕœÌÀ «·„ÊŸ›" : "Õ›Ÿ «·„ÊŸ›"; }
            };

            popup.Controls.Add(body); popup.Controls.Add(footer); popup.Controls.Add(head);
            pf.FormClosed += (s, e2) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e2) => close();
            pf.Shown += (s, e2) => fName.Focus();
            pf.ShowDialog(this);
        }

        private void ShowLoanPopup(EmployeeDto emp)
        {
            var (pf, popup, overlay) = CreatePopup(500, 400);
            Action close = () => { try { pf.Close(); pf.Dispose(); } catch { } };

            var head = BuildPopupHeader(pf.Width, "’—› ”·›…",
                $"«·„ÊŸ›: {emp.Name}  |  «·—’Ìœ: {emp.RemainingBalance:N2} Ã", close);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8), RightToLeft = RightToLeft.Yes };

            var balWrap = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.Transparent };
            balWrap.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 4, balWrap.Width - 1, 38);
                bool zero = emp.RemainingBalance <= 0;
                using (var path = RoundPath(rc, 8))
                { g.FillPath(new SolidBrush(zero ? ColorTranslator.FromHtml("#FEF2F2") : ColorTranslator.FromHtml("#ECFDF5")), path); g.DrawPath(new Pen(zero ? ColorTranslator.FromHtml("#FECACA") : ColorTranslator.FromHtml("#A7F3D0"), 1f), path); }
                string txt = zero ? "—’Ìœ «·„ÊŸ› ’›— - ·« Ì„ﬂ‰ ’—› ”·›…" : $"«·—’Ìœ «·„ «Õ: {emp.RemainingBalance:N2} Ã‰ÌÂ";
                using (var f = new Font("Cairo", 11F, FontStyle.Bold)) using (var tb = new SolidBrush(zero ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#065F46")))
                    g.DrawString(txt, f, tb, rc, _sfCenter);
            };

            var wrapAmt = MakeTxtWrapped("«·„»·€ »«·Ã‰ÌÂ", out var fAmt);
            var wrapNotes = MakeTxtWrapped("„·«ÕŸ«  («Œ Ì«—Ì)", out var fNotes);

            body.Controls.Add(Sp(16));
            body.Controls.Add(wrapNotes); body.Controls.Add(MakeLbl("„·«ÕŸ« "));
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapAmt); body.Controls.Add(MakeLbl("„»·€ «·”·›… *"));
            body.Controls.Add(Sp(6)); body.Controls.Add(balWrap);

            var (footer, btnSave) = BuildPopupFooter("’—› «·”·›…");
            btnSave.FillColor = ColorTranslator.FromHtml("#065F46");
            btnSave.HoverState.FillColor = ColorTranslator.FromHtml("#047857");
            if (emp.RemainingBalance <= 0) btnSave.Enabled = false;

            btnSave.Click += async (s, e) =>
            {
                fAmt.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                if (!decimal.TryParse(fAmt.Text, NumberStyles.Any, Inv, out decimal amt) || amt <= 0) { fAmt.BorderColor = ColorTranslator.FromHtml("#EF4444"); return; }
                if (amt > emp.RemainingBalance) { fAmt.BorderColor = ColorTranslator.FromHtml("#EF4444"); ShowErrorToast($"«·—’Ìœ «·„ «Õ {emp.RemainingBalance:N2} Ã‰ÌÂ ›ﬁÿ"); return; }
                btnSave.Enabled = false; btnSave.Text = "Ã«— «·’—›...";
                try
                {
                    var dto = new EmployeeLoanDto { EmployeeId = emp.Id, Amount = amt, Notes = fNotes.Text.Trim() };
                    await Task.Run(() => _service.AddLoan(dto));
                    LoadAll(); close(); ShowSuccessToast($" „ ’—› {amt:N2} Ã ··„ÊŸ› {emp.Name}");
                }
                catch (Exception ex) { ShowErrorToast(GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = "’—› «·”·›…"; }
            };

            popup.Controls.Add(body); popup.Controls.Add(footer); popup.Controls.Add(head);
            pf.FormClosed += (s, e2) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e2) => close();
            pf.Shown += (s, e2) => fAmt.Focus();
            pf.ShowDialog(this);
        }

        private void ShowExpensePopup()
        {
            var (pf, popup, overlay) = CreatePopup(500, 390);
            Action close = () => { try { pf.Close(); pf.Dispose(); } catch { } };

            var head = BuildPopupHeader(pf.Width, "«÷«›… „’—Ê› «œ«—Ì", "«œŒ· »‰œ «·„’—Ê› Ê«·„»·€ À„ «÷€ÿ Õ›Ÿ", close);
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8), RightToLeft = RightToLeft.Yes };

            var wrapDesc = MakeTxtWrapped("„À«·: «ÌÃ«— „Œ“‰", out var fDesc);
            var wrapAmt = MakeTxtWrapped("«·„»·€ »«·Ã‰ÌÂ", out var fAmt);

            body.Controls.Add(Sp(16));
            body.Controls.Add(wrapAmt); body.Controls.Add(MakeLbl("«·„»·€ *"));
            body.Controls.Add(Sp(6)); body.Controls.Add(wrapDesc); body.Controls.Add(MakeLbl("«·»‰œ *"));

            var (footer, btnSave) = BuildPopupFooter("Õ›Ÿ «·„’—Ê›");
            btnSave.Click += async (s, e) =>
            {
                fDesc.BorderColor = fAmt.BorderColor = ColorTranslator.FromHtml("#C7D2FE");
                bool ok = true;
                if (string.IsNullOrWhiteSpace(fDesc.Text)) { fDesc.BorderColor = ColorTranslator.FromHtml("#EF4444"); ok = false; }
                if (!decimal.TryParse(fAmt.Text, NumberStyles.Any, Inv, out decimal amt) || amt <= 0) { fAmt.BorderColor = ColorTranslator.FromHtml("#EF4444"); ok = false; }
                if (!ok) return;
                btnSave.Enabled = false; btnSave.Text = "Ã«— «·Õ›Ÿ...";
                try
                {
                    await Task.Run(() => _service.AddExpense(new AdminExpenseDto { Description = fDesc.Text.Trim(), Amount = amt }));
                    LoadAll(); close(); ShowSuccessToast(" „  «÷«›… «·„’—Ê›");
                }
                catch (Exception ex) { ShowErrorToast(GetInner(ex)); }
                finally { btnSave.Enabled = true; btnSave.Text = "Õ›Ÿ «·„’—Ê›"; }
            };

            popup.Controls.Add(body); popup.Controls.Add(footer); popup.Controls.Add(head);
            pf.FormClosed += (s, e2) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e2) => close();
            pf.Shown += (s, e2) => fDesc.Focus();
            pf.ShowDialog(this);
        }

        // ??????????????????????????????????????????????????????
        //  POPUP FACTORY
        // ??????????????????????????????????????????????????????
        private (Form pf, Guna2Panel popup, Form overlay) CreatePopup(int w, int h)
        {
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

            var pf = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(w, h),
                BackColor = Color.White,
                ShowInTaskbar = false,
                TopMost = true,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true
            };
            pf.Location = new Point(sc.Left + (sc.Width - w) / 2, sc.Top + (sc.Height - h) / 2);

            using (var rgn = new GraphicsPath())
            {
                rgn.AddArc(0, 0, 40, 40, 180, 90); rgn.AddArc(w - 40, 0, 40, 40, 270, 90);
                rgn.AddArc(w - 40, h - 40, 40, 40, 0, 90); rgn.AddArc(0, h - 40, 40, 40, 90, 90);
                rgn.CloseFigure(); pf.Region = new Region(rgn);
            }

            var popup = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White, BackColor = Color.White };
            popup.ShadowDecoration.Enabled = true; popup.ShadowDecoration.Depth = 32; popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            pf.Controls.Add(popup);
            return (pf, popup, overlay);
        }

        private Panel BuildPopupHeader(int popupWidth, string title, string sub, Action close)
        {
            var head = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            head.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, head.Width, head.Height);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 8; x < head.Width; x += 20) for (int y = 6; y < head.Height; y += 20) g.FillEllipse(db, x, y, 2, 2);
                using (var cb = new SolidBrush(Color.FromArgb(12, 255, 255, 255))) g.FillEllipse(cb, head.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(title, tf); g.DrawString(title, tf, tb, head.Width - tsz.Width - 60, 16); }
                using (var sf3 = new Font("Cairo", 10F)) using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString(sub, sf3); g.DrawString(sub, sf3, sb3, head.Width - ssz.Width - 60, 54); }
            };

            var btnX = new Guna2Button
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
            btnX.HoverState.FillColor = Color.FromArgb(80, 255, 255, 255);
            btnX.Click += (s, e) => close();
            head.Controls.Add(btnX);
            head.Layout += (s, e) => btnX.Location = new Point(25, 20);
            return head;
        }

        private (Panel footer, Guna2Button btnSave) BuildPopupFooter(string saveText)
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = ColorTranslator.FromHtml("#F8FAFF"), Padding = new Padding(24, 10, 24, 14) };
            footer.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 2f)) g.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 2, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 2, footer.Width, 2);
            };
            var btnSave = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = saveText,
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#4E73DF"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 13F, FontStyle.Bold),
                Animated = true
            };
            btnSave.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9");
            btnSave.ShadowDecoration.Enabled = true; btnSave.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223); btnSave.ShadowDecoration.Depth = 10;
            footer.Controls.Add(btnSave);
            return (footer, btnSave);
        }

        private Panel MakeTxtWrapped(string placeholder, out Guna2TextBox txtOut)
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

        private Label MakeLbl(string text) => new Label
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
        private Panel Sp(int h = 8) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };

        private Guna2DataGridView MakeDgv()
        {
            var dgv = new Guna2DataGridView
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
            dgv.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#64748B");
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#F8FAFC");
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#64748B");
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgv.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgv.DefaultCellStyle.Font = new Font("Cairo", 13F, FontStyle.Bold);
            dgv.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#1E293B");
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dgv, true); } catch { }
            return dgv;
        }

        // ??????????????????????????????????????????????????????
        //  TOAST + HELPERS
        // ??????????????????????????????????????????????????????
        private async void ShowSuccessToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#10B981"), ColorTranslator.FromHtml("#ECFDF5"));
        private async void ShowErrorToast(string msg) => await ShowToast(msg, ColorTranslator.FromHtml("#EF4444"), ColorTranslator.FromHtml("#FEF2F2"));
        private async Task ShowToast(string msg, Color accent, Color bgc)
        {
            var t = new Panel { Size = new Size(360, 52), BackColor = bgc, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath()) { gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(t.Width - 20, 0, 20, 20, 270, 90); gp.AddArc(t.Width - 20, t.Height - 20, 20, 20, 0, 90); gp.AddArc(0, t.Height - 20, 20, 20, 90, 90); gp.CloseFigure(); t.Region = new Region(gp); }
            t.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(accent, 1.5f)) using (var ph = RoundPath(new Rectangle(0, 0, t.Width - 1, t.Height - 1), 10)) pe.Graphics.DrawPath(pen, ph);
                pe.Graphics.FillRectangle(new SolidBrush(accent), 0, 7, 4, t.Height - 14);
                using (var f = new Font("Cairo", 10.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1F2937")))
                    pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, t.Width - 8, t.Height), _sfCenter);
            };
            t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36);
            Controls.Add(t); t.BringToFront();
            t.Click += (s, e) => { try { Controls.Remove(t); t.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10) { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + (100 - i) / 5); await Task.Delay(7); }
            await Task.Delay(2600);
            for (int i = 0; i <= 100; i += 10) { try { t.Location = new Point(Width - t.Width - 28, Height - t.Height - 36 + i / 5); } catch { break; } await Task.Delay(7); }
            try { Controls.Remove(t); t.Dispose(); } catch { }
        }

        private static string GetInner(Exception ex) { if (ex == null) return ""; var e = ex; while (e.InnerException != null) e = e.InnerException; return e.Message; }
        private void EmployeesForm_Load(object sender, EventArgs e) { }
    }
}