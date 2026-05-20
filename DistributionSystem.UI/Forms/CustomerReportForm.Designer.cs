using System.Drawing;
using System.Windows.Forms;

namespace DistributionSystem.UI.Forms
{
    partial class CustomerReportForm
    {
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // CustomerReportForm
            // 
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.ClientSize = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Cairo", 10F);
            this.ResumeLayout(false);
        }
    }
}
