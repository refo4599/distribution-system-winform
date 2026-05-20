using System;
using System.Windows.Forms;

namespace DistributionSystem.UI.Forms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnProducts_Click(object sender, EventArgs e)
        {
            using (var form = new ProductForm())
            {
                form.ShowDialog(this);
            }
        }

        private void btnInbound_Click(object sender, EventArgs e)
        {
            using (var form = new InboundForm())
            {
                form.ShowDialog(this);
            }
        }

        private void btnWarehouseReport_Click(object sender, EventArgs e)
        {
            using (var form = new WarehouseReportForm())
            {
                form.ShowDialog(this);
            }
        }
    }
}
