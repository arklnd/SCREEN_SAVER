using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace SCREEN_SAVER
{
    public partial class AnalogClockForm : Form
    {
        // For preview window handle
        private readonly IntPtr previewHandle = IntPtr.Zero;
        private readonly bool isPreview = false;

        private System.Windows.Forms.Timer timer;
        private PointF centerPoint;
        private float clockRadius;

        private class Projectile
        {
            public DateTime startTime;
            public double angle;
            public bool hasExploded;
        }

        private class Blast
        {
            public PointF position;
            public DateTime startTime;
            public Color color;
        }

        private List<Projectile> projectiles;
        private List<Blast> blasts;
        private Random random = new Random();
        private int lastSecond = -1;

        // Import user32.dll for preview window
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        public AnalogClockForm()
        {
            InitializeComponent();
            InitializeClock();
        }

        public AnalogClockForm(Rectangle bounds)
        {
            InitializeComponent();
            Bounds = bounds;
            isPreview = false;
            InitializeClock();
        }

        public AnalogClockForm(IntPtr previewHandle, bool isPreview)
        {
            InitializeComponent();
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

            InitializeClock();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            ClientSize = new Size(800, 600);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            Name = "AnalogClockForm";
            Text = "Analog Clock";

            // Only set to maximized if not preview mode
            if (!isPreview)
            {
                WindowState = FormWindowState.Maximized;
                TopMost = true;
            }

            StartPosition = FormStartPosition.Manual;

            // Handle mouse and keyboard events to close screensaver (only if not preview)
            if (!isPreview)
            {
                MouseMove += AnalogClockForm_MouseMove;
                MouseClick += AnalogClockForm_MouseClick;
                KeyPress += AnalogClockForm_KeyPress;
            }

            ResumeLayout(false);
        }

        private void InitializeClock()
        {
            // Set up timer to update clock every 20ms for smooth animation
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 20; // 20ms for smoother updates
            timer.Tick += Timer_Tick;
            timer.Start();

            projectiles = new List<Projectile>();
            blasts = new List<Blast>();

            // Calculate clock center and radius
            UpdateClockDimensions();
        }

        private void UpdateClockDimensions()
        {
            centerPoint = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
            clockRadius = Math.Min(ClientSize.Width, ClientSize.Height) / 3f;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (now.Second != lastSecond)
            {
                projectiles.Add(new Projectile { startTime = now, angle = now.Second * 6 * Math.PI / 180 });
                lastSecond = now.Second;
            }

            // Check for impacts
            foreach (var p in projectiles)
            {
                if (!p.hasExploded && (now - p.startTime).TotalSeconds >= 1.0)
                {
                    p.hasExploded = true;
                    float x = centerPoint.X + (float)Math.Sin(p.angle) * clockRadius;
                    float y = centerPoint.Y - (float)Math.Cos(p.angle) * clockRadius;
                    
                    // Random bright color for balloon
                    Color blastColor = Color.FromArgb(
                        random.Next(150, 255),
                        random.Next(150, 255),
                        random.Next(150, 255));
                        
                    blasts.Add(new Blast 
                    { 
                        position = new PointF(x, y), 
                        startTime = now,
                        color = blastColor
                    });
                }
            }

            // Remove old blasts
            blasts.RemoveAll(b => (now - b.startTime).TotalSeconds > 0.5);

            Invalidate(); // Redraw the clock
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // Draw clock face
            DrawClockFace(e.Graphics);

            // Draw hour markers
            DrawHourMarkers(e.Graphics);

            // Draw numbers
            DrawNumbers(e.Graphics);

            // Draw clock hands
            DrawClockHands(e.Graphics);

            // Draw date and time
            DrawDateAndTime(e.Graphics);
        }

        private void DrawClockFace(Graphics g)
        {
            using Pen pen = new Pen(Color.White, 3);
            g.DrawEllipse(pen, centerPoint.X - clockRadius, centerPoint.Y - clockRadius,
                         clockRadius * 2, clockRadius * 2);

            // Draw center dot
            using SolidBrush brush = new SolidBrush(Color.White);
            g.FillEllipse(brush, centerPoint.X - 5, centerPoint.Y - 5, 10, 10);
        }

        private void DrawHourMarkers(Graphics g)
        {
            using Pen hourPen = new Pen(Color.White, 2);
            using Pen minutePen = new Pen(Color.Gray, 1);

            for (int i = 0; i < 60; i++)
            {
                double angle = i * 6 * Math.PI / 180; // 6 degrees per minute
                float markerLength = (i % 5 == 0) ? 20 : 10; // Longer for hours
                Pen pen = (i % 5 == 0) ? hourPen : minutePen;

                PointF innerPoint = new PointF(
                    centerPoint.X + (float)Math.Sin(angle) * (clockRadius - markerLength),
                    centerPoint.Y - (float)Math.Cos(angle) * (clockRadius - markerLength)
                );

                PointF outerPoint = new PointF(
                    centerPoint.X + (float)Math.Sin(angle) * clockRadius,
                    centerPoint.Y - (float)Math.Cos(angle) * clockRadius
                );

                g.DrawLine(pen, innerPoint, outerPoint);
            }
        }

        private void DrawNumbers(Graphics g)
        {
            using Font font = new Font("Arial", clockRadius / 8, FontStyle.Bold);
            using SolidBrush brush = new SolidBrush(Color.White);

            for (int hour = 1; hour <= 12; hour++)
            {
                double angle = ((hour % 12) * 30) * Math.PI / 180; // 30 degrees per hour, starting at 12
                float numberRadius = clockRadius - 100;

                PointF numberPoint = new PointF(
                    centerPoint.X + (float)Math.Sin(angle) * numberRadius,
                    centerPoint.Y - (float)Math.Cos(angle) * numberRadius
                );

                string numberText = hour.ToString();
                SizeF textSize = g.MeasureString(numberText, font);
                numberPoint.X -= textSize.Width / 2;
                numberPoint.Y -= textSize.Height / 2;

                g.DrawString(numberText, font, brush, numberPoint);
            }
        }

        private void DrawClockHands(Graphics g)
        {
            DateTime now = DateTime.Now;

            // Hour hand shadow
            double hourAngle = ((now.Hour % 12) * 30 + now.Minute * 0.5) * Math.PI / 180;
            DrawHand(g, hourAngle, clockRadius * 0.5f, 12, Color.Gray, 2, 2, 2);

            // Hour hand
            DrawHand(g, hourAngle, clockRadius * 0.5f, 10, Color.White, 0, 0, 2);

            // Minute hand shadow
            double minuteAngle = (now.Minute * 6 + now.Second * 0.1) * Math.PI / 180;
            DrawHand(g, minuteAngle, clockRadius * 0.7f, 6, Color.Gray, 2, 2);

            // Minute hand
            DrawHand(g, minuteAngle, clockRadius * 0.7f, 4, Color.White);

            // Draw projectiles instead of second hand
            DrawProjectiles(g);
            DrawBlasts(g);
        }

        private void DrawBlasts(Graphics g)
        {
            DateTime now = DateTime.Now;
            // Create a copy to avoid modification exception if accessed from other threads (though Timer is UI thread)
            // But here we are just reading.
            for (int i = 0; i < blasts.Count; i++)
            {
                var blast = blasts[i];
                double t = (now - blast.startTime).TotalSeconds;
                if (t > 0.5) continue;

                // Balloon expands and fades
                float progress = (float)(t / 0.5);
                float size = 10f + 100f * progress; // Expands from 10 to 110
                int alpha = (int)(255 * (1 - progress));
                if (alpha < 0) alpha = 0;
                if (alpha > 255) alpha = 255;
                
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, blast.color)))
                {
                    g.FillEllipse(brush, 
                        blast.position.X - size / 2, 
                        blast.position.Y - size / 2, 
                        size, size);
                }
            }
        }

        private void DrawProjectiles(Graphics g)
        {
            DateTime now = DateTime.Now;
            int trailSteps = 20;
            double trailDuration = 0.3;

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                double t = (now - p.startTime).TotalSeconds;
                
                if (t > 1 + trailDuration)
                {
                    projectiles.RemoveAt(i);
                    continue;
                }

                for (int step = 0; step < trailSteps; step++)
                {
                    double trailT = t - (step * (trailDuration / trailSteps));
                    
                    if (trailT < 0) continue;
                    if (trailT > 1) continue;

                    float distance = (float)(clockRadius * trailT);
                    PointF pos = new PointF(
                        centerPoint.X + (float)Math.Sin(p.angle) * distance,
                        centerPoint.Y - (float)Math.Cos(p.angle) * distance
                    );

                    int alpha = (int)(255 * (1.0 - (double)step / trailSteps));
                    float size = 6f * (float)(1.0 - (double)step / trailSteps);
                    if (size < 1) size = 1;

                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, Color.Red)))
                    {
                        g.FillEllipse(brush, pos.X - size / 2, pos.Y - size / 2, size, size);
                    }
                }
            }
        }

        private void DrawDateAndTime(Graphics g)
        {
            DateTime now = DateTime.Now;
            
            var parts = new (string text, Color color)[]
            {
                (now.ToString("dddd, "), Color.DeepSkyBlue),
                (now.ToString("MMMM dd, yyyy"), Color.LightGreen),
                ("        ", Color.White),
                (now.ToString("hh:mm:ss "), Color.White),
                (now.ToString("tt"), Color.LightGray)
            };

            float fontSize = Math.Min(14, clockRadius / 10);
            using Font font = new Font("Arial", fontSize, FontStyle.Bold);
            using StringFormat format = new StringFormat(StringFormat.GenericTypographic);

            // Calculate total width
            float totalWidth = 0;
            foreach (var part in parts)
            {
                totalWidth += g.MeasureString(part.text, font, PointF.Empty, format).Width;
            }
            
            float x = (ClientSize.Width - totalWidth) / 2;
            float y = ClientSize.Height - g.MeasureString("A", font).Height - (ClientSize.Height * 0.05f);

            foreach (var (text, color) in parts)
            {
                using SolidBrush brush = new SolidBrush(color);
                g.DrawString(text, font, brush, x, y, format);
                x += g.MeasureString(text, font, PointF.Empty, format).Width;
            }
        }

        private void DrawHand(Graphics g, double angle, float length, float width, Color color, float dx = 0, float dy = 0, float taperFactor = 4)
        {
            PointF center = new PointF(centerPoint.X + dx, centerPoint.Y + dy);
            PointF end = new PointF(center.X + (float)Math.Sin(angle) * length, center.Y - (float)Math.Cos(angle) * length);

            float halfWidth = width / 2;
            float endHalfWidth = width / taperFactor; // Taper factor for vintage look

            PointF centerLeft = new PointF(center.X + (float)Math.Sin(angle - Math.PI / 2) * halfWidth, center.Y - (float)Math.Cos(angle - Math.PI / 2) * halfWidth);
            PointF centerRight = new PointF(center.X + (float)Math.Sin(angle + Math.PI / 2) * halfWidth, center.Y - (float)Math.Cos(angle + Math.PI / 2) * halfWidth);

            PointF endLeft = new PointF(end.X + (float)Math.Sin(angle - Math.PI / 2) * endHalfWidth, end.Y - (float)Math.Cos(angle - Math.PI / 2) * endHalfWidth);
            PointF endRight = new PointF(end.X + (float)Math.Sin(angle + Math.PI / 2) * endHalfWidth, end.Y - (float)Math.Cos(angle + Math.PI / 2) * endHalfWidth);

            PointF[] points = { centerLeft, centerRight, endRight, endLeft };

            using SolidBrush brush = new SolidBrush(color);
            g.FillPolygon(brush, points);

            // Optional: draw outline for definition
            using Pen pen = new Pen(Color.Black, 1);
            g.DrawPolygon(pen, points);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateClockDimensions();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer?.Stop();
            timer?.Dispose();
            base.OnFormClosing(e);
        }

        // Event handlers to close screensaver
        private Point lastMousePos;
        private void AnalogClockForm_MouseMove(object sender, MouseEventArgs e)
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

        private void AnalogClockForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isPreview) Application.Exit();
        }

        private void AnalogClockForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!isPreview) Application.Exit();
        }
    }
}