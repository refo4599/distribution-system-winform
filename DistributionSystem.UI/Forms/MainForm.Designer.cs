namespace DistributionSystem.UI.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnProducts;
        private System.Windows.Forms.Button btnCustomers;
        private System.Windows.Forms.Button btnInbound;
        private System.Windows.Forms.Button btnWarehouseReport;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.btnProducts = new System.Windows.Forms.Button();
            this.btnCustomers = new System.Windows.Forms.Button();
            this.btnInbound = new System.Windows.Forms.Button();
            this.btnWarehouseReport = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnProducts
            // 
            this.btnProducts.Location = new System.Drawing.Point(12, 12);
            this.btnProducts.Name = "btnProducts";
            this.btnProducts.Size = new System.Drawing.Size(120, 30);
            this.btnProducts.TabIndex = 0;
            this.btnProducts.Text = "«·„‰ Ã« ";
            this.btnProducts.UseVisualStyleBackColor = true;
            this.btnProducts.Click += new System.EventHandler(this.btnProducts_Click);
            // 
            // btnCustomers
            // 
            this.btnCustomers.Location = new System.Drawing.Point(12, 48);
            this.btnCustomers.Name = "btnCustomers";
            this.btnCustomers.Size = new System.Drawing.Size(120, 30);
            this.btnCustomers.TabIndex = 1;
            this.btnCustomers.Text = "«·⁄„·«¡";
            this.btnCustomers.UseVisualStyleBackColor = true;
            this.btnCustomers.Click += new System.EventHandler((s,e)=> { using(var f=new CustomerForm()){ f.ShowDialog(this); } });
            // 
            // btnInbound
            // 
            this.btnInbound.Location = new System.Drawing.Point(12, 84);
            this.btnInbound.Name = "btnInbound";
            this.btnInbound.Size = new System.Drawing.Size(120, 30);
            this.btnInbound.TabIndex = 2;
            this.btnInbound.Text = "«·Ê«—œ";
            this.btnInbound.UseVisualStyleBackColor = true;
            this.btnInbound.Click += new System.EventHandler(this.btnInbound_Click);
            // 
            // btnWarehouseReport
            // 
            this.btnWarehouseReport.Location = new System.Drawing.Point(12, 120);
            this.btnWarehouseReport.Name = "btnWarehouseReport";
            this.btnWarehouseReport.Size = new System.Drawing.Size(120, 30);
            this.btnWarehouseReport.TabIndex = 3;
            this.btnWarehouseReport.Text = " ﬁ—Ì— «·„Œ“‰";
            this.btnWarehouseReport.UseVisualStyleBackColor = true;
            this.btnWarehouseReport.Click += new System.EventHandler(this.btnWarehouseReport_Click);
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnWarehouseReport);
            this.Controls.Add(this.btnInbound);
            this.Controls.Add(this.btnCustomers);
            this.Controls.Add(this.btnProducts);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "‰Ÿ«„ «· Ê“Ì⁄ - «·—∆Ì”Ì…";
            this.ResumeLayout(false);
        }
    }
}
