using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

namespace SCREEN_SAVER
{
    public partial class ScreensaverForm : Form
    {
        // For preview window handle
        private IntPtr previewHandle = IntPtr.Zero;
        private bool isPreview = false;

        // Animation variables
        private float currentRadius = 1f;
        private float maxRadius;
        private float expansionSpeed = 1.5f;
        private Color circleColor = Color.FromArgb(0, 150, 255); // Blue color
        private PointF centerPoint;
        private Thread animationThread;
        private bool isRunning = true;

        // For smooth animation
        private BufferedGraphicsContext context;
        private BufferedGraphics bufferedGraphics;

        // Import user32.dll for preview window
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        public ScreensaverForm(Rectangle bounds)
        {
            InitializeForm();
            this.Bounds = bounds;
            this.isPreview = false;
        }

        public ScreensaverForm(IntPtr previewHandle, bool isPreview)
        {
            InitializeForm();
            this.previewHandle = previewHandle;
            this.isPreview = isPreview;

            // Set parent for preview window
            SetParent(this.Handle, previewHandle);

            // Make it a child window
            SetWindowLong(this.Handle, -16,
                GetWindowLong(this.Handle, -16) | 0x40000000);

            // Get preview window size
            Rectangle parentRect;
            GetClientRect(previewHandle, out parentRect);
            this.Size = parentRect.Size;
            this.Location = new Point(0, 0);
        }

        private void InitializeForm()
        {
            InitializeComponent();

            // Set form properties
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.TopMost = true;
            this.DoubleBuffered = true;

            // Set up buffered graphics
            context = BufferedGraphicsManager.Current;

            // Handle mouse and keyboard events to close screensaver
            this.MouseMove += ScreensaverForm_MouseMove;
            this.MouseClick += ScreensaverForm_MouseClick;
            this.KeyPress += ScreensaverForm_KeyPress;
        }

        private void ScreensaverForm_Load(object sender, EventArgs e)
        {
            // Calculate center and maximum radius
            centerPoint = new PointF(this.ClientSize.Width / 2f, this.ClientSize.Height / 2f);
            maxRadius = (float)Math.Sqrt(
                Math.Pow(this.ClientSize.Width / 2f, 2) +
                Math.Pow(this.ClientSize.Height / 2f, 2));

            // Start animation thread
            animationThread = new Thread(AnimationLoop);
            animationThread.IsBackground = true;
            animationThread.Start();
        }

        private void AnimationLoop()
        {
            while (isRunning)
            {
                // Update radius
                currentRadius += expansionSpeed;

                // Reset when circle is fully expanded
                if (currentRadius > maxRadius)
                {
                    currentRadius = 1f;
                }

                // Redraw on UI thread
                this.Invoke(new Action(Invalidate));

                // Control animation speed (60 FPS)
                Thread.Sleep(16);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Create a gradient brush for smooth circle
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(
                    centerPoint.X - currentRadius,
                    centerPoint.Y - currentRadius,
                    currentRadius * 2,
                    currentRadius * 2);

                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(200, circleColor);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, circleColor) };
                    brush.CenterPoint = centerPoint;

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(brush,
                        centerPoint.X - currentRadius,
                        centerPoint.Y - currentRadius,
                        currentRadius * 2,
                        currentRadius * 2);
                }
            }

            // Draw center point
            using (SolidBrush centerBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(centerBrush,
                    centerPoint.X - 2,
                    centerPoint.Y - 2,
                    4, 4);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Don't call base to prevent flickering
            e.Graphics.Clear(Color.Black);
        }

        // Event handlers to close screensaver
        private Point lastMousePos;
        private void ScreensaverForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPreview)
            {
                if (!lastMousePos.IsEmpty)
                {
                    if (Math.Abs(e.X - lastMousePos.X) > 5 || Math.Abs(e.Y - lastMousePos.Y) > 5)
                    {
                        Application.Exit();
                    }
                }
                lastMousePos = e.Location;
            }
        }

        private void ScreensaverForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isPreview) Application.Exit();
        }

        private void ScreensaverForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!isPreview) Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isRunning = false;
            if (animationThread != null && animationThread.IsAlive)
            {
                animationThread.Join(1000);
            }
            base.OnFormClosing(e);
        }

        #region Windows Form Designer generated code
        private System.ComponentModel.IContainer components = null;
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
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "ScreensaverForm";
            this.Text = "Expanding Circle Screensaver";
            this.Load += new System.EventHandler(this.ScreensaverForm_Load);
            this.ResumeLayout(false);
        }
        #endregion
    }
}