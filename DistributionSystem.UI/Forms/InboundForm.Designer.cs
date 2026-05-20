namespace DistributionSystem.UI.Forms
{
    partial class InboundForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // InboundForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 800);
            this.Name = "InboundForm";
            this.Text = "«·Ê«—œ";
            this.Load += new System.EventHandler(this.InboundForm_Load);
            this.ResumeLayout(false);
        }
    }
}