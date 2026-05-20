using System;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace DistributionSystem.UI.Forms
{
    public static class AnimationHelper
    {
        public static async void FadeIn(Control control, int duration = 300)
        {
            if (control == null) return;
            try
            {
                control.SuspendLayout();
                control.Visible = true;
                float step = 50f / duration; // 50ms per tick
                float opacity = 0f;

                // If control is a form, use Opacity; otherwise apply simple fade by enabling gradually.
                var form = control as Form;
                if (form != null)
                {
                    form.Opacity = 0;
                    int ticks = duration / 50;
                    for (int i = 0; i < ticks; i++)
                    {
                        await Task.Delay(50);
                        form.Opacity = Math.Min(1.0, form.Opacity + (1.0 / ticks));
                    }
                    form.Opacity = 1;
                }
                else
                {
                    // For user controls/panels: simple approach: refresh a few times to give impression of animation
                    int ticks = duration / 50;
                    for (int i = 0; i < ticks; i++)
                    {
                        await Task.Delay(50);
                        control.Refresh();
                    }
                }
                control.ResumeLayout();
            }
            catch
            {
                // swallow animation exceptions
            }
        }
    }
}
