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
        private readonly IntPtr previewHandle = IntPtr.Zero;
        private readonly bool isPreview = false;

        // Animation variables
        private float u = 0f; // parameter for Möbius strip
        private readonly float speed = 0.05f; // Increased speed for faster movement
        private float angle = 0f; // rotation angle
        private float floatTime = 0f; // for floating motion
        private readonly float floatSpeed = 0.02f; // Faster rotation
        private readonly float floatSpeed2 = 0.01f;
        private readonly float floatAmp = 50f; // Increased amplitude
        private const float PI = (float)Math.PI;
        private float scale;
        private PointF centerPoint;
        private Thread animationThread;
        private bool isRunning = true;
        private float du = 0.08f; // Smaller segments for smoother fluid effect

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
            Bounds = bounds;
            isPreview = false;
        }

        public ScreensaverForm(IntPtr previewHandle, bool isPreview)
        {
            InitializeForm();
            this.previewHandle = previewHandle;
            this.isPreview = isPreview;

            // Set parent for preview window
            SetParent(Handle, previewHandle);

            // Make it a child window
            SetWindowLong(Handle, -16,
                GetWindowLong(Handle, -16) | 0x40000000);

            // Get preview window size
            Rectangle parentRect;
            GetClientRect(previewHandle, out parentRect);
            Size = parentRect.Size;
            Location = new Point(0, 0);
        }

        private void InitializeForm()
        {
            InitializeComponent();

            // Set form properties
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            TopMost = true;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.Manual;

            // Set up buffered graphics
            // context = BufferedGraphicsManager.Current;

            // Handle mouse and keyboard events to close screensaver
            MouseMove += ScreensaverForm_MouseMove;
            MouseClick += ScreensaverForm_MouseClick;
            KeyPress += ScreensaverForm_KeyPress;
        }

        private void ScreensaverForm_Load(object sender, EventArgs e)
        {
            // Calculate scale and center - increased to span more screen
            scale = Math.Min(ClientSize.Width, ClientSize.Height) / 2f;
            centerPoint = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);

            // Start animation thread
            animationThread = new Thread(AnimationLoop)
            {
                IsBackground = true
            };
            animationThread.Start();
        }

        private PointF GetPoint(float u, float v)
        {
            // Add fluid wave distortions
            float wave1 = (float)Math.Sin(u * 3 + floatTime * 2) * 0.2f;
            float wave2 = (float)Math.Cos(u * 2 + floatTime * 1.5f) * 0.15f;
            float fluidOffset = wave1 + wave2;

            float x = (1 + v / 2 * (float)Math.Cos(u / 2)) * (float)Math.Cos(u + fluidOffset);
            float y = (1 + v / 2 * (float)Math.Cos(u / 2)) * (float)Math.Sin(u + fluidOffset);
            float z = v / 2 * (float)Math.Sin(u / 2) + fluidOffset * 0.5f;

            // Rotate around y-axis with additional fluid rotation
            float fluidAngle = angle + (float)Math.Sin(floatTime) * 0.5f;
            float cosA = (float)Math.Cos(fluidAngle);
            float sinA = (float)Math.Sin(fluidAngle);
            float newX = x * cosA - z * sinA;
            float newZ = x * sinA + z * cosA;
            float newY = y;

            // Add random spanning across screen with enhanced horizontal spread
            float randomOffsetX = (float)Math.Sin(u * 3 + floatTime * 2) * scale * 0.6f + (float)Math.Cos(u * 7 + floatTime * 3.5f) * scale * 0.4f;
            float randomOffsetY = (float)Math.Cos(u * 4 + floatTime * 2.5f) * scale * 0.2f;

            // Simple 3D to 2D projection with enhanced perspective
            float xp = newX * scale + centerPoint.X + randomOffsetX;
            float yp = newY * scale + centerPoint.Y - newZ * scale * 0.5f + floatAmp * (float)Math.Sin(floatTime) + randomOffsetY;

            return new PointF(xp, yp);
        }

        private float GetWidth(float uu)
        {
            // More complex fluid width variation
            float baseWidth = 1f + 0.3f * (float)Math.Sin(uu + floatTime * 3);
            float fluidWave = 0.2f * (float)Math.Sin(uu * 2 + floatTime * 4);
            float randomPulse = 0.1f * (float)Math.Cos(uu * 7 + floatTime * 5);
            return baseWidth + fluidWave + randomPulse;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw space background with stars
            using (SolidBrush bgBrush = new(Color.Black))
            {
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);
            }

            // Draw stars with parallax movement and glow effect
            Random rand = new(42); // Fixed seed for consistent star positions
            
            // Three layers of stars for parallax effect
            for (int layer = 0; layer < 3; layer++)
            {
                float parallaxSpeed = 1.0f + layer * 0.8f; // Much faster movement for perspective
                float baseBrightness = 100 + layer * 30; // Farther layers are dimmer
                
                for (int i = 0; i < (layer == 0 ? 150 : layer == 1 ? 100 : 70); i++) // More stars
                {
                    // Generate base position
                    int baseX = rand.Next(ClientRectangle.Width);
                    int baseY = rand.Next(ClientRectangle.Height);
                    
                    // Apply faster parallax movement for perspective simulation
                    float offsetX = (float)Math.Sin(floatTime * parallaxSpeed + i * 0.1f) * 100 * (4 - layer);
                    float offsetY = (float)Math.Cos(floatTime * parallaxSpeed * 0.8f + i * 0.07f) * 80 * (4 - layer);
                    
                    int x = (int)(baseX + offsetX) % ClientRectangle.Width;
                    int y = (int)(baseY + offsetY) % ClientRectangle.Height;
                    
                    // Handle wraparound
                    if (x < 0) x += ClientRectangle.Width;
                    if (y < 0) y += ClientRectangle.Height;
                    
                    // Vary star brightness based on position and time for twinkling effect
                    float twinkle = (float)Math.Sin(floatTime * 2 + i * 0.1f + layer) * 0.5f + 0.5f;
                    int brightness = (int)(baseBrightness + twinkle * (255 - baseBrightness));
                    
                    // Draw glow effect (multiple concentric circles with decreasing opacity)
                    for (int glow = 3; glow >= 0; glow--)
                    {
                        int glowSize = (glow + 1) * 2;
                        int alpha = brightness / (glow + 1);
                        if (alpha > 255) alpha = 255;

                        using SolidBrush glowBrush = new(Color.FromArgb(alpha, brightness, brightness, brightness));
                        e.Graphics.FillEllipse(glowBrush, x - glowSize / 2, y - glowSize / 2, glowSize, glowSize);
                    }
                    
                    // Draw the core star
                    int coreSize = rand.Next(1, 3 + layer);
                    using SolidBrush starBrush = new(Color.FromArgb(brightness, brightness, brightness));
                    e.Graphics.FillEllipse(starBrush, x - coreSize / 2, y - coreSize / 2, coreSize, coreSize);
                }
            }

            // Add some distant nebula-like clouds
            using (GraphicsPath nebulaPath = new())
            {
                nebulaPath.AddEllipse(ClientRectangle.Width * 0.1f, ClientRectangle.Height * 0.2f, 
                                    ClientRectangle.Width * 0.3f, ClientRectangle.Height * 0.4f);
                nebulaPath.AddEllipse(ClientRectangle.Width * 0.6f, ClientRectangle.Height * 0.1f, 
                                    ClientRectangle.Width * 0.4f, ClientRectangle.Height * 0.3f);

                using PathGradientBrush nebulaBrush = new(nebulaPath);
                nebulaBrush.CenterColor = Color.FromArgb(30, 20, 40, 60);
                nebulaBrush.SurroundColors = [Color.FromArgb(10, 10, 20, 30)];
                nebulaBrush.CenterPoint = new PointF(ClientRectangle.Width * 0.3f, ClientRectangle.Height * 0.4f);
                e.Graphics.FillPath(nebulaBrush, nebulaPath);
            }

            // Draw the ball behind the strip for half the path
            if (Math.Sin(u / 2) <= 0)
            {
                PointF ballPos = GetPoint(u, 0);
                float ballRadius = 50;
                using GraphicsPath ballPath = new();
                ballPath.AddEllipse(ballPos.X - ballRadius, ballPos.Y - ballRadius, ballRadius * 2, ballRadius * 2);
                using PathGradientBrush ballBrush = new(ballPath);
                ballBrush.CenterColor = Color.Yellow;
                ballBrush.SurroundColors = [Color.FromArgb(100, 100, 0)];
                ballBrush.CenterPoint = new PointF(ballPos.X - ballRadius * 0.3f, ballPos.Y - ballRadius * 0.3f);
                e.Graphics.FillPath(ballBrush, ballPath);
            }

            // Draw the Möbius strip
            for (float uu = 0; uu < 2 * PI; uu += du)
            {
                float width = GetWidth(uu);
                PointF p1 = GetPoint(uu, -width);
                PointF p2 = GetPoint(uu, width);
                PointF p3 = GetPoint(uu + du, width);
                PointF p4 = GetPoint(uu + du, -width);

                PointF[] points = [p1, p2, p3, p4];

                float hue = (uu / (2 * PI)) * 360;
                Color baseColor = ColorFromHsv(hue, 1.0f, 0.8f);
                Color lightColor = ControlPaint.Light(baseColor, 0.3f);

                using (LinearGradientBrush brush = new(p1, p3, Color.FromArgb(128, baseColor), Color.FromArgb(128, lightColor)))
                {
                    e.Graphics.FillPolygon(brush, points);
                }

                using Pen pen = new(Color.FromArgb(150, Color.White), 1);
                e.Graphics.DrawPolygon(pen, points);
            }

            // Draw the ball on top of the strip for half the path
            if (Math.Sin(u / 2) > 0)
            {
                PointF ballPos = GetPoint(u, 0);
                float ballRadius = 50;
                using GraphicsPath ballPath = new();
                ballPath.AddEllipse(ballPos.X - ballRadius, ballPos.Y - ballRadius, ballRadius * 2, ballRadius * 2);
                using PathGradientBrush ballBrush = new(ballPath);
                ballBrush.CenterColor = Color.Yellow;
                ballBrush.SurroundColors = [Color.FromArgb(100, 100, 0)];
                ballBrush.CenterPoint = new PointF(ballPos.X - ballRadius * 0.3f, ballPos.Y - ballRadius * 0.3f);
                e.Graphics.FillPath(ballBrush, ballPath);
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

                // Update segment size for fluid effect, but clamp to prevent too many iterations
                du = Math.Max(0.08f, 0.04f + 0.03f * (float)Math.Sin(floatTime * 0.5f) + 0.02f * (float)Math.Cos(floatTime * 0.3f));

                // Redraw on UI thread
                Invoke(new Action(Invalidate));

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
        private readonly System.ComponentModel.IContainer? components = null;
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
            SuspendLayout();
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new Size(800, 600);
            Name = "ScreensaverForm";
            Text = "Möbius Strip Screensaver";
            Load += new EventHandler(ScreensaverForm_Load);
            ResumeLayout(false);
        }
        #endregion
    }
}