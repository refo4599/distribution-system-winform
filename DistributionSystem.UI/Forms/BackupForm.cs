using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DistributionSystem.Business.Services;
using Guna.UI2.WinForms;

namespace DistributionSystem.UI.Forms
{
    public class BackupForm : Form
    {
        private readonly BackupService _backupService = new BackupService();

        public BackupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1, 1);
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Opacity = 0;
            this.Load += (s, e) => ShowBackupPopup();
        }

        private void ShowBackupPopup()
        {
            var sc = Screen.FromControl(this).WorkingArea;

            // ???????????????????????????????????????????????????????
            //  OVERLAY ó ‰›” √”·Ê» CustomerForm
            // ???????????????????????????????????????????????????????
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

            // ???????????????????????????????????????????????????????
            //  POPUP FORM
            // ???????????????????????????????????????????????????????
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

            // “Ê«Ì« „” œÌ—…
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
            {
                try { overlay.Close(); overlay.Dispose(); } catch { }
                try { this.Close(); this.Dispose(); } catch { }
            };
            overlay.Click += (s, e) => popupForm.Close();

            // ???????????????????????????????????????????????????????
            //  POPUP CONTENT
            // ???????????????????????????????????????????????????????
            var popup = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 0,
                FillColor = Color.White,
                BackColor = Color.White
            };
            popup.ShadowDecoration.Enabled = true;
            popup.ShadowDecoration.Depth = 32;
            popup.ShadowDecoration.Color = Color.FromArgb(70, 0, 0, 60);
            popupForm.Controls.Add(popup);

            Action closePopup = () =>
            {
                try { popupForm.Close(); popupForm.Dispose(); } catch { }
            };

            // ???????????????????????????????????????????????????????
            //  HEADER
            // ???????????????????????????????????????????????????????
            var pnlHead = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            pnlHead.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, pnlHead.Width, pnlHead.Height);
                using (var br = new LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#1a2f5e"),
                    ColorTranslator.FromHtml("#1565c0"),
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, rc);

                using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    for (int x = 8; x < pnlHead.Width; x += 20)
                        for (int y = 6; y < pnlHead.Height; y += 20)
                            g.FillEllipse(dot, x, y, 2, 2);

                using (var cb = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                    g.FillEllipse(cb, pnlHead.Width - 100, -40, 180, 180);

                using (var tf = new Font("Cairo", 17F, FontStyle.Bold))
                using (var tb = new SolidBrush(Color.White))
                {
                    var tsz = g.MeasureString("‰”Œ «Õ Ì«ÿÌ Ê«” ⁄«œ…", tf);
                    g.DrawString("‰”Œ «Õ Ì«ÿÌ Ê«” ⁄«œ…", tf, tb,
                        pnlHead.Width - tsz.Width - 60, 16);
                }
                using (var sf = new Font("Cairo", 10F))
                using (var sb = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    var ssz = g.MeasureString("«Õ›Ÿ »Ì«‰« ﬂ √Ê «” ⁄œÂ« »”ÂÊ·…", sf);
                    g.DrawString("«Õ›Ÿ »Ì«‰« ﬂ √Ê «” ⁄œÂ« »”ÂÊ·…", sf, sb,
                        pnlHead.Width - ssz.Width - 60, 54);
                }
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
            btnX.Click += (s, e) => closePopup();
            pnlHead.Controls.Add(btnX);
            pnlHead.Layout += (s, e) => btnX.Location = new Point(25, 20);

            // ???????????????????????????????????????????????????????
            //  BODY
            // ???????????????????????????????????????????????????????
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(28, 20, 28, 0),
                RightToLeft = RightToLeft.Yes
            };

            // “—«— «·‰”Œ «·«Õ Ì«ÿÌ
            var btnBackup = new Guna2Button
            {
                Text = "‰”Œ «Õ Ì«ÿÌ «·¬‰",
                Dock = DockStyle.Top,
                Height = 70,
                FillColor = ColorTranslator.FromHtml("#065F46"),
                ForeColor = Color.White,
                BorderRadius = 14,
                Font = new Font("Cairo", 13F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Animated = true,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnBackup.HoverState.FillColor = ColorTranslator.FromHtml("#047857");
            btnBackup.ShadowDecoration.Enabled = true;
            btnBackup.ShadowDecoration.Color = Color.FromArgb(40, 6, 95, 70);
            btnBackup.ShadowDecoration.Depth = 8;

            var lblBackupDesc = new Label
            {
                Text = "ÌÕ›Ÿ ‰”Œ… ﬂ«„·… „‰ «·»Ì«‰«  ›Ì „ﬂ«‰  Œ «—Â",
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = ColorTranslator.FromHtml("#6B7280"),
                BackColor = Color.Transparent,
                Font = new Font("Cairo", 9.5F),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var sep = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = ColorTranslator.FromHtml("#E5E7EB"),
                Margin = new Padding(0, 8, 0, 8)
            };

            var sp1 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };
            var sp2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };
            var sp3 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // “—«— «·«” ⁄«œ…
            var btnRestore = new Guna2Button
            {
                Text = "«” ⁄«œ… „‰ ‰”Œ… «Õ Ì«ÿÌ…",
                Dock = DockStyle.Top,
                Height = 70,
                FillColor = ColorTranslator.FromHtml("#92400E"),
                ForeColor = Color.White,
                BorderRadius = 14,
                Font = new Font("Cairo", 13F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Animated = true
            };
            btnRestore.HoverState.FillColor = ColorTranslator.FromHtml("#B45309");
            btnRestore.ShadowDecoration.Enabled = true;
            btnRestore.ShadowDecoration.Color = Color.FromArgb(40, 146, 64, 14);
            btnRestore.ShadowDecoration.Depth = 8;

            var lblRestoreDesc = new Label
            {
                Text = "” Õ–› «·»Ì«‰«  «·Õ«·Ì… Ê ” »œ·Â« »«·‰”Œ… «·„Œ «—…",
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = ColorTranslator.FromHtml("#B45309"),
                BackColor = Color.Transparent,
                Font = new Font("Cairo", 9.5F),
                TextAlign = ContentAlignment.MiddleCenter
            };

            //  — Ì» «·‹ controls „‰  Õ  ·›Êﬁ (Dock Top „⁄ﬂÊ”)
            body.Controls.Add(sp3);
            body.Controls.Add(lblRestoreDesc);
            body.Controls.Add(btnRestore);
            body.Controls.Add(sp2);
            body.Controls.Add(sep);
            body.Controls.Add(sp1);
            body.Controls.Add(lblBackupDesc);
            body.Controls.Add(btnBackup);

            // ???????????????????????????????????????????????????????
            //  FOOTER
            // ???????????????????????????????????????????????????????
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = ColorTranslator.FromHtml("#F8FAFF"),
                Padding = new Padding(24, 10, 24, 14)
            };
            footer.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 2f))
                    g.DrawLine(pen, 0, 0, footer.Width, 0);
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 2, footer.Width, 2),
                    ColorTranslator.FromHtml("#4E73DF"),
                    ColorTranslator.FromHtml("#E8EDFF"),
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(br, 0, 2, footer.Width, 2);
            };

            var btnClose = new Guna2Button
            {
                Dock = DockStyle.Fill,
                Text = "≈€·«ﬁ",
                BorderRadius = 12,
                FillColor = ColorTranslator.FromHtml("#374151"),
                ForeColor = Color.White,
                Font = new Font("Cairo", 12F, FontStyle.Bold),
                Animated = true
            };
            btnClose.HoverState.FillColor = ColorTranslator.FromHtml("#4B5563");
            btnClose.Click += (s, e) => closePopup();
            footer.Controls.Add(btnClose);

            // ???????????????????????????????????????????????????????
            //  EVENTS
            // ???????????????????????????????????????????????????????
            btnBackup.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Title = "«Œ — „ﬂ«‰ Õ›Ÿ «·‰”Œ… «·«Õ Ì«ÿÌ…";
                    dlg.Filter = "„·› ‰”Œ… «Õ Ì«ÿÌ… (*.bak)|*.bak";
                    dlg.FileName = $"DistributionDb_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.bak";
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    try
                    {
                        _backupService.BackupToPath(dlg.FileName);
                        MessageBox.Show(
                            $" „ Õ›Ÿ «·‰”Œ… «·«Õ Ì«ÿÌ… »‰Ã«Õ!\n\n«·„ﬂ«‰:\n{dlg.FileName}",
                            "‰”Œ… «Õ Ì«ÿÌ…", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"›‘· ⁄„· «·‰”Œ… «·«Õ Ì«ÿÌ…:\n{ex.Message}",
                            "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            btnRestore.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(
                    " Õ–Ì—!\n\n⁄„·Ì… «·«” ⁄«œ… ” Õ–› Ã„Ì⁄ «·»Ì«‰«  «·Õ«·Ì… Ê ” »œ·Â« »«·‰”Œ… «·«Õ Ì«ÿÌ….\n\nÂ· √‰  „ √ﬂœø",
                    " √ﬂÌœ «·«” ⁄«œ…", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = "«Œ — „·› «·‰”Œ… «·«Õ Ì«ÿÌ…";
                    dlg.Filter = "„·› ‰”Œ… «Õ Ì«ÿÌ… (*.bak)|*.bak";
                    dlg.InitialDirectory = _backupService.AutoBackupFolder_Public;
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    try
                    {
                        _backupService.RestoreFromPath(dlg.FileName);
                        MessageBox.Show(
                            " „ «” ⁄«œ… «·»Ì«‰«  »‰Ã«Õ!\n\n”Ì „ ≈€·«ﬁ «·»—‰«„Ã «·¬‰ ó «› ÕÂ „—… √Œ—Ï.",
                            " „  «·«” ⁄«œ…", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Application.Exit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"›‘· «” ⁄«œ… «·»Ì«‰« :\n{ex.Message}",
                            "Œÿ√", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            // ???????????????????????????????????????????????????????
            //  ASSEMBLE
            // ???????????????????????????????????????????????????????
            popup.Controls.Add(body);
            popup.Controls.Add(footer);
            popup.Controls.Add(pnlHead);

            popupForm.ShowDialog(this);
        }
    }
}