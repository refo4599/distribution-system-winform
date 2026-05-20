using DistributionSystem.UI.Controls;
using System.Drawing;
using System.Windows.Forms;

namespace DistributionSystem.UI.Forms
{
    partial class DashboardForm
    {
        private System.ComponentModel.IContainer components = null;

        // Cards
        private DashboardCard cardProducts;
        private DashboardCard cardCustomers;
        private DashboardCard cardSuppliers;   // ? ÈÞì ÇáÎÒäÉ
        private DashboardCard cardSales;
        private DashboardCard cardPurchases;
        private DashboardCard cardLowStock;

        // Layout
        private TableLayoutPanel tableLayoutPanel;
        private Panel pnlHeader;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = ColorTranslator.FromHtml("#EEF0F5");
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.Font = new System.Drawing.Font("Cairo", 9.5F);
            this.Load += DashboardForm_Load;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Header banner
            pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.Transparent };
            pnlHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, pnlHeader.Width, pnlHeader.Height);

                using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(rc,
                    ColorTranslator.FromHtml("#1a2f5e"),
                    ColorTranslator.FromHtml("#1565c0"),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
                {
                    var path = RoundPath(rc, 14);
                    g.FillPath(br, path);
                    path.Dispose();
                }

                using (var dot = new SolidBrush(System.Drawing.Color.FromArgb(20, 255, 255, 255)))
                    for (int x = 10; x < rc.Width; x += 20)
                        for (int y = 8; y < rc.Height; y += 20)
                            g.FillEllipse(dot, x, y, 2, 2);

                using (var cb = new SolidBrush(System.Drawing.Color.FromArgb(14, 255, 255, 255)))
                {
                    g.FillEllipse(cb, rc.Width - 120, -40, 200, 200);
                    g.FillEllipse(cb, rc.Width - 30, 20, 140, 140);
                }

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var f = new System.Drawing.Font("Cairo", 20F, FontStyle.Bold))
                    g.DrawString("áæÍÉ ÇáÊÍßã", f, Brushes.White,
                        new RectangleF(0, -10, rc.Width, rc.Height), sf);

                using (var f = new System.Drawing.Font("Cairo", 10F))
                using (var tb = new SolidBrush(System.Drawing.Color.FromArgb(190, 255, 255, 255)))
                    g.DrawString("ãÑÍÈÇð — Åáíß ãáÎÕ ÃÏÇÁ ÇáäÙÇã", f, tb,
                        new RectangleF(0, 16, rc.Width, rc.Height), sf);
            };

            // Cards grid
            tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = System.Drawing.Color.Transparent,
                Padding = new Padding(16, 12, 16, 16)
            };
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            cardProducts = MakeCard("ÇáãäÊÌÇÊ", "0", "ÅÌãÇáí ÇáãäÊÌÇÊ", ColorTranslator.FromHtml("#2563EB"), "Products");
            cardCustomers = MakeCard("ÇáÚãáÇÁ", "0", "ÅÌãÇáí ÇáÚãáÇÁ", ColorTranslator.FromHtml("#059669"), "Customers");

            // ? ÇáÎÒäÉ ÈÏá ÇáãæÑÏíä
            cardSuppliers = MakeCard("ÇáÎÒäÉ", "0 Ì", "ÇáÑÕíÏ Çáßáí ááÎÒäÉ", ColorTranslator.FromHtml("#1565c0"), "Suppliers");

            cardSales = MakeCard("ÇáÝæÇÊíÑ", "0", "ÅÌãÇáí ÝæÇÊíÑ ÇáãÈíÚÇÊ", ColorTranslator.FromHtml("#7C3AED"), "Sales");
            cardPurchases = MakeCard("ÇáæÇÑÏ", "0", "ÅÌãÇáí ÃæÇãÑ ÇáÔÑÇÁ", ColorTranslator.FromHtml("#0891B2"), "Purchases");
            cardLowStock = MakeCard("ÊäÈíåÇÊ ÇáãÎÒæä", "0", "ãäÊÌÇÊ ãäÎÝÖÉ ÇáãÎÒæä", ColorTranslator.FromHtml("#DC2626"), "Warehouse");

            tableLayoutPanel.Controls.Add(cardProducts, 0, 0);
            tableLayoutPanel.Controls.Add(cardCustomers, 1, 0);
            tableLayoutPanel.Controls.Add(cardSuppliers, 2, 0);  // ÇáÎÒäÉ — äÝÓ ÇáãæÞÚ
            tableLayoutPanel.Controls.Add(cardSales, 0, 1);
            tableLayoutPanel.Controls.Add(cardPurchases, 1, 1);
            tableLayoutPanel.Controls.Add(cardLowStock, 2, 1);

            root.Controls.Add(pnlHeader, 0, 0);
            root.Controls.Add(tableLayoutPanel, 0, 1);
            this.Controls.Add(root);
            root.BringToFront();

            this.ResumeLayout(false);
        }

        private DashboardCard MakeCard(string title, string value, string subtitle,
            Color color, string tag)
        {
            var card = new DashboardCard
            {
                CardTitle = title,
                CardValue = value,
                CardSubtitle = subtitle,
                CardColor = color,
                Tag = tag,
                Dock = DockStyle.Fill,
                Margin = new Padding(8)
            };
            card.Click += Card_Click;
            return card;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = System.Math.Min(radius * 2, System.Math.Min(r.Width, r.Height));
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}