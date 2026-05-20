using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public partial class CustomerDetailsForm : Form
    {
        private readonly CustomerDetailsService _detailsService = new CustomerDetailsService();
        private readonly CustomerDetailsReportService _reportService = new CustomerDetailsReportService();
        private readonly int _customerId;

        private CustomerFullDetailsDto _loadedData = null;

        // header controls
        private Label lblName, lblPhone, lblAddress, lblTypeBadge;
        private Panel _bannerPanel;

        private Panel _metricsCard;
        private Guna2DataGridView dgvInvoices, dgvInbound;
        private Guna2Panel pnlInvoices, pnlInbound;

        private string _m1_title = "", _m1_value = ""; private Color _m1_color = Color.Gray;
        private string _m2_title = "", _m2_value = ""; private Color _m2_color = Color.Gray;
        private string _m3_title = "", _m3_value = ""; private Color _m3_color = Color.Gray;
        private string _m4_title = "", _m4_value = ""; private Color _m4_color = Color.Gray;

        public CustomerDetailsForm(int customerId)
        {
            _customerId = customerId;
            InitializeComponent();
            BuildLayout();
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = " ›«’Ì· «·⁄„Ì·";
            Size = new Size(920, 700);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Cairo", 10F);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            MinimumSize = new Size(700, 550);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, false);
            this.UpdateStyles();
        }

        private static readonly PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void EnableDbAll(Control p)
        {
            foreach (Control c in p.Controls)
            {
                try { _dbProp?.SetValue(c, true); } catch { }
                if (c.Controls.Count > 0) EnableDbAll(c);
            }
        }

        // ??????????????????????????????????????????????????????
        //  LAYOUT
        // ??????????????????????????????????????????????????????
        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 14, 16, 14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F)); // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // metrics
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // grids
            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildMetrics(), 0, 1);
            root.Controls.Add(BuildGrids(), 0, 2);
            EnableDbAll(root);
            Controls.Add(root);
        }

        // ??????????????????????????????????????????????????????
        //  HEADER ó  ’„Ì„ ÃœÌœ Ê«÷Õ Ê„‰Ÿ„
        // ??????????????????????????????????????????????????????
        private Control BuildHeader()
        {
            var wrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };

            _bannerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // ?? —”„ «·Œ·›Ì… ??????????????????????????????????
            _bannerPanel.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rc = new Rectangle(0, 0, _bannerPanel.Width, _bannerPanel.Height);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var br = new LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#1a2f5e"),
                    ColorTranslator.FromHtml("#1565c0"),
                    LinearGradientMode.Horizontal))
                using (var path = RoundPath(rc, 16))
                    g.FillPath(br, path);

                // ‰ﬁ«ÿ “Œ—›Ì…
                using (var dot = new SolidBrush(Color.FromArgb(15, 255, 255, 255)))
                    for (int x = 10; x < _bannerPanel.Width; x += 22)
                        for (int y = 8; y < _bannerPanel.Height; y += 22)
                            g.FillEllipse(dot, x, y, 2, 2);

                // œÊ«∆— “Œ—›Ì…
                using (var cb = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                {
                    g.FillEllipse(cb, _bannerPanel.Width - 100, -50, 200, 200);
                    g.FillEllipse(cb, _bannerPanel.Width - 20, 20, 140, 140);
                }

                // Œÿ “Œ—›Ì √”›· Ì”«—
                using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                    g.FillRectangle(b1, 16, _bannerPanel.Height - 6, 50, 3);
                using (var b2 = new SolidBrush(Color.FromArgb(130, 100, 181, 246)))
                    g.FillRectangle(b2, 70, _bannerPanel.Height - 6, 20, 3);
            };

            // ?? Labels ????????????????????????????????????????
            var labelsTable = new TableLayoutPanel
            {
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.Transparent,
                AutoSize = false
            };
            labelsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // «·«”„
            labelsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F)); // «·Â« ›
            labelsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F)); // «·⁄‰Ê«‰
            labelsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); // badge «·‰Ê⁄

            lblName = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 20F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "..."
            };
            lblPhone = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 10.5F),
                ForeColor = Color.FromArgb(220, 255, 255, 255),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "Â« ›: -"
            };
            lblAddress = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 10F),
                ForeColor = Color.FromArgb(190, 255, 255, 255),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "⁄‰Ê«‰: -"
            };
            lblTypeBadge = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight,
                Text = ""
            };
            // —”„ badge «·‰Ê⁄
            lblTypeBadge.Paint += (s, pe) =>
            {
                if (string.IsNullOrEmpty(lblTypeBadge.Tag as string)) return;
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                string txt = lblTypeBadge.Tag as string;
                bool isInv = txt == "›Ê« Ì—";
                Color bgC = isInv ? Color.FromArgb(200, 209, 250, 229) : Color.FromArgb(200, 219, 234, 254);
                Color fgC = isInv ? ColorTranslator.FromHtml("#065F46") : ColorTranslator.FromHtml("#1E3A8A");
                Color bd = isInv ? ColorTranslator.FromHtml("#6EE7B7") : ColorTranslator.FromHtml("#93C5FD");

                using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                {
                    var sz = g.MeasureString(txt, f);
                    int bw = (int)sz.Width + 24, bh = 24;
                    // „Õ«–«… Ì„Ì‰
                    int bx = lblTypeBadge.Width - bw;
                    int by = (lblTypeBadge.Height - bh) / 2;
                    var brc = new Rectangle(bx, by, bw, bh);
                    using (var path = RoundPath(brc, bh / 2))
                    {
                        g.FillPath(new SolidBrush(bgC), path);
                        g.DrawPath(new Pen(bd, 1.2f), path);
                    }
                    g.DrawString(txt, f, new SolidBrush(fgC), brc,
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                }
            };

            labelsTable.Controls.Add(lblName, 0, 0);
            labelsTable.Controls.Add(lblPhone, 0, 1);
            labelsTable.Controls.Add(lblAddress, 0, 2);
            labelsTable.Controls.Add(lblTypeBadge, 0, 3);

            // ?? “—«— PDF ?????????????????????????????????????
            var btnReport = new Guna2Button
            {
                Text = " Õ„Ì· «· ﬁ—Ì—",
                Size = new Size(160, 34),
                BorderRadius = 10,
                FillColor = Color.FromArgb(45, 255, 255, 255),
                ForeColor = Color.White,
                BorderThickness = 1,
                BorderColor = Color.FromArgb(80, 255, 255, 255),
                Font = new Font("Cairo", 10F, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnReport.HoverState.FillColor = Color.FromArgb(80, 255, 255, 255);
            btnReport.Click += async (s, e) => await GenerateAndSaveReport();

            //  ÕœÌœ „Ê«ﬁ⁄ «·⁄‰«’— ó «·‰’ ⁄·Ï «·Ì„Ì‰° «·“—«— ⁄·Ï «·Ì”«—
            _bannerPanel.Resize += (s, e) =>
            {
                int tableH = 40 + 26 + 24 + 28;
                int tableY = (_bannerPanel.Height - tableH) / 2;
                int tableW = 400;
                int tableX = _bannerPanel.Width - tableW - 8; // √ﬁ’Ï «·Ì„Ì‰
                labelsTable.SetBounds(tableX, tableY, tableW, tableH);
                btnReport.Location = new Point(16, (_bannerPanel.Height - btnReport.Height) / 2);
            };

            _bannerPanel.Controls.Add(labelsTable);
            _bannerPanel.Controls.Add(btnReport);
            wrap.Controls.Add(_bannerPanel);
            return wrap;
        }

        // ??????????????????????????????????????????????????????
        //  GENERATE & SAVE PDF
        // ??????????????????????????????????????????????????????
        private async Task GenerateAndSaveReport()
        {
            if (_loadedData == null)
            {
                MessageBox.Show("«·»Ì«‰«  ·„  ıÕ„Û¯· »⁄œ.", " ‰»ÌÂ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Guna2Button btn = null;
            foreach (Control c in _bannerPanel.Controls)
                if (c is Guna2Button gb) { btn = gb; break; }

            if (btn != null) { btn.Enabled = false; btn.Text = "Ã«—Ú «·≈‰‘«¡..."; }
            try
            {
                byte[] pdfBytes = await Task.Run(() => _reportService.GenerateCustomerReport(_loadedData));
                string safeName = _loadedData.Customer?.Name ?? "⁄„Ì·";
                foreach (char c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');
                string defaultName = $" ﬁ—Ì—_{safeName}_{DateTime.Now:yyyy-MM-dd}.pdf";
                using (var sfd = new SaveFileDialog
                {
                    Title = "Õ›Ÿ «· ﬁ—Ì—",
                    Filter = "PDF|*.pdf",
                    FileName = defaultName,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllBytes(sfd.FileName, pdfBytes);
                        ShowToast("?  „ Õ›Ÿ «· ﬁ—Ì— »‰Ã«Õ",
                            ColorTranslator.FromHtml("#10B981"),
                            ColorTranslator.FromHtml("#ECFDF5"));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + GetInner(ex), "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (btn != null) { btn.Enabled = true; btn.Text = " Õ„Ì· «· ﬁ—Ì—"; }
            }
        }

        // ??????????????????????????????????????????????????????
        //  METRICS
        // ??????????????????????????????????????????????????????
        private Control BuildMetrics()
        {
            var wrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
            _metricsCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _metricsCard.Paint += PaintMetrics;
            wrap.Controls.Add(_metricsCard);
            return wrap;
        }

        private void PaintMetrics(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var items = new (string title, string value, Color accent)[]
            {
                (_m1_title, _m1_value, _m1_color),
                (_m2_title, _m2_value, _m2_color),
                (_m3_title, _m3_value, _m3_color),
                (_m4_title, _m4_value, _m4_color),
            };

            int total = 4, gap = 10;
            int cardW = (_metricsCard.Width - gap * (total - 1)) / total;
            int cardH = _metricsCard.Height - 4;

            for (int i = 0; i < total; i++)
            {
                var (title, value, accent) = items[i];
                int x = _metricsCard.Width - (i + 1) * cardW - i * gap;
                var rc = new Rectangle(x, 2, cardW, cardH);

                using (var path = RoundPath(rc, 14))
                {
                    using (var br = new SolidBrush(Color.White)) g.FillPath(br, path);
                    using (var pen = new Pen(Color.FromArgb(16, 0, 0, 0), 1f)) g.DrawPath(pen, path);
                }
                if (string.IsNullOrEmpty(title)) continue;

                var topBar = new Rectangle(rc.Left + 14, rc.Top, rc.Width - 28, 5);
                using (var tbBr = new LinearGradientBrush(topBar, accent, Color.FromArgb(60, accent), LinearGradientMode.Horizontal))
                using (var tbPath = RoundPath(topBar, 2))
                    g.FillPath(tbBr, tbPath);

                using (var fVal = new Font("Cairo", 15F, FontStyle.Bold))
                using (var bVal = new SolidBrush(accent))
                {
                    var sz = g.MeasureString(value, fVal);
                    g.DrawString(value, fVal, bVal,
                        rc.Left + (rc.Width - sz.Width) / 2f,
                        rc.Top + (cardH / 2f) - sz.Height / 2f - 6);
                }

                using (var fTit = new Font("Cairo", 9.5F))
                using (var bTit = new SolidBrush(ColorTranslator.FromHtml("#64748B")))
                {
                    var sz = g.MeasureString(title, fTit);
                    g.DrawString(title, fTit, bTit,
                        rc.Left + (rc.Width - sz.Width) / 2f,
                        rc.Bottom - sz.Height - 10);
                }
            }
        }

        private void RefreshMetrics() => _metricsCard?.Invalidate();

        // ??????????????????????????????????????????????????????
        //  GRIDS
        // ??????????????????????????????????????????????????????
        private Control BuildGrids()
        {
            var wrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            pnlInvoices = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 14, BorderThickness = 0, Visible = false };
            pnlInvoices.ShadowDecoration.Enabled = true; pnlInvoices.ShadowDecoration.Depth = 6; pnlInvoices.ShadowDecoration.Color = Color.FromArgb(12, 0, 0, 0);
            dgvInvoices = MakeGrid();
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn { Name = "I_No", HeaderText = "—ﬁ„ «·›« Ê—…", DataPropertyName = "Id", Width = 110 });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn { Name = "I_Date", HeaderText = "«· «—ÌŒ", DataPropertyName = "CreatedAt", Width = 180 });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn { Name = "I_Tot", HeaderText = "«·≈Ã„«·Ì", DataPropertyName = "TotalAmount", Width = 140 });
            dgvInvoices.Columns.Add(new DataGridViewTextBoxColumn { Name = "I_Paid", HeaderText = "«·„œ›Ê⁄", DataPropertyName = "PaidAmount", Width = 140 });
            pnlInvoices.Controls.Add(dgvInvoices);
            pnlInvoices.Controls.Add(MakeGridTitle("›Ê« Ì— «·„»Ì⁄« ", "#1a2f5e"));

            pnlInbound = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 14, BorderThickness = 0, Visible = false };
            pnlInbound.ShadowDecoration.Enabled = true; pnlInbound.ShadowDecoration.Depth = 6; pnlInbound.ShadowDecoration.Color = Color.FromArgb(12, 0, 0, 0);
            dgvInbound = MakeGrid();
            dgvInbound.Columns.Add(new DataGridViewTextBoxColumn { Name = "P_Id", HeaderText = "—ﬁ„ «·√„—", DataPropertyName = "Id", Width = 110 });
            dgvInbound.Columns.Add(new DataGridViewTextBoxColumn { Name = "P_Date", HeaderText = "«· «—ÌŒ", DataPropertyName = "CreatedAt", Width = 180 });
            dgvInbound.Columns.Add(new DataGridViewTextBoxColumn { Name = "P_Tot", HeaderText = "≈Ã„«·Ì «·Ê«—œ", DataPropertyName = "TotalValue", Width = 140 });
            pnlInbound.Controls.Add(dgvInbound);
            pnlInbound.Controls.Add(MakeGridTitle("√Ê«„— «·Ê«—œ« ", "#7c3aed"));

            wrapper.Controls.Add(pnlInvoices);
            wrapper.Controls.Add(pnlInbound);
            return wrapper;
        }

        private Guna2DataGridView MakeGrid()
        {
            var dgv = new Guna2DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersHeight = 44,
                EnableHeadersVisualStyles = false,
                GridColor = Color.White,
                ScrollBars = ScrollBars.Vertical
            };
            dgv.RowTemplate.Height = 50;
            dgv.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1e3a6e");
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Cairo", 11F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1e3a6e");
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Font = new Font("Cairo", 12F);
            dgv.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#374151");
            dgv.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#EEF2FF");
            dgv.DefaultCellStyle.SelectionForeColor = ColorTranslator.FromHtml("#0F172A");
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#F8FAFF");
            try { _dbProp?.SetValue(dgv, true); } catch { }

            dgv.CellPainting += (s, pe) =>
            {
                if (pe.RowIndex == -1)
                {
                    pe.Handled = true;
                    var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(pe.CellBounds, ColorTranslator.FromHtml("#1e3a6e"), ColorTranslator.FromHtml("#243f7a"), LinearGradientMode.Vertical))
                        g.FillRectangle(br, pe.CellBounds);
                    using (var font = new Font("Cairo", 11F, FontStyle.Bold))
                        g.DrawString(pe.Value?.ToString() ?? "", font, Brushes.White, pe.CellBounds,
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    using (var sp = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    { g.DrawLine(sp, pe.CellBounds.Left, pe.CellBounds.Top + 6, pe.CellBounds.Left, pe.CellBounds.Bottom - 6); g.DrawLine(sp, pe.CellBounds.Right - 1, pe.CellBounds.Top + 6, pe.CellBounds.Right - 1, pe.CellBounds.Bottom - 6); }
                    return;
                }
                try
                {
                    pe.Paint(pe.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Border);
                    using (var p = new Pen(ColorTranslator.FromHtml("#EEF0F5"), 1f))
                        pe.Graphics.DrawLine(p, pe.CellBounds.Left, pe.CellBounds.Bottom - 1, pe.CellBounds.Right, pe.CellBounds.Bottom - 1);
                    pe.Handled = true;
                }
                catch { }
            };
            return dgv;
        }

        private Panel MakeGridTitle(string text, string hexColor)
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
            pnl.Paint += (s, pe) =>
            {
                var g = pe.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var f = new Font("Cairo", 14F, FontStyle.Bold))
                using (var b = new SolidBrush(ColorTranslator.FromHtml(hexColor)))
                {
                    var sz = g.MeasureString(text, f);
                    g.DrawString(text, f, b, (pnl.Width - sz.Width) / 2f, (pnl.Height - sz.Height) / 2f);
                }
                using (var br = new LinearGradientBrush(new Rectangle(0, pnl.Height - 3, pnl.Width, 3),
                    ColorTranslator.FromHtml(hexColor), Color.FromArgb(30, ColorTranslator.FromHtml(hexColor)), LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, pnl.Height - 3, pnl.Width, 3);
            };
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  LOAD DATA
        // ??????????????????????????????????????????????????????
        private async Task LoadDataAsync()
        {
            try
            {
                var data = await Task.Run(() => _detailsService.GetCustomerFullDetails(_customerId));
                if (data == null) { MessageBox.Show("·«  ÊÃœ »Ì«‰«  ·Â–« «·⁄„Ì·."); Close(); return; }

                _loadedData = data;

                // ??  ÕœÌÀ Labels ??????????????????????????????
                lblName.Text = data.Customer?.Name ?? "-";
                lblPhone.Text = "Â« ›: " + (data.Customer?.Phone ?? "-");
                lblAddress.Text = "⁄‰Ê«‰: " + (data.Customer?.Address ?? "-");

                bool isInv = data.Customer?.CustomerType == CustomerType.Invoices;
                lblTypeBadge.Tag = isInv ? "›Ê« Ì—" : "Ê«—œ« ";
                lblTypeBadge.Text = " "; // trigger repaint
                lblTypeBadge.Invalidate();
                _bannerPanel.Invalidate(); // ≈⁄«œ… —”„ Avatar »«·Õ—› «·’Õ

                // ?? Grids + Metrics ????????????????????????????
                if (isInv)
                {
                    var invList = data.Invoices ?? new List<SalesInvoiceDto>();
                    pnlInvoices.Visible = true; pnlInbound.Visible = false;
                    dgvInvoices.DataSource = new BindingSource { DataSource = invList };

                    decimal tot = invList.Sum(x => x.TotalAmount);
                    decimal paid = invList.Sum(x => x.PaidAmount);
                    decimal rem = tot - paid;

                    _m1_title = "⁄œœ «·›Ê« Ì—"; _m1_value = invList.Count.ToString(); _m1_color = ColorTranslator.FromHtml("#1a2f5e");
                    _m2_title = "≈Ã„«·Ì ﬂ·Ì"; _m2_value = $"{tot:N2}"; _m2_color = ColorTranslator.FromHtml("#1565c0");
                    _m3_title = "≈Ã„«·Ì „œ›Ê⁄"; _m3_value = $"{paid:N2}"; _m3_color = ColorTranslator.FromHtml("#16A34A");
                    _m4_title = "«·„ »ﬁÌ"; _m4_value = $"{rem:N2}";
                    _m4_color = rem > 0 ? ColorTranslator.FromHtml("#DC2626") : ColorTranslator.FromHtml("#16A34A");
                }
                else
                {
                    var inbList = data.Inbounds ?? new List<InboundOrderDto>();
                    pnlInvoices.Visible = false; pnlInbound.Visible = true;
                    dgvInbound.DataSource = new BindingSource { DataSource = inbList };

                    decimal tot = inbList.Sum(x => x.TotalValue);
                    _m1_title = "⁄œœ «·√Ê«„—"; _m1_value = inbList.Count.ToString(); _m1_color = ColorTranslator.FromHtml("#7c3aed");
                    _m2_title = "≈Ã„«·Ì «·Ê«—œ« "; _m2_value = $"{tot:N2}"; _m2_color = ColorTranslator.FromHtml("#1565c0");
                    _m3_title = ""; _m3_value = ""; _m3_color = Color.Transparent;
                    _m4_title = ""; _m4_value = ""; _m4_color = Color.Transparent;
                }

                RefreshMetrics();
            }
            catch (Exception ex)
            {
                MessageBox.Show("›‘·  Õ„Ì· »Ì«‰«  «·⁄„Ì·: " + ex.Message, "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ??????????????????????????????????????????????????????
        //  TOAST
        // ??????????????????????????????????????????????????????
        private async void ShowToast(string msg, Color accent, Color bg)
        {
            var t = new Panel { Size = new Size(300, 46), BackColor = bg, Cursor = Cursors.Hand };
            using (var gp = new GraphicsPath())
            {
                gp.AddArc(0, 0, 20, 20, 180, 90); gp.AddArc(t.Width - 20, 0, 20, 20, 270, 90);
                gp.AddArc(t.Width - 20, t.Height - 20, 20, 20, 0, 90); gp.AddArc(0, t.Height - 20, 20, 20, 90, 90);
                gp.CloseFigure(); t.Region = new Region(gp);
            }
            t.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pe.Graphics.FillRectangle(new SolidBrush(accent), 0, 6, 4, t.Height - 12);
                using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                using (var tb = new SolidBrush(ColorTranslator.FromHtml("#1F2937")))
                    pe.Graphics.DrawString(msg, f, tb, new RectangleF(8, 0, t.Width - 12, t.Height),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            t.Location = new Point(Width - t.Width - 20, Height - t.Height - 30);
            Controls.Add(t); t.BringToFront();
            t.Click += (s, e) => { try { Controls.Remove(t); t.Dispose(); } catch { } };
            for (int i = 0; i <= 10; i++) { t.Location = new Point(t.Location.X, Height - t.Height - 30 + (10 - i) * 2); await Task.Delay(10); }
            await Task.Delay(2800);
            for (int i = 0; i <= 10; i++) { try { t.Location = new Point(t.Location.X, Height - t.Height - 30 + i * 2); } catch { break; } await Task.Delay(10); }
            try { Controls.Remove(t); t.Dispose(); } catch { }
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private static string GetInner(Exception ex)
        {
            if (ex == null) return "";
            var e = ex; while (e.InnerException != null) e = e.InnerException; return e.Message;
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
}