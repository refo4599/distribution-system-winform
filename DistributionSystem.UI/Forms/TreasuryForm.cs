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
using DistributionSystem.Business.Services;

namespace DistributionSystem.UI.Forms
{
    public class TreasuryForm : Form
    {
        private readonly TreasuryService _service = new TreasuryService();
        private readonly TreasuryPdfService _pdfService = new TreasuryPdfService();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private Bitmap _bannerCache;

        private Label _lblInventory, _lblRevenue, _lblManual, _lblExpenses, _lblInbound, _lblProfit, _lblTotal;
        private Panel _historyContainer;
        private DataGridView _dgv;
        private Panel _paginationBar;
        private List<TreasuryMovementDto> _allMovements = new List<TreasuryMovementDto>();
        private int _currentPage = 1;
        private const int PageSize = 5;   // ? 5 ’›Ê› ›Ì ﬂ· ’›Õ…


        private static readonly Color ThemeDark = ColorTranslator.FromHtml("#1a2f5e");
        private static readonly Color ThemeMid = ColorTranslator.FromHtml("#1565c0");
        private static readonly Color ThemeAccent = ColorTranslator.FromHtml("#4E73DF");
        private static readonly Color ThemeLight = ColorTranslator.FromHtml("#EEF2FF");

        public TreasuryForm()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, false);
            UpdateStyles();
            BuildLayout();
            Shown += (s, e) => BeginInvoke(new Action(async () => await RefreshAsync()));
        }

        private void BuildLayout()
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildBanner(), 0, 0);
            root.Controls.Add(BuildCards(), 0, 1);
            root.Controls.Add(BuildBody(), 0, 2);

            Controls.Add(root); root.BringToFront();
            EnableDbAll(this);
            ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  HEADER BANNER
        // ??????????????????????????????????????????????????????
        private Panel BuildBanner()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 88, Padding = new Padding(0, 0, 0, 12), BackColor = Color.Transparent };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            banner.Paint += (s, e) =>
            {
                if (_bannerCache == null || _bannerCache.Width != banner.Width || _bannerCache.Height != banner.Height)
                {
                    _bannerCache?.Dispose();
                    if (banner.Width <= 0 || banner.Height <= 0) return;
                    _bannerCache = new Bitmap(banner.Width, banner.Height);
                    using (var g2 = Graphics.FromImage(_bannerCache))
                    {
                        g2.SmoothingMode = SmoothingMode.AntiAlias;
                        g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        var rc = new Rectangle(0, 0, banner.Width, banner.Height);
                        using (var br = new LinearGradientBrush(rc, ThemeDark, ThemeMid, LinearGradientMode.Horizontal))
                        using (var path = RoundPath(rc, 16)) g2.FillPath(br, path);
                        using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                            for (int x = 10; x < banner.Width; x += 22)
                                for (int y = 8; y < banner.Height; y += 22)
                                    g2.FillEllipse(dot, x, y, 2, 2);
                        using (var cb = new SolidBrush(Color.FromArgb(14, 255, 255, 255)))
                        { g2.FillEllipse(cb, banner.Width - 130, -50, 220, 220); g2.FillEllipse(cb, banner.Width - 30, 20, 160, 160); }
                        using (var tf = new Font("Cairo", 22F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                        { var sz = g2.MeasureString("«·Œ“‰…", tf); g2.DrawString("«·Œ“‰…", tf, tb, banner.Width - sz.Width - 24, 8); }
                        using (var sf2 = new Font("Cairo", 9.5F)) using (var sb2 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                            g2.DrawString("≈œ«—… —’Ìœ Ê√—»«Õ «·‘—ﬂ…", sf2, sb2, banner.Width - 230, 46);
                        using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6"))) g2.FillRectangle(b1, banner.Width - 42, 44, 38, 3);
                        using (var b2 = new SolidBrush(Color.FromArgb(120, 100, 181, 246))) g2.FillRectangle(b2, banner.Width - 60, 44, 14, 3);
                    }
                }
                e.Graphics.DrawImage(_bannerCache, 0, 0);
            };
            banner.Resize += (s, e) => { _bannerCache?.Dispose(); _bannerCache = null; };

            var btnRef = new Guna2Button
            {
                Text = " ÕœÌÀ «·»Ì«‰« ",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(145, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(20, (76 - 44) / 2)
            };
            btnRef.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnRef.Click += async (s, e) => await RefreshAsync();
            banner.Controls.Add(btnRef);

            var btnReport = new Guna2Button
            {
                Text = " ﬁ—Ì— PDF",
                FillColor = Color.FromArgb(30, 255, 255, 255),
                ForeColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(60, 255, 255, 255),
                Font = new Font("Cairo", 11F, FontStyle.Bold),
                Size = new Size(145, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Location = new Point(175, (76 - 44) / 2)
            };
            btnReport.HoverState.FillColor = Color.FromArgb(55, 255, 255, 255);
            btnReport.Click += (s, e) => ShowDatePickerPopup();
            banner.Controls.Add(btnReport);

            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  SUMMARY CARDS ó 7 ﬂ—Ê 
        // ??????????????????????????????????????????????????????
        private Panel BuildCards()
        {
            var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 8) };
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            for (int i = 0; i < 7; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7F));

            var cards = new[]
            {
                ("#10B981", "#065F46", "ﬁÌ„… «·„Œ“Ê‰",      " ﬂ·›… «·»÷«⁄… «·Õ«·Ì…",    "??"),
                ("#0891B2", "#164E63", "ﬁÌ„… «·Ê«—œ",        "≈Ã„«·Ì √Ê«„— «·Ê«—œ",      "??"),
                ("#8B5CF6", "#5B21B6", "—’Ìœ „÷«›",          "≈œŒ«· ÌœÊÌ",               "?"),
                ("#4E73DF", "#1e3a6e", "≈Ì—«œ«  «·›Ê« Ì—",   "«·„œ›Ê⁄ «·›⁄·Ì",           "??"),
                ("#EF4444", "#991B1B", "„’«—Ì› «·„ÊŸ›Ì‰",    "”·› + ≈œ«—Ì…",             "??"),
                ("#16A34A", "#14532D", "’«›Ì «·—»Õ",         "≈Ì—«œ«  »Ì⁄ -  ﬂ·›… ‘—«¡", "??"),
                ("#F59E0B", "#92400E", "«·—’Ìœ «·ﬂ·Ì",       "„÷«›+≈Ì—«œ« -Ê«—œ-„’«—Ì›","??"),
            };

            Label[] lblValues = new Label[7];
            for (int i = 0; i < 7; i++)
            {
                int marginL = i == 6 ? 0 : 5;
                int marginR = i == 0 ? 0 : 5;
                var card = MakeSmallCard(cards[i].Item1, cards[i].Item2,
                                         cards[i].Item3, cards[i].Item4,
                                         cards[i].Item5, marginL, marginR,
                                         out lblValues[i]);
                row.Controls.Add(card, i, 0);
            }

            _lblInventory = lblValues[0];
            _lblInbound = lblValues[1];
            _lblManual = lblValues[2];
            _lblRevenue = lblValues[3];
            _lblExpenses = lblValues[4];
            _lblProfit = lblValues[5];
            _lblTotal = lblValues[6];

            wrapper.Controls.Add(row);
            return wrapper;
        }

        private Guna2Panel MakeSmallCard(
            string accentHex, string valueFgHex,
            string title, string sub, string icon,
            int marginLeft, int marginRight,
            out Label valueLabel)
        {
            var card = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = 12,
                BorderThickness = 0,
                Margin = new Padding(marginLeft, 0, marginRight, 0)
            };
            card.ShadowDecoration.Enabled = true;
            card.ShadowDecoration.Depth = 5;
            card.ShadowDecoration.Color = Color.FromArgb(16, 0, 0, 0);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 5, BackColor = ColorTranslator.FromHtml(accentHex) };
            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(10, 6, 10, 6) };

            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Color.Transparent };
            pnlTop.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pe.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                pe.Graphics.FillEllipse(new SolidBrush(ColorTranslator.FromHtml(accentHex)), 4, (pnlTop.Height - 8) / 2, 8, 8);
                using (var f = new Font("Cairo", 9.5F, FontStyle.Bold))
                using (var br = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                { var sz = pe.Graphics.MeasureString(title, f); pe.Graphics.DrawString(title, f, br, pnlTop.Width - sz.Width - 2, (pnlTop.Height - sz.Height) / 2f); }
            };

            var lblV = new Label
            {
                Text = "...",
                Font = new Font("Cairo", 14F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml(valueFgHex),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            var lblS = new Label
            {
                Text = sub,
                Font = new Font("Cairo", 9F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            inner.Controls.Add(lblV);
            inner.Controls.Add(lblS);
            inner.Controls.Add(pnlTop);
            card.Controls.Add(inner);
            card.Controls.Add(topBar);
            valueLabel = lblV;
            return card;
        }

        // ??????????????????????????????????????????????????????
        //  BODY
        // ??????????????????????????????????????????????????????
        private Panel BuildBody()
        {
            var wrapper = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
            wrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            wrapper.Controls.Add(BuildInputCard(), 0, 0);
            wrapper.Controls.Add(BuildHistoryCard(), 1, 0);
            return wrapper;
        }

        private Panel BuildInputCard()
        {
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 10, 10) };
            var card = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 16, BorderThickness = 0 };
            card.ShadowDecoration.Enabled = true; card.ShadowDecoration.Depth = 6; card.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var hdr = BuildCardHeader("≈÷«›… —’Ìœ ··Œ“‰…");
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(20, 16, 20, 16) };

            var lblAmt = MakeBodyLabel("«·„»·€ (Ã‰ÌÂ)");
            var txtAmt = MakeBodyTxt("0.00", true);
            var sp1 = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };
            var lblNote = MakeBodyLabel("„·«ÕŸ… («Œ Ì«—Ì)");
            var txtNote = MakeBodyTxt("„À«·: —√” «·„«· «·«» œ«∆Ì", false);
            var sp2 = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            var chkDed = new CheckBox
            {
                Text = "Œ’„ (”Õ» „‰ «·Œ“‰…)",
                Font = new Font("Cairo", 10F),
                ForeColor = ColorTranslator.FromHtml("#EF4444"),
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            var sp3 = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.Transparent };
            var errLbl = new Label
            {
                Dock = DockStyle.Top,
                Height = 0,
                Font = new Font("Cairo", 9F),
                ForeColor = ColorTranslator.FromHtml("#EF4444"),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            var btnSave = new Guna2Button
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Text = "≈÷«›… ··Œ“‰…",
                BorderRadius = 13,
                FillColor = ThemeMid,
                ForeColor = Color.White,
                Font = new Font("Cairo", 12F, FontStyle.Bold)
            };
            btnSave.HoverState.FillColor = ThemeDark;
            btnSave.ShadowDecoration.Enabled = true;
            btnSave.ShadowDecoration.Depth = 4;
            btnSave.ShadowDecoration.Color = Color.FromArgb(40, 21, 101, 192);

            btnSave.Click += async (s, e) =>
            {
                errLbl.Height = 0; errLbl.Text = ""; txtAmt.BorderColor = ColorTranslator.FromHtml("#E5E7EB");
                if (!decimal.TryParse(txtAmt.Text.Replace(",", "."), System.Globalization.NumberStyles.Any, Inv, out decimal amt) || amt <= 0)
                { txtAmt.BorderColor = ColorTranslator.FromHtml("#EF4444"); errLbl.Text = "ï √œŒ· „»·€« ’ÕÌÕ« √ﬂ»— „‰ ’›—"; errLbl.Height = 18; return; }
                decimal finalAmt = chkDed.Checked ? -amt : amt;
                string note = string.IsNullOrWhiteSpace(txtNote.Text) ? (chkDed.Checked ? "”Õ»" : "≈Ìœ«⁄") : txtNote.Text.Trim();
                btnSave.Enabled = false; btnSave.Text = "Ã«—Ú «·Õ›Ÿ...";
                await Task.Run(() => _service.AddManualEntry(finalAmt, note));
                txtAmt.Text = ""; txtNote.Text = ""; chkDed.Checked = false;
                btnSave.Enabled = true; btnSave.Text = "≈÷«›… ··Œ“‰…";
                await RefreshAsync();
                ShowSuccessToast(" „ ≈÷«›… «·—’Ìœ ··Œ“‰… »‰Ã«Õ");
            };

            body.Controls.Add(btnSave);
            body.Controls.Add(errLbl);
            body.Controls.Add(sp3);
            body.Controls.Add(chkDed);
            body.Controls.Add(sp2);
            body.Controls.Add(txtNote);
            body.Controls.Add(lblNote);
            body.Controls.Add(sp1);
            body.Controls.Add(txtAmt);
            body.Controls.Add(lblAmt);

            card.Controls.Add(body);
            card.Controls.Add(hdr);
            outer.Controls.Add(card);
            return outer;
        }

        private Panel BuildHistoryCard()
        {
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };
            var card = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 16, BorderThickness = 0 };
            card.ShadowDecoration.Enabled = true; card.ShadowDecoration.Depth = 6; card.ShadowDecoration.Color = Color.FromArgb(14, 0, 0, 0);

            var hdr = BuildCardHeader("”Ã· Õ—ﬂ«  «·Œ“‰…");

            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ColumnHeadersHeight = 44,
                EnableHeadersVisualStyles = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.None
            };
            _dgv.RowTemplate.Height = 64;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1e3a6e");
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 10.5F, FontStyle.Bold);
            _dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1e3a6e");
            _dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            _dgv.DefaultCellStyle.BackColor = Color.White;
            _dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            _dgv.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            _dgv.DefaultCellStyle.Font = new Font("Cairo", 10.5F);
            _dgv.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#374151");
            _dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFF");
            try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_dgv, true); } catch { }

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "«·‰Ê⁄", Width = 100, DataPropertyName = "CategoryLabel" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "«· ›«’Ì·", Width = 300, DataPropertyName = "Note" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "SubDetail", HeaderText = "", Width = 0, DataPropertyName = "SubDetail", Visible = false });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reference", HeaderText = "«·„—Ã⁄", Width = 110, DataPropertyName = "Reference" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DateCol", HeaderText = "«· «—ÌŒ", Width = 130, DataPropertyName = "Date" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "«·„»·€", Width = 110, DataPropertyName = "Amount" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsDebit", HeaderText = "", Width = 0, DataPropertyName = "IsDebit", Visible = false });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category2", HeaderText = "", Width = 0, DataPropertyName = "Category", Visible = false });

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["Note"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            _dgv.CellPainting += Dgv_CellPainting;
            _dgv.Resize += (s, e) => FitDgvColumns();

            _paginationBar = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.White };

            var dgvWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            dgvWrapper.Controls.Add(_dgv);

            card.Controls.Add(dgvWrapper);
            card.Controls.Add(_paginationBar);
            card.Controls.Add(hdr);
            outer.Controls.Add(card);
            return outer;
        }

        private void FitDgvColumns()
        {
            if (_dgv == null || _dgv.Columns.Count == 0) return;
            int W = _dgv.ClientSize.Width;
            int wRef = 110, wDate = 130, wAmt = 110, wCat = 100;
            int wNote = Math.Max(120, W - wRef - wDate - wAmt - wCat - 4);
            _dgv.Columns["Category"].Width = wCat;
            _dgv.Columns["Note"].Width = wNote;
            _dgv.Columns["Reference"].Width = wRef;
            _dgv.Columns["DateCol"].Width = wDate;
            _dgv.Columns["Amount"].Width = wAmt;
        }

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
                    using (var font = new Font("Cairo", 10.5F, FontStyle.Bold)) using (var tb = new SolidBrush(Color.White))
                        g.DrawString(e.Value?.ToString() ?? "", font, tb, e.CellBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, e.CellBounds.Left, e.CellBounds.Top + 6, e.CellBounds.Left, e.CellBounds.Bottom - 6); g.DrawLine(sp, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6); }
                    return;
                }
                if (e.RowIndex < 0) return;

                string cat = _dgv.Rows[e.RowIndex].Cells["Category2"].Value?.ToString() ?? "";
                bool isDebit = _dgv.Rows[e.RowIndex].Cells["IsDebit"].Value is bool bd && bd;
                bool sel = _dgv.Rows[e.RowIndex].Selected;

                Color bg, accent, amtClr, badgeBg;
                switch (cat)
                {
                    case "invoice":
                        bg = ColorTranslator.FromHtml(sel ? "#EEF2FF" : "#F0F7FF"); accent = ColorTranslator.FromHtml("#4E73DF");
                        amtClr = ColorTranslator.FromHtml("#1e3a6e"); badgeBg = ColorTranslator.FromHtml("#DBEAFE"); break;
                    case "inbound":
                        bg = ColorTranslator.FromHtml(sel ? "#EEF2FF" : "#F0FDF4"); accent = ColorTranslator.FromHtml("#10B981");
                        amtClr = ColorTranslator.FromHtml("#065F46"); badgeBg = ColorTranslator.FromHtml("#D1FAE5"); break;
                    case "employee_loan":
                        bg = ColorTranslator.FromHtml(sel ? "#EEF2FF" : "#FFF7ED"); accent = ColorTranslator.FromHtml("#F59E0B");
                        amtClr = ColorTranslator.FromHtml("#92400E"); badgeBg = ColorTranslator.FromHtml("#FEF3C7"); break;
                    case "employee_expense":
                    case "manual_out":
                        bg = ColorTranslator.FromHtml(sel ? "#EEF2FF" : "#FEF2F2"); accent = ColorTranslator.FromHtml("#EF4444");
                        amtClr = ColorTranslator.FromHtml("#DC2626"); badgeBg = ColorTranslator.FromHtml("#FECACA"); break;
                    default:
                        bg = ColorTranslator.FromHtml(sel ? "#EEF2FF" : "#F5F3FF"); accent = ColorTranslator.FromHtml("#8B5CF6");
                        amtClr = ColorTranslator.FromHtml("#5B21B6"); badgeBg = ColorTranslator.FromHtml("#EDE9FE"); break;
                }

                string colName = _dgv.Columns[e.ColumnIndex].Name;
                var g2 = e.Graphics; g2.SmoothingMode = SmoothingMode.AntiAlias;

                if (colName == "Category")
                {
                    e.Handled = true;
                    g2.FillRectangle(new SolidBrush(bg), e.CellBounds);
                    string lbl = e.Value?.ToString() ?? "";
                    int bW = 82, bH = 26, bx = e.CellBounds.Left + (e.CellBounds.Width - bW) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bH) / 2;
                    using (var path = RoundPath(new Rectangle(bx, by, bW, bH), bH / 2))
                    { g2.FillPath(new SolidBrush(badgeBg), path); g2.DrawPath(new Pen(accent, 1f), path); }
                    using (var f = new Font("Cairo", 8.5F, FontStyle.Bold)) using (var tb = new SolidBrush(amtClr))
                        g2.DrawString(lbl, f, tb, new RectangleF(bx, by, bW, bH), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
                else if (colName == "Amount")
                {
                    e.Handled = true;
                    g2.FillRectangle(new SolidBrush(bg), e.CellBounds);
                    // ? «ﬁ—√ «·„»·€ „»«‘—… „‰ «·‹ DTO ó «··Ì ”«·» Ì»ﬁÏ debit
                    decimal amt = 0;
                    if (e.Value != null) decimal.TryParse(e.Value.ToString(), out amt);
                    bool amtIsDebit = amt < 0;
                    string amtStr = (amtIsDebit ? "- " : "+ ") + Math.Abs(amt).ToString("N2", Inv) + " Ã";
                    int bW = 100, bH = 28, bx = e.CellBounds.Left + (e.CellBounds.Width - bW) / 2, by = e.CellBounds.Top + (e.CellBounds.Height - bH) / 2;
                    using (var path = RoundPath(new Rectangle(bx, by, bW, bH), 8))
                        g2.FillPath(new SolidBrush(Color.FromArgb(35, accent)), path);
                    using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(amtClr))
                        g2.DrawString(amtStr, f, tb, new RectangleF(bx, by, bW, bH), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
                else if (colName == "DateCol")
                {
                    e.Handled = true;
                    g2.FillRectangle(new SolidBrush(bg), e.CellBounds);
                    string dateStr = "", timeStr = "";
                    if (e.Value is DateTime dt2) { dateStr = dt2.ToString("yyyy/MM/dd", Inv); timeStr = dt2.ToString("HH:mm"); }
                    else if (DateTime.TryParse(e.Value?.ToString(), out DateTime dt3)) { dateStr = dt3.ToString("yyyy/MM/dd", Inv); timeStr = dt3.ToString("HH:mm"); }
                    int cy2 = e.CellBounds.Top + e.CellBounds.Height / 2;
                    using (var f = new Font("Cairo", 9.5F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                        g2.DrawString(dateStr, f, tb, new RectangleF(e.CellBounds.Left, cy2 - 20, e.CellBounds.Width, 20), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var f = new Font("Cairo", 8.5F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#94A3B8")))
                        g2.DrawString(timeStr, f, tb, new RectangleF(e.CellBounds.Left, cy2 + 2, e.CellBounds.Width, 18), new StringFormat { Alignment = StringAlignment.Center });
                }
                else if (colName == "Note")
                {
                    e.Handled = true;
                    g2.FillRectangle(new SolidBrush(bg), e.CellBounds);
                    string note = _dgv.Rows[e.RowIndex].Cells["Note"].Value?.ToString() ?? "";
                    string sub = _dgv.Rows[e.RowIndex].Cells["SubDetail"].Value?.ToString() ?? "";
                    int ty = string.IsNullOrEmpty(sub) ? e.CellBounds.Top + (e.CellBounds.Height - 20) / 2 : e.CellBounds.Top + 10;
                    using (var f = new Font("Cairo", 10F, FontStyle.Bold)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#0F172A")))
                        g2.DrawString(note, f, tb, new RectangleF(e.CellBounds.Left + 4, ty, e.CellBounds.Width - 12, 20),
                            new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter });
                    if (!string.IsNullOrEmpty(sub))
                        using (var f = new Font("Cairo", 8.5F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                            g2.DrawString(sub, f, tb, new RectangleF(e.CellBounds.Left + 4, ty + 22, e.CellBounds.Width - 12, 18),
                                new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter });
                    g2.FillRectangle(new SolidBrush(accent), e.CellBounds.Right - 4, e.CellBounds.Top + 8, 4, e.CellBounds.Height - 16);
                }
                else
                {
                    e.Handled = true;
                    g2.FillRectangle(new SolidBrush(bg), e.CellBounds);
                    e.PaintContent(e.CellBounds);
                }

                using (var wPen = new Pen(Color.White, 2f))
                {
                    g2.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Top, e.CellBounds.Left, e.CellBounds.Bottom);
                    g2.DrawLine(wPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);
                    g2.DrawLine(wPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }
            }
            catch { }
        }

        private void RenderPagination()
        {
            if (_paginationBar == null) return;
            _paginationBar.Controls.Clear();
            int total = Math.Max(1, (int)Math.Ceiling(_allMovements.Count / (double)PageSize));

            // ?? ‰’ «·„⁄·Ê„«  ??????????????????????????????????????
            int firstItem = _allMovements.Count == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
            int lastItem = Math.Min(_allMovements.Count, _currentPage * PageSize);
            var lblInfo = new Label
            {
                Text = $"⁄—÷ {firstItem}-{lastItem} „‰ {_allMovements.Count}",
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                AutoSize = false,
                Width = 200,
                Height = 52,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                BackColor = Color.Transparent
            };
            _paginationBar.Controls.Add(lblInfo);

            // ?? √“—«— «·’›Õ«  ?????????????????????????????????????
            var pnlPages = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                WrapContents = false,
                Padding = new Padding(0)
            };

            pnlPages.Controls.Add(MakeNavBtn("õ", _currentPage < total, () => { _currentPage++; BindDgv(); }));

            for (int i = total; i >= 1; i--)
            {
                int pg = i; bool isCurrent = pg == _currentPage;
                var btn = new Panel { Size = new Size(34, 34), BackColor = Color.Transparent, Cursor = isCurrent ? Cursors.Default : Cursors.Hand, Margin = new Padding(3, 9, 3, 9) };
                btn.Paint += (s, pe) =>
                {
                    var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                    if (isCurrent)
                    {
                        using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#3B5DC9"), LinearGradientMode.Vertical))
                        using (var path = RoundPath(rc, 7)) g.FillPath(br, path);
                        using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                            g.DrawString(pg.ToString(), f, Brushes.White, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                    else
                    {
                        using (var path = RoundPath(rc, 7))
                        { g.FillPath(new SolidBrush(ColorTranslator.FromHtml("#F8FAFC")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                        using (var f = new Font("Cairo", 10F)) using (var tb = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                            g.DrawString(pg.ToString(), f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                };
                if (!isCurrent) btn.Click += (s, e2) => { _currentPage = pg; BindDgv(); };
                pnlPages.Controls.Add(btn);
            }

            pnlPages.Controls.Add(MakeNavBtn("ã", _currentPage > 1, () => { _currentPage--; BindDgv(); }));
            _paginationBar.Controls.Add(pnlPages);
        }

        private Panel MakeNavBtn(string text, bool enabled, Action onClick)
        {
            var btn = new Panel { Size = new Size(34, 34), BackColor = Color.Transparent, Cursor = enabled ? Cursors.Hand : Cursors.Default, Margin = new Padding(3, 9, 3, 9) };
            btn.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = RoundPath(rc, 7))
                { g.FillPath(new SolidBrush(enabled ? ColorTranslator.FromHtml("#F8FAFC") : ColorTranslator.FromHtml("#F1F5F9")), path); g.DrawPath(new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f), path); }
                using (var f = new Font("Segoe UI", 13F)) using (var tb = new SolidBrush(enabled ? ColorTranslator.FromHtml("#374151") : ColorTranslator.FromHtml("#CBD5E1")))
                    g.DrawString(text, f, tb, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            if (enabled) btn.Click += (s, e) => onClick();
            return btn;
        }

        private void BindDgv()
        {
            if (_dgv == null) return;
            int total = Math.Max(1, (int)Math.Ceiling(_allMovements.Count / (double)PageSize));
            _currentPage = Math.Min(_currentPage, total);
            var page = _allMovements.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
            _dgv.DataSource = new System.Windows.Forms.BindingSource { DataSource = page };
            FitDgvColumns();
            RenderPagination();
        }

        private Panel BuildCardHeader(string title)
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.Transparent };
            hdr.Paint += (s, e) =>
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, hdr.Width, hdr.Height);
                using (var br = new LinearGradientBrush(rc, ThemeDark, ThemeMid, LinearGradientMode.Horizontal))
                using (var path = new GraphicsPath())
                {
                    int r2 = 16;
                    path.AddArc(rc.Left, rc.Top, r2 * 2, r2 * 2, 180, 90);
                    path.AddArc(rc.Right - r2 * 2, rc.Top, r2 * 2, r2 * 2, 270, 90);
                    path.AddLine(rc.Right, rc.Bottom, rc.Left, rc.Bottom);
                    path.CloseFigure();
                    g.FillPath(br, path);
                }
                using (var f = new Font("Cairo", 13F, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                { var sz = g.MeasureString(title, f); g.DrawString(title, f, tb, hdr.Width - sz.Width - 16, (hdr.Height - sz.Height) / 2f); }
            };
            return hdr;
        }

        // ??????????????????????????????????????????????????????
        //  REFRESH
        // ??????????????????????????????????????????????????????
        private async Task RefreshAsync()
        {
            TreasurySummaryDto summary = null;
            List<TreasuryMovementDto> movements = null;
            decimal inboundTotal = 0m;
            decimal profitTotal = 0m;

            await Task.Run(() =>
            {
                try { summary = _service.GetSummary(); } catch { summary = new TreasurySummaryDto(); }
                try { movements = _service.GetAllMovements(); } catch { movements = new List<TreasuryMovementDto>(); }
                try { inboundTotal = _service.GetInboundTotal(); } catch { inboundTotal = 0m; }
                try { profitTotal = _service.GetProfitTotal(); } catch { profitTotal = 0m; }
            });

            string Fmt(decimal v) => v.ToString("N2", Inv) + " Ã‰ÌÂ";

            _lblInventory.Text = Fmt(summary.InventoryValue);
            _lblRevenue.Text = Fmt(summary.InvoicesRevenue);
            _lblManual.Text = Fmt(summary.ManualBalance);

            _lblExpenses.Text = "- " + Fmt(summary.EmployeeExpenses);
            _lblExpenses.ForeColor = ColorTranslator.FromHtml("#DC2626");

            _lblInbound.Text = Fmt(inboundTotal);
            _lblInbound.ForeColor = ColorTranslator.FromHtml("#164E63");

            _lblProfit.Text = Fmt(profitTotal);
            _lblProfit.ForeColor = profitTotal >= 0
                ? ColorTranslator.FromHtml("#14532D")
                : ColorTranslator.FromHtml("#DC2626");

            decimal total = summary.ManualBalance
                          + summary.InvoicesRevenue
                          - inboundTotal
                          - summary.EmployeeExpenses;

            _lblTotal.Text = Fmt(total);
            _lblTotal.ForeColor = total >= 0
                ? ColorTranslator.FromHtml("#92400E")
                : ColorTranslator.FromHtml("#DC2626");

            summary.TotalBalance = total;

            _allMovements = movements ?? new List<TreasuryMovementDto>();
            _currentPage = 1;
            BindDgv();
        }

        // ??????????????????????????????????????????????????????
        //  PDF REPORT
        // ??????????????????????????????????????????????????????
        private void ShowDatePickerPopup()
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

            int w = 420, h = 290;
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
                rgn.CloseFigure();
                pf.Region = new Region(rgn);
            }

            pf.FormClosed += (s, e) => { try { overlay.Close(); overlay.Dispose(); } catch { } };
            overlay.Click += (s, e) => pf.Close();

            var popup2 = new Guna2Panel { Dock = DockStyle.Fill, BorderRadius = 0, FillColor = Color.White };
            popup2.ShadowDecoration.Enabled = true; popup2.ShadowDecoration.Depth = 30; popup2.ShadowDecoration.Color = Color.FromArgb(60, 0, 0, 0);
            pf.Controls.Add(popup2);

            var head = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.Transparent };
            head.Paint += (sndr, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, head.Width, head.Height);
                using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);
                using (var db = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 8; x < head.Width; x += 20)
                        for (int y = 6; y < head.Height; y += 20)
                            g.FillEllipse(db, x, y, 2, 2);
                using (var cb2 = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                    g.FillEllipse(cb2, head.Width - 100, -40, 180, 180);
                using (var tf = new Font("Cairo", 17F, FontStyle.Bold))
                using (var tb2 = new SolidBrush(Color.White))
                { var tsz = g.MeasureString(" ﬁ—Ì— «·Œ“‰…", tf); g.DrawString(" ﬁ—Ì— «·Œ“‰…", tf, tb2, head.Width - tsz.Width - 50, 16); }
                using (var sf3 = new Font("Cairo", 10F))
                using (var sb3 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                { var ssz = g.MeasureString("«Œ — «·ÌÊ„ À„ «÷€ÿ  Õ„Ì·", sf3); g.DrawString("«Œ — «·ÌÊ„ À„ «÷€ÿ  Õ„Ì·", sf3, sb3, head.Width - ssz.Width - 50, 52); }
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
            btnX.HoverState.FillColor = Color.FromArgb(90, 255, 255, 255);
            btnX.Click += (s, e) => pf.Close();
            head.Controls.Add(btnX);
            head.Layout += (s, e) => btnX.Location = new Point(18, 18);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 20, 28, 4), RightToLeft = RightToLeft.Yes };
            var lblDate = new Label { Text = " «—ÌŒ «· ﬁ—Ì—", Dock = DockStyle.Top, Height = 26, Font = new Font("Cairo", 10F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#374151"), TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent, RightToLeft = RightToLeft.No };
            var dtPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today, Font = new Font("Cairo", 11F), RightToLeft = RightToLeft.No, Dock = DockStyle.Top, Height = 44 };
            body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent });
            body.Controls.Add(dtPicker);
            body.Controls.Add(lblDate);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 10, 20, 14) };
            footer.Paint += (s, pe) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1f)) pe.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(new Rectangle(0, 1, footer.Width, 2), ColorTranslator.FromHtml("#4E73DF"), ColorTranslator.FromHtml("#E8EDFF"), LinearGradientMode.Horizontal))
                    pe.Graphics.FillRectangle(br, 0, 1, footer.Width, 2);
            };
            var btnSave2 = new Guna2Button { Dock = DockStyle.Fill, Text = "??   Õ„Ì· «· ﬁ—Ì—", BorderRadius = 12, FillColor = ColorTranslator.FromHtml("#4E73DF"), ForeColor = Color.White, Font = new Font("Cairo", 12F, FontStyle.Bold), Animated = true };
            btnSave2.HoverState.FillColor = ColorTranslator.FromHtml("#3B5DC9");
            btnSave2.ShadowDecoration.Enabled = true; btnSave2.ShadowDecoration.Color = Color.FromArgb(45, 78, 115, 223); btnSave2.ShadowDecoration.Depth = 10;
            btnSave2.Click += async (sndr, ev) => { DateTime chosen = dtPicker.Value.Date; btnSave2.Enabled = false; btnSave2.Text = "Ã«—Ú «· Õ÷Ì—..."; pf.Close(); await GenerateAndSaveDailyReport(chosen); };
            footer.Controls.Add(btnSave2);

            popup2.Controls.Add(body);
            popup2.Controls.Add(footer);
            popup2.Controls.Add(head);
            pf.ShowDialog(this);
        }

        private async Task GenerateAndSaveDailyReport(DateTime selectedDate)
        {
            try
            {
                TreasurySummaryDto summary = null;
                List<TreasuryMovementDto> mv = null;
                decimal inboundTot = 0m, profitTot = 0m;

                await Task.Run(() =>
                {
                    summary = _service.GetSummary();
                    var allMovements = _service.GetAllMovements();
                    mv = allMovements.Where(m => m.Date.Date == selectedDate).ToList();
                    try { inboundTot = _service.GetInboundTotal(); } catch { }
                    try { profitTot = _service.GetProfitTotal(); } catch { }
                    summary.TotalBalance = summary.ManualBalance + summary.InvoicesRevenue - inboundTot - summary.EmployeeExpenses;
                });

                var pdfBytes = await Task.Run(() => _pdfService.GenerateDailyReport(summary, mv, selectedDate, inboundTot, profitTot));
                string dateStr = selectedDate.ToString("yyyy-MM-dd", Inv);
                using (var sfd = new SaveFileDialog { Title = "Õ›Ÿ «· ﬁ—Ì—", Filter = "PDF|*.pdf", FileName = $" ﬁ—Ì—_«·Œ“‰…_{dateStr}.pdf", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    { File.WriteAllBytes(sfd.FileName, pdfBytes); ShowSuccessToast($" „ Õ›Ÿ  ﬁ—Ì— {dateStr} »‰Ã«Õ"); }
                }
            }
            catch (Exception ex)
            { MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—: " + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ??????????????????????????????????????????????????????
        //  TOAST
        // ??????????????????????????????????????????????????????
        private async void ShowSuccessToast(string msg)
        {
            var toast = new Panel { Size = new Size(320, 52), BackColor = ThemeLight, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath())
            {
                gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(toast.Width - 20, 0, 20, 20, 270, 90);
                gp.AddArc(toast.Width - 20, toast.Height - 20, 20, 20, 0, 90); gp.AddArc(0, toast.Height - 20, 20, 20, 90, 90);
                gp.CloseFigure(); toast.Region = new Region(gp);
            }
            toast.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(ThemeAccent, 1.5f))
                using (var path = RoundPath(new Rectangle(0, 0, toast.Width - 1, toast.Height - 1), 10))
                    pe.Graphics.DrawPath(pen, path);
                pe.Graphics.FillRectangle(new SolidBrush(ThemeAccent), 0, 8, 4, toast.Height - 16);
                using (var f = new Font("Cairo", 10.5F, FontStyle.Bold))
                using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1e3a6e")))
                    pe.Graphics.DrawString(msg, f, tb, new RectangleF(4, 0, toast.Width - 8, toast.Height),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
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
        private Label MakeBodyLabel(string text) =>
            new Label { Text = text, Dock = DockStyle.Top, Height = 22, Font = new Font("Cairo", 10F, FontStyle.Bold), ForeColor = ColorTranslator.FromHtml("#374151"), TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent };

        private Guna2TextBox MakeBodyTxt(string placeholder, bool bold)
        {
            var t = new Guna2TextBox
            {
                Dock = DockStyle.Top,
                Height = 36,
                BorderRadius = 10,
                FillColor = ColorTranslator.FromHtml("#F9FAFB"),
                BorderColor = ColorTranslator.FromHtml("#E5E7EB"),
                Font = new Font("Cairo", bold ? 12F : 10.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                PlaceholderText = placeholder,
                PlaceholderForeColor = ColorTranslator.FromHtml("#C4C9D4"),
                ForeColor = ThemeDark,
                TextAlign = HorizontalAlignment.Right,
                RightToLeft = RightToLeft.Yes,
            };
            t.FocusedState.BorderColor = ThemeAccent;
            t.FocusedState.FillColor = Color.White;
            return t;
        }

        private static readonly System.Reflection.PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static void EnableDbAll(Control parent)
        { foreach (Control c in parent.Controls) { try { _dbProp?.SetValue(c, true); } catch { } if (c.Controls.Count > 0) EnableDbAll(c); } }

        private GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2; var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
    }
}