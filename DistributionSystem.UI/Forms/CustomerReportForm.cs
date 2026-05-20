using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DistributionSystem.Business.Services;
using DistributionSystem.Business.Dtos;

namespace DistributionSystem.UI.Forms
{
    public partial class CustomerReportForm : Form
    {
        private readonly CustomerService _customerSvc = new CustomerService();
        private readonly CustomerDetailsService _detailsSvc = new CustomerDetailsService();
        private readonly CustomerDetailsReportService _reportSvc = new CustomerDetailsReportService();

        private static readonly Color ThemeDark = ColorTranslator.FromHtml("#1a2f5e");
        private static readonly Color ThemeMid = ColorTranslator.FromHtml("#1565c0");
        private static readonly Color ThemeAccent = ColorTranslator.FromHtml("#4E73DF");

        private List<CustomerDto> _customers = new List<CustomerDto>();
        private ComboBox _cbo;
        private Guna2Button _btnDownload;
        private Label _lblStatus;
        private Panel _previewCard;
        private Bitmap _bannerCache;

        private string _previewName = "";
        private string _previewPhone = "";
        private string _previewType = "";

        public CustomerReportForm()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            BuildLayout();
            Shown += async (s, e) => await LoadCustomersAsync();
        }

        private void BuildLayout()
        {
            SuspendLayout();
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = ColorTranslator.FromHtml("#EEF0F5");
            Padding = new Padding(0);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildBanner(), 0, 0);
            root.Controls.Add(BuildBody(), 0, 1);
            Controls.Add(root);
            root.BringToFront();
            EnableDbAll(this);
            ResumeLayout(true);
        }

        // ??????????????????????????????????????????????????????
        //  BANNER
        // ??????????????????????????????????????????????????????
        private Panel BuildBanner()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Height = 88, BackColor = Color.Transparent };
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            banner.Paint += (s, e) =>
            {
                if (_bannerCache == null ||
                    _bannerCache.Width != banner.Width ||
                    _bannerCache.Height != banner.Height)
                {
                    _bannerCache?.Dispose();
                    if (banner.Width <= 0 || banner.Height <= 0) return;
                    _bannerCache = new Bitmap(banner.Width, banner.Height);
                    using (var g = Graphics.FromImage(_bannerCache))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                        var rc = new Rectangle(0, 0, banner.Width, banner.Height);

                        using (var br = new LinearGradientBrush(rc, ThemeDark, ThemeMid, LinearGradientMode.Horizontal))
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

                        using (var f = new Font("Cairo", 22F, FontStyle.Bold))
                        using (var b = new SolidBrush(Color.White))
                        {
                            var sz = g.MeasureString(" ﬁ«—Ì— «·⁄„·«¡", f);
                            g.DrawString(" ﬁ«—Ì— «·⁄„·«¡", f, b, banner.Width - sz.Width - 24, 8);
                        }

                        using (var f2 = new Font("Cairo", 9.5F))
                        using (var b2 = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                            g.DrawString("«Œ — ⁄„Ì· ·«” Œ—«Ã  ﬁ—Ì—Â «·ﬂ«„·",
                                f2, b2, banner.Width - 280, 46);

                        using (var b1 = new SolidBrush(ColorTranslator.FromHtml("#64B5F6")))
                            g.FillRectangle(b1, banner.Width - 42, 44, 38, 3);
                        using (var b2 = new SolidBrush(Color.FromArgb(120, 100, 181, 246)))
                            g.FillRectangle(b2, banner.Width - 60, 44, 14, 3);
                    }
                }
                e.Graphics.DrawImage(_bannerCache, 0, 0);
            };

            banner.Resize += (s, e) => { _bannerCache?.Dispose(); _bannerCache = null; };
            pnl.Controls.Add(banner);
            return pnl;
        }

        // ??????????????????????????????????????????????????????
        //  BODY
        // ??????????????????????????????????????????????????????
        private Panel BuildBody()
        {
            var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            const int cardW = 580, cardH = 470;
            var center = new Panel { BackColor = Color.Transparent, Size = new Size(cardW, cardH) };
            outer.Controls.Add(center);
            outer.Resize += (s, e) =>
            {
                int x = Math.Max(0, (outer.Width - cardW) / 2);
                int y = Math.Max(0, (outer.Height - cardH) / 2 - 10);
                center.SetBounds(x, y, cardW, cardH);
            };

            var card = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = 20,
                BorderThickness = 0
            };
            card.ShadowDecoration.Enabled = true;
            card.ShadowDecoration.Depth = 24;
            card.ShadowDecoration.Color = Color.FromArgb(28, 0, 0, 0);

            // ?? ÂÌœ— «·»ÿ«ﬁ… ó „” ÿÌ· ﬂ«„· »œÊ‰ “Ê«Ì« »Ì÷«¡ ??
            var cardHdr = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.Transparent };
            cardHdr.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var rc = new Rectangle(0, 0, cardHdr.Width, cardHdr.Height);

                // „·¡ ﬂ«„· »œÊ‰ “Ê«Ì« ó Ìÿ«»ﬁ ·Ê‰ «·ÂÌœ—
                using (var br = new LinearGradientBrush(rc, ThemeDark, ThemeMid, LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);

                // ‰ﬁ«ÿ œÌﬂÊ—
                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    for (int x = 8; x < cardHdr.Width; x += 20)
                        for (int y = 4; y < cardHdr.Height; y += 20)
                            g.FillEllipse(dot, x, y, 2, 2);

                // «·⁄‰Ê«‰
                using (var f = new Font("Cairo", 15F, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    string title = " ﬁ—Ì— «·⁄„Ì·";
                    var sz = g.MeasureString(title, f);
                    g.DrawString(title, f, b,
                        cardHdr.Width - sz.Width - 24,
                        (cardHdr.Height - sz.Height) / 2f);
                }
            };

            // ?? body «·»ÿ«ﬁ… ??????????????????????????????????
            var cardBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(28, 18, 28, 18)
            };

            // label ›Êﬁ «·‹ ComboBox
            var lblCustLbl = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
            lblCustLbl.Paint += (s, pe) =>
            {
                pe.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var f = new Font("Cairo", 10F, FontStyle.Bold))
                using (var b = new SolidBrush(ThemeDark))
                {
                    var sz = pe.Graphics.MeasureString("«Œ — «·⁄„Ì·", f);
                    pe.Graphics.DrawString("«Œ — «·⁄„Ì·", f, b,
                        lblCustLbl.Width - sz.Width - 2,
                        (lblCustLbl.Height - sz.Height) / 2f);
                }
            };

            // ?? ComboBox ??????????????????????????????????????
            _cbo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 36,
                Font = new Font("Cairo", 11F),
                BackColor = Color.White,
                ForeColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                RightToLeft = RightToLeft.Yes,
                Dock = DockStyle.Top,
                Height = 48
            };

            _cbo.DrawItem += (s2, de) =>
            {
                if (de.Index < 0) return;
                de.DrawBackground();
                bool hot = (de.State & DrawItemState.Selected) != 0;
                using (var br = new SolidBrush(hot ? ColorTranslator.FromHtml("#EEF2FF") : Color.White))
                    de.Graphics.FillRectangle(br, de.Bounds);
                string txt = _cbo.GetItemText(_cbo.Items[de.Index]);
                de.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var f = new Font("Cairo", 10.5F, hot ? FontStyle.Bold : FontStyle.Regular))
                using (var b = new SolidBrush(hot ? ThemeDark : ColorTranslator.FromHtml("#111827")))
                    de.Graphics.DrawString(txt, f, b,
                        new RectangleF(de.Bounds.X + 8, de.Bounds.Y,
                                       de.Bounds.Width - 16, de.Bounds.Height),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Far,
                            LineAlignment = StringAlignment.Center
                        });
            };

            var cboOverlay = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
            _cbo.SetBounds(0, 0, 560, 48);
            cboOverlay.Controls.Add(_cbo);
            cboOverlay.Resize += (s2, e2) => _cbo.SetBounds(0, 0, cboOverlay.Width, 48);

            cboOverlay.Paint += (s2, pe2) =>
            {
                var g = pe2.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var rc2 = new Rectangle(0, 0, cboOverlay.Width - 1, cboOverlay.Height - 1);

                // Œ·›Ì… »Ì÷«¡
                using (var br = new SolidBrush(Color.White))
                using (var p2 = RoundPath(rc2, 10))
                    g.FillPath(br, p2);

                // Õœ
                bool focused = _cbo.DroppedDown || _cbo.Focused;
                using (var pen2 = new Pen(
                    focused ? ColorTranslator.FromHtml("#4E73DF")
                            : ColorTranslator.FromHtml("#C7D2FE"),
                    focused ? 2f : 1.5f))
                using (var p2 = RoundPath(rc2, 10))
                    g.DrawPath(pen2, p2);

                // ”Â„
                int ax = 22, ay = cboOverlay.Height / 2;
                using (var ap = new Pen(ThemeAccent, 2.5f))
                {
                    g.DrawLine(ap, ax + 6, ay - 3, ax, ay + 4);
                    g.DrawLine(ap, ax, ay + 4, ax - 6, ay - 3);
                }

                // placeholder √Ê «·‰’ «·„Õœœ
                string selTxt;
                bool isPh;
                if (_cbo.SelectedIndex < 0)
                {
                    selTxt = "«Œ — «·⁄„Ì· · Õ„Ì· ›« Ê— Â";
                    isPh = true;
                }
                else
                {
                    selTxt = _cbo.GetItemText(_cbo.SelectedItem);
                    isPh = false;
                }

                using (var f = new Font("Cairo", 11F, isPh ? FontStyle.Regular : FontStyle.Bold))
                using (var b = new SolidBrush(isPh
                    ? ColorTranslator.FromHtml("#94A3B8") : ThemeDark))
                    g.DrawString(selTxt, f, b,
                        new RectangleF(42, 0, cboOverlay.Width - 58, cboOverlay.Height),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Far,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        });
            };

            _cbo.SelectedIndexChanged += (s2, e2) => { cboOverlay.Invalidate(); OnCustomerSelected(); };
            _cbo.DropDown += (s2, e2) => cboOverlay.Invalidate();
            _cbo.DropDownClosed += (s2, e2) => cboOverlay.Invalidate();

            // ?? »ÿ«ﬁ… „⁄«Ì‰… «·⁄„Ì· ???????????????????????????
            _previewCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = Color.Transparent,
                Visible = false
            };
            _previewCard.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                var rc = new Rectangle(0, 0, _previewCard.Width - 1, _previewCard.Height - 1);

                // Œ·›Ì…
                using (var br = new LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#EFF6FF"),
                    ColorTranslator.FromHtml("#F0FDF4"),
                    LinearGradientMode.ForwardDiagonal))
                using (var path = RoundPath(rc, 12))
                    g.FillPath(br, path);

                // Õœ
                using (var pen = new Pen(ColorTranslator.FromHtml("#BFDBFE"), 1.5f))
                using (var path = RoundPath(rc, 12))
                    g.DrawPath(pen, path);

                // ‘—Ìÿ Ã«‰»Ì
                using (var br = new LinearGradientBrush(
                    new Rectangle(rc.Right - 5, rc.Top + 10, 5, rc.Height - 20),
                    ThemeMid, ThemeAccent, LinearGradientMode.Vertical))
                    g.FillRectangle(br, rc.Right - 5, rc.Top + 10, 5, rc.Height - 20);

                // √›« «—
                int av = 60, ax2 = rc.Right - 18 - av;
                int ay2 = rc.Top + (rc.Height - av) / 2;
                using (var sh = new SolidBrush(Color.FromArgb(20, ThemeAccent)))
                    g.FillEllipse(sh, ax2 + 2, ay2 + 2, av, av);
                using (var br = new LinearGradientBrush(
                    new Rectangle(ax2, ay2, av, av),
                    ThemeDark, ThemeMid, LinearGradientMode.ForwardDiagonal))
                    g.FillEllipse(br, ax2, ay2, av, av);

                // Õ—› «·√›« «—
                string letter = !string.IsNullOrEmpty(_previewName)
                    ? _previewName[0].ToString() : "⁄";
                using (var f = new Font("Cairo", 22F, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    var sz = g.MeasureString(letter, f);
                    g.DrawString(letter, f, b,
                        ax2 + (av - sz.Width) / 2f,
                        ay2 + (av - sz.Height) / 2f);
                }

                // «·«”„ ﬂ«„·
                using (var f = new Font("Cairo", 14F, FontStyle.Bold))
                using (var b = new SolidBrush(ThemeDark))
                {
                    float maxW = ax2 - 14 - (rc.Left + 12);
                    g.DrawString(_previewName, f, b,
                        new RectangleF(rc.Left + 12, rc.Top + 10, maxW, 30),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Far,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.None
                        });
                }

                // «·Â« ›
                string phoneDisplay = string.IsNullOrEmpty(_previewPhone)
                    ? "·« ÌÊÃœ Â« ›" : _previewPhone;
                using (var f = new Font("Cairo", 10.5F))
                using (var b = new SolidBrush(ColorTranslator.FromHtml("#374151")))
                {
                    float maxW = ax2 - 14 - (rc.Left + 12);
                    g.DrawString(phoneDisplay, f, b,
                        new RectangleF(rc.Left + 12, rc.Top + 46, maxW, 26),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Far,
                            LineAlignment = StringAlignment.Center
                        });
                }

                // ‘«—… «·‰Ê⁄
                bool isInv = _previewType.Contains("Invoice") || _previewType.Contains("Invoices");
                string badgeTxt = isInv ? "›Ê« Ì— „»Ì⁄« " : "Ê«—œ« ";
                Color badgeBg = isInv ? ColorTranslator.FromHtml("#D1FAE5") : ColorTranslator.FromHtml("#DBEAFE");
                Color badgeFg = isInv ? ColorTranslator.FromHtml("#065F46") : ColorTranslator.FromHtml("#1E40AF");
                Color badgeBd = isInv ? ColorTranslator.FromHtml("#6EE7B7") : ColorTranslator.FromHtml("#93C5FD");

                using (var f = new Font("Cairo", 9.5F, FontStyle.Bold))
                {
                    var bsz = g.MeasureString(badgeTxt, f);
                    int bw = (int)bsz.Width + 20, bh = 26;
                    int bx = ax2 - 14 - bw, by = rc.Top + 84;
                    var brrc = new Rectangle(bx, by, bw, bh);
                    using (var br = new SolidBrush(badgeBg))
                    using (var path2 = RoundPath(brrc, bh / 2))
                    {
                        g.FillPath(br, path2);
                        g.DrawPath(new Pen(badgeBd, 1f), path2);
                    }
                    g.DrawString(badgeTxt, f, new SolidBrush(badgeFg),
                        new RectangleF(bx, by, bw, bh),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
                }
            };

            // Status
            _lblStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 0,
                Font = new Font("Cairo", 9.5F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            // “— «· Õ„Ì·
            _btnDownload = new Guna2Button
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Text = " Õ„Ì·  ﬁ—Ì— «·⁄„Ì·",
                BorderRadius = 14,
                FillColor = ColorTranslator.FromHtml("#94A3B8"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 12F, FontStyle.Bold),
                Enabled = false
            };
            _btnDownload.HoverState.FillColor = ThemeDark;
            _btnDownload.ShadowDecoration.Enabled = true;
            _btnDownload.ShadowDecoration.Depth = 8;
            _btnDownload.ShadowDecoration.Color = Color.FromArgb(35, 21, 101, 192);
            _btnDownload.Click += async (s, e) => await DownloadReportAsync();

            Panel Sp(int h) => new Panel { Dock = DockStyle.Top, Height = h, BackColor = Color.Transparent };

            cardBody.Controls.Add(_btnDownload);
            cardBody.Controls.Add(Sp(4));
            cardBody.Controls.Add(_lblStatus);
            cardBody.Controls.Add(Sp(10));
            cardBody.Controls.Add(_previewCard);
            cardBody.Controls.Add(Sp(10));
            cardBody.Controls.Add(cboOverlay);
            cardBody.Controls.Add(Sp(6));
            cardBody.Controls.Add(lblCustLbl);

            card.Controls.Add(cardBody);
            card.Controls.Add(cardHdr);
            center.Controls.Add(card);
            return outer;
        }

        // ??????????????????????????????????????????????????????
        //  LOAD CUSTOMERS
        // ??????????????????????????????????????????????????????
        private async Task LoadCustomersAsync()
        {
            SetStatus("Ã«—Ú  Õ„Ì· ﬁ«∆„… «·⁄„·«¡...", "#64748B");
            await Task.Run(() =>
            {
                try
                {
                    _customers = (_customerSvc.GetAll() ?? Enumerable.Empty<CustomerDto>())
                        .OrderBy(c => c.Name).ToList();
                }
                catch { _customers = new List<CustomerDto>(); }
            });

            if (InvokeRequired) { Invoke(new Action(FillCombo)); return; }
            FillCombo();
        }

        private void FillCombo()
        {
            _cbo.Items.Clear();
            foreach (var c in _customers)
                _cbo.Items.Add(new CboItem { Id = c.Id, Name = c.Name, Dto = c });
            _cbo.DisplayMember = "Name";
            SetStatus(
                _customers.Count > 0 ? $" „  Õ„Ì· {_customers.Count} ⁄„Ì·" : "·« ÌÊÃœ ⁄„·«¡",
                _customers.Count > 0 ? "#10B981" : "#EF4444");
        }

        // ??????????????????????????????????????????????????????
        //  ON CUSTOMER SELECTED
        // ??????????????????????????????????????????????????????
        private void OnCustomerSelected()
        {
            if (_cbo.SelectedItem is CboItem sel && sel.Id > 0)
            {
                _previewName = sel.Dto?.Name ?? "";
                _previewPhone = sel.Dto?.Phone ?? "";
                _previewType = sel.Dto?.CustomerType.ToString() ?? "";

                _previewCard.Visible = true;
                _previewCard.Invalidate();

                _btnDownload.Enabled = true;
                _btnDownload.FillColor = ThemeMid;
                SetStatus("", "#64748B");
            }
            else
            {
                _previewName = _previewPhone = _previewType = "";
                _previewCard.Visible = false;
                _btnDownload.Enabled = false;
                _btnDownload.FillColor = ColorTranslator.FromHtml("#94A3B8");
            }
        }

        // ??????????????????????????????????????????????????????
        //  DOWNLOAD
        // ??????????????????????????????????????????????????????
        private async Task DownloadReportAsync()
        {
            if (!(_cbo.SelectedItem is CboItem sel) || sel.Id <= 0) return;
            _btnDownload.Enabled = false;
            _btnDownload.Text = "Ã«—Ú «· Õ÷Ì—...";
            SetStatus("Ã«—Ú Ã·» »Ì«‰«  «·⁄„Ì·...", "#4E73DF");
            try
            {
                CustomerFullDetailsDto data = null;
                await Task.Run(() => { data = _detailsSvc.GetCustomerFullDetails(sel.Id); });
                if (data == null) { SetStatus("·„ Ì „ «·⁄ÀÊ— ⁄·Ï »Ì«‰« ", "#EF4444"); return; }

                SetStatus("Ã«—Ú ≈‰‘«¡ „·› PDF...", "#4E73DF");
                byte[] pdfBytes = await Task.Run(() => _reportSvc.GenerateCustomerReport(data));

                string safeName = (data.Customer?.Name ?? sel.Name ?? "⁄„Ì·")
                    .Replace("/", "-").Replace("\\", "-").Replace(":", "-");

                using (var sfd = new SaveFileDialog
                {
                    Title = "Õ›Ÿ  ﬁ—Ì— «·⁄„Ì·",
                    Filter = "PDF|*.pdf",
                    FileName = $" ﬁ—Ì—_{safeName}_{DateTime.Today:yyyy-MM-dd}.pdf",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllBytes(sfd.FileName, pdfBytes);
                        SetStatus(" „ Õ›Ÿ «· ﬁ—Ì— »‰Ã«Õ", "#10B981");
                        ShowSuccessToast($" „  Õ„Ì·  ﬁ—Ì— {safeName}");
                    }
                    else SetStatus(" „ ≈·€«¡ «·Õ›Ÿ", "#64748B");
                }
            }
            catch (Exception ex)
            {
                SetStatus("›‘· ≈‰‘«¡ «· ﬁ—Ì—", "#EF4444");
                MessageBox.Show("›‘· ≈‰‘«¡ «· ﬁ—Ì—:\n" + ex.Message, "Œÿ√",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnDownload.Enabled = true;
                _btnDownload.Text = " Õ„Ì·  ﬁ—Ì— «·⁄„Ì·";
            }
        }

        // ??????????????????????????????????????????????????????
        //  STATUS
        // ??????????????????????????????????????????????????????
        private void SetStatus(string msg, string colorHex)
        {
            if (_lblStatus == null) return;
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = ColorTranslator.FromHtml(colorHex);
            _lblStatus.Height = string.IsNullOrEmpty(msg) ? 0 : 22;
        }

        // ??????????????????????????????????????????????????????
        //  TOAST
        // ??????????????????????????????????????????????????????
        private async void ShowSuccessToast(string msg)
        {
            var toast = new Panel { Size = new Size(340, 52), BackColor = ColorTranslator.FromHtml("#EEF2FF") };
            using (var gp = new GraphicsPath())
            {
                gp.AddArc(0, 0, 20, 20, 180, 90);
                gp.AddArc(toast.Width - 20, 0, 20, 20, 270, 90);
                gp.AddArc(toast.Width - 20, toast.Height - 20, 20, 20, 0, 90);
                gp.AddArc(0, toast.Height - 20, 20, 20, 90, 90);
                gp.CloseFigure();
                toast.Region = new Region(gp);
            }
            toast.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(ThemeAccent, 1.5f))
                using (var path = RoundPath(new Rectangle(0, 0, toast.Width - 1, toast.Height - 1), 10))
                    pe.Graphics.DrawPath(pen, path);
                pe.Graphics.FillRectangle(new SolidBrush(ThemeAccent), 0, 8, 4, toast.Height - 16);
                using (var f = new Font("Cairo", 10.5F, FontStyle.Bold))
                using (var b = new SolidBrush(ThemeDark))
                    pe.Graphics.DrawString(msg, f, b,
                        new RectangleF(4, 0, toast.Width - 8, toast.Height),
                        new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
            };
            toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40);
            Controls.Add(toast); toast.BringToFront();
            toast.Click += (s, e) => { try { Controls.Remove(toast); toast.Dispose(); } catch { } };
            for (int i = 0; i <= 100; i += 10)
            { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + (100 - i) / 5); await Task.Delay(8); }
            await Task.Delay(2800);
            for (int i = 0; i <= 100; i += 10)
            { try { toast.Location = new Point(Width - toast.Width - 32, Height - toast.Height - 40 + i / 5); } catch { break; } await Task.Delay(8); }
            try { Controls.Remove(toast); toast.Dispose(); } catch { }
        }

        // ??????????????????????????????????????????????????????
        //  HELPERS
        // ??????????????????????????????????????????????????????
        private static readonly System.Reflection.PropertyInfo _dbProp =
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        private static void EnableDbAll(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                try { _dbProp?.SetValue(c, true); } catch { }
                if (c.Controls.Count > 0) EnableDbAll(c);
            }
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

        private class CboItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public CustomerDto Dto { get; set; }
            public override string ToString() => Name ?? "";
        }
    }
}