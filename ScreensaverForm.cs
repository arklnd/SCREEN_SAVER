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
        private float u = 0f; // parameter for Möbius strip
        private float speed = 0.02f;
        private float angle = 0f; // rotation angle
        private float floatTime = 0f; // for floating motion
        private float floatSpeed = 0.01f;
        private float floatSpeed2 = 0.005f;
        private float floatAmp = 30f;
        private const float PI = (float)Math.PI;
        private float scale;
        private PointF centerPoint;
        private Thread animationThread;
        private bool isRunning = true;
        private float du = 0.1f;

        // For smooth animation
        // private BufferedGraphicsContext context;
        // private BufferedGraphics bufferedGraphics;

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
            // context = BufferedGraphicsManager.Current;

            // Handle mouse and keyboard events to close screensaver
            this.MouseMove += ScreensaverForm_MouseMove;
            this.MouseClick += ScreensaverForm_MouseClick;
            this.KeyPress += ScreensaverForm_KeyPress;
        }

        private void ScreensaverForm_Load(object sender, EventArgs e)
        {
            // Calculate scale and center
            scale = Math.Min(this.ClientSize.Width, this.ClientSize.Height) / 4f;
            centerPoint = new PointF(this.ClientSize.Width / 2f, this.ClientSize.Height / 2f);

            // Start animation thread
            animationThread = new Thread(AnimationLoop);
            animationThread.IsBackground = true;
            animationThread.Start();
        }

        private PointF GetPoint(float u, float v)
        {
            float x = (1 + v / 2 * (float)Math.Cos(u / 2)) * (float)Math.Cos(u);
            float y = (1 + v / 2 * (float)Math.Cos(u / 2)) * (float)Math.Sin(u);
            float z = v / 2 * (float)Math.Sin(u / 2);

            // Rotate around y-axis
            float cosA = (float)Math.Cos(angle);
            float sinA = (float)Math.Sin(angle);
            float newX = x * cosA - z * sinA;
            float newZ = x * sinA + z * cosA;
            float newY = y;

            // Simple 3D to 2D projection
            float xp = newX * scale + centerPoint.X;
            float yp = newY * scale + centerPoint.Y - newZ * scale * 0.3f + floatAmp * (float)Math.Sin(floatTime);

            return new PointF(xp, yp);
        }

        private float GetWidth(float uu)
        {
            return 1f + 0.15625f * (float)Math.Sin(uu + floatTime * 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw space background
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(ClientRectangle, Color.Black, Color.DarkBlue, LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            // Draw the ball behind the strip for half the path
            if (Math.Sin(u / 2) <= 0)
            {
                PointF ballPos = GetPoint(u, 0);
                float ballRadius = 50;
                using (GraphicsPath ballPath = new GraphicsPath())
                {
                    ballPath.AddEllipse(ballPos.X - ballRadius, ballPos.Y - ballRadius, ballRadius * 2, ballRadius * 2);
                    using (PathGradientBrush ballBrush = new PathGradientBrush(ballPath))
                    {
                        ballBrush.CenterColor = Color.Yellow;
                        ballBrush.SurroundColors = new Color[] { Color.FromArgb(100, 100, 0) };
                        ballBrush.CenterPoint = new PointF(ballPos.X - ballRadius * 0.3f, ballPos.Y - ballRadius * 0.3f);
                        e.Graphics.FillPath(ballBrush, ballPath);
                    }
                }
            }

            // Draw the Möbius strip
            for (float uu = 0; uu < 2 * PI; uu += du)
            {
                float width = GetWidth(uu);
                PointF p1 = GetPoint(uu, -width);
                PointF p2 = GetPoint(uu, width);
                PointF p3 = GetPoint(uu + du, width);
                PointF p4 = GetPoint(uu + du, -width);

                PointF[] points = { p1, p2, p3, p4 };

                float hue = (uu / (2 * PI)) * 360;
                Color baseColor = ColorFromHsv(hue, 1.0f, 0.8f);
                Color lightColor = ControlPaint.Light(baseColor, 0.3f);

                using (LinearGradientBrush brush = new LinearGradientBrush(p1, p3, Color.FromArgb(128, baseColor), Color.FromArgb(128, lightColor)))
                {
                    e.Graphics.FillPolygon(brush, points);
                }

                using (Pen pen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawPolygon(pen, points);
                }
            }

            // Draw the ball on top of the strip for half the path
            if (Math.Sin(u / 2) > 0)
            {
                PointF ballPos = GetPoint(u, 0);
                float ballRadius = 50;
                using (GraphicsPath ballPath = new GraphicsPath())
                {
                    ballPath.AddEllipse(ballPos.X - ballRadius, ballPos.Y - ballRadius, ballRadius * 2, ballRadius * 2);
                    using (PathGradientBrush ballBrush = new PathGradientBrush(ballPath))
                    {
                        ballBrush.CenterColor = Color.Yellow;
                        ballBrush.SurroundColors = new Color[] { Color.FromArgb(100, 100, 0) };
                        ballBrush.CenterPoint = new PointF(ballPos.X - ballRadius * 0.3f, ballPos.Y - ballRadius * 0.3f);
                        e.Graphics.FillPath(ballBrush, ballPath);
                    }
                }
            }
        }

        private Color ColorFromHsv(float hue, float saturation, float value)
        {
            int hi = (int)(hue / 60) % 6;
            float f = hue / 60 - hi;
            float p = value * (1 - saturation);
            float q = value * (1 - f * saturation);
            float t = value * (1 - (1 - f) * saturation);
            int r, g, b;
            switch (hi)
            {
                case 0: r = (int)(value * 255); g = (int)(t * 255); b = (int)(p * 255); break;
                case 1: r = (int)(q * 255); g = (int)(value * 255); b = (int)(p * 255); break;
                case 2: r = (int)(p * 255); g = (int)(value * 255); b = (int)(t * 255); break;
                case 3: r = (int)(p * 255); g = (int)(q * 255); b = (int)(value * 255); break;
                case 4: r = (int)(t * 255); g = (int)(p * 255); b = (int)(value * 255); break;
                case 5: r = (int)(value * 255); g = (int)(p * 255); b = (int)(q * 255); break;
                default: r = g = b = 0; break;
            }
            return Color.FromArgb(r, g, b);
        }

        private void AnimationLoop()
        {
            while (isRunning)
            {
                // Update u
                u += speed;

                // Reset when full loop
                if (u > 2 * PI)
                {
                    u -= 2 * PI;
                }

                // Update rotation and floating
                angle += floatSpeed;
                floatTime += floatSpeed2;

                // Update segment size for varying number of plates
                du = 0.05f + 0.04f * (float)Math.Sin(floatTime * 0.3f);

                // Redraw on UI thread
                this.Invoke(new Action(Invalidate));

                // Control animation speed (60 FPS)
                Thread.Sleep(16);
            }
        }

        // protected override void OnPaintBackground(PaintEventArgs e)
        // {
        //     // Don't call base to prevent flickering
        //     e.Graphics.Clear(Color.Black);
        // }

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
            this.Text = "Möbius Strip Screensaver";
            this.Load += new System.EventHandler(this.ScreensaverForm_Load);
            this.ResumeLayout(false);
        }
        #endregion
    }
}