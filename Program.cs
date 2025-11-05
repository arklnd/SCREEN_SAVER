using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SCREEN_SAVER
{
    static class Program
    {
        // Import for preview window handling
        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Parse command line arguments
            if (args.Length > 0)
            {
                string arg = args[0].ToLower().Trim();

                if (arg.StartsWith("/c")) // Configuration dialog
                {
                    MessageBox.Show("No settings available.","Screensaver Config");
                    return;
                }
                else if (arg.StartsWith("/p")) // Preview mode
                {
                    if (args.Length > 1 && int.TryParse(args[1], out int previewWnd))
                    {
                        ScreenSaverPreview preview = new ScreenSaverPreview(new IntPtr(previewWnd));
                        Application.Run(preview);
                    }
                    else
                    {
                        MessageBox.Show("Invalid preview window handle.");
                    }
                }
                else if (arg.StartsWith("/s")) // Fullscreen screensaver
                {
                    ScreenSaverForm screensaver = new ScreenSaverForm();
                    Application.Run(screensaver);
                }
                else
                {
                    // Unknown argument, run screensaver by default
                    Application.Run(new ScreenSaverForm());
                }
            }
            else
            {
                // No arguments, run screensaver
                Application.Run(new ScreenSaverForm());
            }
        }
    }

    public class ScreenSaverForm : Form
    {
        private System.Windows.Forms.Timer timer;
        private int x = 0, y = 0;
        private int dx = 4, dy = 4;

        public ScreenSaverForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.TopMost = true;

            this.MouseMove += (s, e) => Application.Exit();
            this.KeyDown += (s, e) => Application.Exit();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 50;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            x += dx;
            y += dy;

            if (x < 0 || x > this.Width - 100) dx = -dx;
            if (y < 0 || y > this.Height - 30) dy = -dy;

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.DrawString("My Screensaver", new Font("Arial", 24), Brushes.White, x, y);
        }
    }

    public class ScreenSaverPreview : Form
    {
        public ScreenSaverPreview(IntPtr previewWnd)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Width = 150;
            this.Height = 120;

            // Set the parent window for preview rendering
            Program.SetParent(this.Handle, previewWnd);
        }
    }
}