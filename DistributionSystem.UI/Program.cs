using System;
using System.Windows.Forms;
using DistributionSystem.UI.Forms;

namespace DistributionSystem.UI
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm());
        }
    }
}