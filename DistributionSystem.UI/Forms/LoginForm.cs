using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DistributionSystem.UI.Forms
{
    public partial class LoginForm : Form
    {
        private TextBox txtEmail, txtPassword;
        private Button btnLogin;
        private Label lblError;
        private CheckBox chkShow;
        private Panel wrapEmail, wrapPass;
        private bool _btnHover = false;

        private const int FW = 420;
        private const int HH = 130;
        private const int CW = 360;
        private const int CPad = 34;
        private const int IW = 292;

        public LoginForm()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            Text = "شركة بصوص — تسجيل الدخول";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ColorTranslator.FromHtml("#E8EDF5");
            Font = new Font("Cairo", 9.5F);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            var header = new Panel { Size = new Size(FW, HH), Location = Point.Empty, BackColor = Color.Transparent };
            header.Paint += PaintHeader;
            Controls.Add(header);

            int cardX = (FW - CW) / 2;
            int cardY = HH - 20;
            int cy = 28;

            var card = new Panel { Location = new Point(cardX, cardY), BackColor = Color.White };
            card.Paint += (s, e) => PaintCard(e.Graphics, card.Width, card.Height);

            var lblTitle = MakeLbl("تسجيل الدخول", 15F, "#0F172A", FontStyle.Bold, CPad, cy, IW, 34, ContentAlignment.MiddleCenter); cy += 34 + 4;
            var lblSub = MakeLbl("أدخل بياناتك للدخول إلى النظام", 9F, "#94A3B8", FontStyle.Regular, CPad, cy, IW, 20, ContentAlignment.MiddleCenter); cy += 20 + 22;

            var lblEmail = MakeLbl("البريد الإلكتروني", 9.5F, "#374151", FontStyle.Bold, CPad, cy, IW, 20, ContentAlignment.MiddleRight); cy += 20 + 6;
            wrapEmail = MakeInputWrap(CPad, cy, IW, 46);
            txtEmail = MakeTextBox(wrapEmail, false);
            cy += 46 + 16;

            var lblPass = MakeLbl("كلمة المرور", 9.5F, "#374151", FontStyle.Bold, CPad, cy, IW, 20, ContentAlignment.MiddleRight); cy += 20 + 6;
            wrapPass = MakeInputWrap(CPad, cy, IW, 46);
            txtPassword = MakeTextBox(wrapPass, true);
            cy += 46 + 10;

            chkShow = new CheckBox
            {
                Text = "إظهار كلمة المرور",
                Font = new Font("Cairo", 9F),
                ForeColor = ColorTranslator.FromHtml("#64748B"),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(CPad, cy),
                Cursor = Cursors.Hand,
                RightToLeft = RightToLeft.Yes
            };
            chkShow.CheckedChanged += (s, e) => txtPassword.PasswordChar = chkShow.Checked ? '\0' : '*';
            cy += 28 + 8;

            var divider = new Panel { Location = new Point(CPad, cy), Size = new Size(IW, 1), BackColor = ColorTranslator.FromHtml("#E2E8F0") };
            cy += 1 + 12;

            lblError = MakeLbl("", 9F, "#DC2626", FontStyle.Regular, CPad, cy, IW, 20, ContentAlignment.MiddleCenter); cy += 20 + 8;

            btnLogin = new Button
            {
                Text = "دخول",
                Font = new Font("Cairo", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(IW, 48),
                Location = new Point(CPad, cy),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnLogin.Paint += PaintBtn;
            btnLogin.Click += BtnLogin_Click;
            btnLogin.MouseEnter += (s, e) => { _btnHover = true; btnLogin.Invalidate(); };
            btnLogin.MouseLeave += (s, e) => { _btnHover = false; btnLogin.Invalidate(); };
            cy += 48 + 24;

            card.Size = new Size(CW, cy);
            card.Region = RoundRgn(new Rectangle(0, 0, CW, cy), 18);
            card.Controls.AddRange(new Control[] { lblTitle, lblSub, lblEmail, wrapEmail, lblPass, wrapPass, chkShow, divider, lblError, btnLogin });
            Controls.Add(card);

            ClientSize = new Size(FW, cardY + cy + 20);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(null, null); };
        }

        private Panel MakeInputWrap(int x, int y, int w, int h)
        {
            var wrap = new Panel { Size = new Size(w, h), Location = new Point(x, y), BackColor = ColorTranslator.FromHtml("#F8FAFC") };
            wrap.Region = RoundRgn(new Rectangle(0, 0, w, h), 10);
            return wrap;
        }

        private TextBox MakeTextBox(Panel wrap, bool isPass)
        {
            int h = wrap.Height;
            var txt = new TextBox
            {
                Font = new Font("Cairo", 11F),
                ForeColor = ColorTranslator.FromHtml("#0F172A"),
                BackColor = ColorTranslator.FromHtml("#F8FAFC"),
                BorderStyle = BorderStyle.None,
                RightToLeft = RightToLeft.No,
                TextAlign = HorizontalAlignment.Right,
                Size = new Size(wrap.Width - 24, 26),
                Location = new Point(12, (h - 26) / 2),
                TabStop = true,
                Cursor = Cursors.IBeam
            };
            if (isPass) txt.PasswordChar = '*';

            txt.GotFocus += (s, e) => { wrap.BackColor = Color.White; wrap.Invalidate(); };
            txt.LostFocus += (s, e) => { wrap.BackColor = ColorTranslator.FromHtml("#F8FAFC"); wrap.Invalidate(); };

            wrap.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, wrap.Width - 1, wrap.Height - 1);
                using (var br = new SolidBrush(wrap.BackColor))
                using (var path = Round(rc, 10)) pe.Graphics.FillPath(br, path);
                bool focused = txt.Focused;
                using (var pen = new Pen(focused ? ColorTranslator.FromHtml("#1565c0") : ColorTranslator.FromHtml("#CBD5E1"), focused ? 2f : 1.5f))
                using (var path = Round(rc, 10)) pe.Graphics.DrawPath(pen, path);
                if (focused)
                {
                    using (var br2 = new LinearGradientBrush(new Rectangle(2, rc.Height - 3, rc.Width - 4, 3), ColorTranslator.FromHtml("#1565c0"), ColorTranslator.FromHtml("#42A5F5"), LinearGradientMode.Horizontal))
                    using (var path2 = Round(new Rectangle(2, rc.Height - 3, rc.Width - 4, 3), 2))
                        pe.Graphics.FillPath(br2, path2);
                }
            };
            wrap.Controls.Add(txt);
            wrap.Click += (s, e) => txt.Focus();
            return txt;
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            var pnl = (Panel)sender; var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rc = new Rectangle(0, 0, pnl.Width, pnl.Height);
            using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml("#1a2f5e"), ColorTranslator.FromHtml("#1565c0"), LinearGradientMode.Horizontal))
                g.FillRectangle(br, rc);
            using (var dot = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                for (int x = 10; x < rc.Width; x += 20) for (int y = 8; y < rc.Height; y += 20) g.FillEllipse(dot, x, y, 2, 2);
            using (var cb = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
            { g.FillEllipse(cb, -50, -50, 210, 210); g.FillEllipse(cb, rc.Width - 130, -40, 200, 200); }
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var f = new Font("Cairo", 21F, FontStyle.Bold)) g.DrawString("شركة بصوص", f, Brushes.White, new RectangleF(0, -18, rc.Width, rc.Height), sf);
            using (var f = new Font("Cairo", 9.5F)) using (var tb = new SolidBrush(Color.FromArgb(185, 255, 255, 255)))
                g.DrawString("نظام إدارة التوزيع المتكامل", f, tb, new RectangleF(0, 22, rc.Width, rc.Height), sf);
            using (var br2 = new LinearGradientBrush(new Rectangle(0, rc.Height - 3, rc.Width, 3), ColorTranslator.FromHtml("#42A5F5"), ColorTranslator.FromHtml("#1a2f5e"), LinearGradientMode.Horizontal))
                g.FillRectangle(br2, 0, rc.Height - 3, rc.Width, 3);
        }

        private void PaintCard(Graphics g, int W, int H)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 4; i >= 1; i--)
                using (var sp = new SolidBrush(Color.FromArgb(i * 6, 0, 0, 0))) using (var shr = Round(new Rectangle(i, i, W - i * 2 - 1, H - i * 2 - 1), 20)) g.FillPath(sp, shr);
            var rc = new Rectangle(0, 0, W - 1, H - 1);
            using (var path = Round(rc, 18)) using (var br = new SolidBrush(Color.White)) g.FillPath(br, path);
            using (var pen = new Pen(ColorTranslator.FromHtml("#E2E8F0"), 1.2f)) using (var path = Round(rc, 18)) g.DrawPath(pen, path);
            using (var br2 = new LinearGradientBrush(new Rectangle(0, 0, W, 4), ColorTranslator.FromHtml("#1565c0"), ColorTranslator.FromHtml("#42A5F5"), LinearGradientMode.Horizontal))
            using (var topPath = new GraphicsPath())
            { topPath.AddArc(0, 0, 36, 36, 180, 90); topPath.AddArc(W - 36, 0, 36, 36, 270, 90); topPath.AddLine(W, 4, 0, 4); topPath.CloseFigure(); g.FillPath(br2, topPath); }
        }

        private void PaintBtn(object sender, PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rc = new Rectangle(0, 0, btnLogin.Width, btnLogin.Height);
            using (var br = new LinearGradientBrush(rc, ColorTranslator.FromHtml(_btnHover ? "#1565c0" : "#1a2f5e"), ColorTranslator.FromHtml(_btnHover ? "#1976D2" : "#1565c0"), LinearGradientMode.Horizontal))
            using (var path = Round(rc, 12)) g.FillPath(br, path);
            using (var gl = new LinearGradientBrush(new Rectangle(0, 0, btnLogin.Width, btnLogin.Height / 2), Color.FromArgb(30, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
            using (var glPath = Round(new Rectangle(2, 2, btnLogin.Width - 4, btnLogin.Height / 2), 10)) g.FillPath(gl, glPath);
            using (var f = new Font("Cairo", 12F, FontStyle.Bold))
                g.DrawString(btnLogin.Text, f, Brushes.White, rc, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }

        private async void BtnLogin_Click(object sender, EventArgs e)
        {
            lblError.Text = "";
            string email = txtEmail.Text?.Trim() ?? "";
            string password = txtPassword.Text ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            { lblError.Text = "يرجى إدخال البريد وكلمة المرور"; await Shake(btnLogin); return; }

            // ✅ الإيميل الجديد
            if (string.Equals(email, "mostafa.com", StringComparison.OrdinalIgnoreCase) && password == "1241994")
            {
                btnLogin.Enabled = false; btnLogin.Text = "جاري الدخول..."; btnLogin.Invalidate();
                await Task.Delay(450);
                Hide();
                using (var main = new MainLayoutForm()) main.ShowDialog();
                Close();
            }
            else
            { lblError.Text = "بيانات غير صحيحة، حاول مرة أخرى"; await Shake(btnLogin); txtPassword.Clear(); txtPassword.Focus(); }
        }

        private async Task Shake(Control ctrl)
        {
            int orig = ctrl.Left;
            foreach (int off in new[] { -7, 7, -5, 5, -3, 3, -1, 1, 0 })
            { ctrl.Left = orig + off; await Task.Delay(25); }
            ctrl.Left = orig;
        }

        private Label MakeLbl(string text, float size, string hex, FontStyle style, int x, int y, int w, int h, ContentAlignment align)
        {
            return new Label { Text = text, Font = new Font("Cairo", size, style), ForeColor = ColorTranslator.FromHtml(hex), BackColor = Color.Transparent, AutoSize = false, Size = new Size(w, h), Location = new Point(x, y), TextAlign = align, RightToLeft = RightToLeft.No };
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        private static Region RoundRgn(Rectangle r, int rad) { using (var p = Round(r, rad)) return new Region(p); }
    }
}