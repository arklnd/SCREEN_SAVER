using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SCREEN_SAVER
{
    public partial class AnalogClockForm : Form
    {
        // For preview window handle
        private readonly IntPtr previewHandle = IntPtr.Zero;
        private readonly bool isPreview = false;
        private Rectangle? targetClockArea = null;

        private CancellationTokenSource cancellationTokenSource;
        private readonly object syncLock = new object();
        private PointF centerPoint;
        private float clockRadius;

        private class Projectile
        {
            public PointF Position;
            public PointF Velocity;
            public Color Color;
            public DateTime CreationTime;
            public bool HasBlasted;
            public List<PointF> Trail = new List<PointF>();
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

        public AnalogClockForm(Rectangle bounds, Rectangle clockArea)
        {
            InitializeComponent();
            this.Bounds = bounds;
            this.StartPosition = FormStartPosition.Manual;
            this.targetClockArea = clockArea;
            this.isPreview = false;
            this.WindowState = FormWindowState.Normal;
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
            projectiles = new List<Projectile>();
            blasts = new List<Blast>();

            // Calculate clock center and radius
            UpdateClockDimensions();

            StartAnimation();
        }

        private void UpdateClockDimensions()
        {
            if (targetClockArea.HasValue)
            {
                float centerX = targetClockArea.Value.X - this.Location.X + targetClockArea.Value.Width / 2f;
                float centerY = targetClockArea.Value.Y - this.Location.Y + targetClockArea.Value.Height / 2f;
                centerPoint = new PointF(centerX, centerY);
                clockRadius = Math.Min(targetClockArea.Value.Width, targetClockArea.Value.Height) / 3f;
            }
            else
            {
                centerPoint = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
                clockRadius = Math.Min(ClientSize.Width, ClientSize.Height) / 3f;
            }
        }

        private void StartAnimation()
        {
            cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => AnimationLoop(cancellationTokenSource.Token));
        }

        private void AnimationLoop(CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastTime = stopwatch.ElapsedMilliseconds;

            while (!token.IsCancellationRequested)
            {
                long currentTime = stopwatch.ElapsedMilliseconds;
                float dt = (currentTime - lastTime) / 1000f;
                lastTime = currentTime;

                // Cap dt to avoid huge jumps
                if (dt > 0.1f) dt = 0.1f;

                lock (syncLock)
                {
                    UpdatePhysics(dt);
                }

                try
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        Invalidate();
                    }
                }
                catch { }

                // Target 60 FPS (approx 16ms)
                int elapsed = (int)(stopwatch.ElapsedMilliseconds - currentTime);
                int sleepTime = 16 - elapsed;
                if (sleepTime > 0) Thread.Sleep(sleepTime);
            }
        }

        private void UpdatePhysics(float dt)
        {
            DateTime now = DateTime.Now;
            if (now.Second != lastSecond)
            {
                double angle = now.Second * 6 * Math.PI / 180;
                float speed = clockRadius * 1.2f; // Adjusted speed for real-time update
                float vx = (float)Math.Sin(angle) * speed;
                float vy = -(float)Math.Cos(angle) * speed;
                
                Color projColor = Color.FromArgb(
                    random.Next(100, 255),
                    random.Next(100, 255),
                    random.Next(100, 255));

                projectiles.Add(new Projectile 
                { 
                    Position = centerPoint,
                    Velocity = new PointF(vx, vy),
                    Color = projColor,
                    CreationTime = now,
                    HasBlasted = false
                });
                lastSecond = now.Second;
            }

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                const double projectileLifespan = 10.0; // seconds 
                var p = projectiles[i];
                double age = (now - p.CreationTime).TotalSeconds;
                
                // Update trail - monotonically decreasing length
                int maxTrail = (int)(50 * (1.0 - age / projectileLifespan));
                if (maxTrail < 0) maxTrail = 0;

                p.Trail.Add(p.Position);
                while (p.Trail.Count > maxTrail && p.Trail.Count > 0) p.Trail.RemoveAt(0);

                // Update position
                p.Position.X += p.Velocity.X * dt;
                p.Position.Y += p.Velocity.Y * dt;

                // Bounce logic
                if (p.Position.X < 0) { p.Position.X = 0; p.Velocity.X = -p.Velocity.X; }
                if (p.Position.X > ClientSize.Width) { p.Position.X = ClientSize.Width; p.Velocity.X = -p.Velocity.X; }
                if (p.Position.Y < 0) { p.Position.Y = 0; p.Velocity.Y = -p.Velocity.Y; }
                if (p.Position.Y > ClientSize.Height) { p.Position.Y = ClientSize.Height; p.Velocity.Y = -p.Velocity.Y; }

                // Blast logic (escaping clock face)
                if (!p.HasBlasted)
                {
                    float dx = p.Position.X - centerPoint.X;
                    float dy = p.Position.Y - centerPoint.Y;
                    float distSq = dx*dx + dy*dy;
                    if (distSq >= clockRadius * clockRadius)
                    {
                        p.HasBlasted = true;
                        
                        // Calculate exact intersection point on the perimeter
                        float dist = (float)Math.Sqrt(distSq);
                        float scale = clockRadius / dist;
                        PointF blastPos = new PointF(
                            centerPoint.X + dx * scale,
                            centerPoint.Y + dy * scale
                        );

                        // Use projectile color for blast
                        blasts.Add(new Blast 
                        { 
                            position = blastPos, 
                            startTime = now,
                            color = p.Color
                        });
                    }
                }

                // Lifespan projectileLifespan-s - end with a blast
                if (age > projectileLifespan)
                {
                    blasts.Add(new Blast 
                    { 
                        position = p.Position, 
                        startTime = now,
                        color = p.Color
                    });
                    projectiles.RemoveAt(i);
                }
            }

            // Remove old blasts
            blasts.RemoveAll(b => (now - b.startTime).TotalSeconds > 1.0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            lock (syncLock)
            {
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
        }

        private void DrawClockFace(Graphics g)
        {
            DateTime now = DateTime.Now;
            long ticks = now.Ticks;
            float rotationSlow = (ticks / 200000f) % 360;
            float rotationFast = -(ticks / 100000f) % 360;

            // 1. Fill with a deep space gradient
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(centerPoint.X - clockRadius, centerPoint.Y - clockRadius,
                               clockRadius * 2, clockRadius * 2);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(20, 30, 50); 
                    brush.SurroundColors = new Color[] { Color.Black };
                    g.FillPath(brush, path);
                }
            }

            // 2. Draw rotating tech rings
            using (Pen ringPen = new Pen(Color.FromArgb(40, 100, 200, 255), 2))
            {
                // Outer segmented ring
                float outerR = clockRadius * 0.95f;
                for (int i = 0; i < 12; i++)
                {
                    float startAngle = i * 30 + rotationSlow;
                    g.DrawArc(ringPen, centerPoint.X - outerR, centerPoint.Y - outerR, 
                        outerR * 2, outerR * 2, startAngle, 20);
                }

                // Inner segmented ring (counter-rotating)
                float innerR = clockRadius * 0.6f;
                ringPen.Color = Color.FromArgb(30, 100, 255, 200);
                ringPen.Width = 1;
                for (int i = 0; i < 8; i++)
                {
                    float startAngle = i * 45 + rotationFast;
                    g.DrawArc(ringPen, centerPoint.X - innerR, centerPoint.Y - innerR,
                        innerR * 2, innerR * 2, startAngle, 35);
                }
            }

            // 3. Draw static grid/crosshair
            using (Pen gridPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1))
            {
                gridPen.DashStyle = DashStyle.Dot;
                g.DrawLine(gridPen, centerPoint.X - clockRadius, centerPoint.Y, centerPoint.X + clockRadius, centerPoint.Y);
                g.DrawLine(gridPen, centerPoint.X, centerPoint.Y - clockRadius, centerPoint.X, centerPoint.Y + clockRadius);
                g.DrawEllipse(gridPen, centerPoint.X - clockRadius * 0.3f, centerPoint.Y - clockRadius * 0.3f, clockRadius * 0.6f, clockRadius * 0.6f);
            }

            // 4. Draw the main rim with glow
            using (Pen pen = new Pen(Color.FromArgb(100, 0, 200, 255), 3))
            {
                g.DrawEllipse(pen, centerPoint.X - clockRadius, centerPoint.Y - clockRadius,
                             clockRadius * 2, clockRadius * 2);
            }

            // 5. Draw reactive arcs for blasts on the rim
            foreach (var blast in blasts)
            {
                float dx = blast.position.X - centerPoint.X;
                float dy = blast.position.Y - centerPoint.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (Math.Abs(dist - clockRadius) < 20)
                {
                    double age = (now - blast.startTime).TotalSeconds;
                    if (age < 0.8) 
                    {
                        float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
                        float sweep = (float)(60 * (1.0 - age / 0.8)); 
                        int alpha = (int)(255 * (1.0 - age / 0.8));
                        if (alpha > 255) alpha = 255;
                        if (alpha < 0) alpha = 0;
                        
                        // Energy shield effect
                        using (Pen blastPen = new Pen(Color.FromArgb(alpha, Color.Cyan), 4))
                        {
                            g.DrawArc(blastPen, centerPoint.X - clockRadius, centerPoint.Y - clockRadius,
                                     clockRadius * 2, clockRadius * 2, angle - sweep/2, sweep);
                        }
                        
                        // Hexagon fragment effect at impact
                        using (SolidBrush hexBrush = new SolidBrush(Color.FromArgb(alpha / 2, Color.Cyan)))
                        {
                            PointF[] hex = new PointF[6];
                            for(int k=0; k<6; k++) {
                                float ha = angle * (float)Math.PI/180 + k * (float)Math.PI/3;
                                hex[k] = new PointF(
                                    blast.position.X + 15 * (float)Math.Cos(ha),
                                    blast.position.Y + 15 * (float)Math.Sin(ha)
                                );
                            }
                            g.FillPolygon(hexBrush, hex);
                        }
                    }
                }
            }

            // 6. Draw center "Reactor"
            float pulse = (float)(Math.Sin(now.Millisecond / 1000.0 * 4 * Math.PI) * 3 + 8); 
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(200, 0, 255, 255)))
            {
                g.FillEllipse(brush, centerPoint.X - pulse, centerPoint.Y - pulse, pulse * 2, pulse * 2);
            }
            using (Pen reactorPen = new Pen(Color.FromArgb(100, 0, 255, 255), 2))
            {
                g.DrawEllipse(reactorPen, centerPoint.X - 15, centerPoint.Y - 15, 30, 30);
            }
        }

        private void DrawHourMarkers(Graphics g)
        {
            using Pen hourPen = new Pen(Color.FromArgb(200, 0, 255, 255), 2);
            using SolidBrush hourBrush = new SolidBrush(Color.FromArgb(200, 0, 255, 255));
            using Pen minutePen = new Pen(Color.FromArgb(50, 255, 255, 255), 1);

            for (int i = 0; i < 60; i++)
            {
                double angle = i * 6 * Math.PI / 180;
                
                if (i % 5 == 0) // Hour marker
                {
                    // Draw a small tech rectangle/chevron
                    float r1 = clockRadius - 25;
                    float r2 = clockRadius - 10;
                    
                    PointF p1 = new PointF(
                        centerPoint.X + (float)Math.Sin(angle) * r1,
                        centerPoint.Y - (float)Math.Cos(angle) * r1
                    );
                    PointF p2 = new PointF(
                        centerPoint.X + (float)Math.Sin(angle) * r2,
                        centerPoint.Y - (float)Math.Cos(angle) * r2
                    );
                    
                    g.DrawLine(hourPen, p1, p2);
                    
                    // Add a small dot at the inner tip
                    g.FillRectangle(hourBrush, p1.X - 2, p1.Y - 2, 4, 4);
                }
                else // Minute marker
                {
                    float r1 = clockRadius - 15;
                    float r2 = clockRadius - 10;
                    
                    PointF p1 = new PointF(
                        centerPoint.X + (float)Math.Sin(angle) * r1,
                        centerPoint.Y - (float)Math.Cos(angle) * r1
                    );
                    PointF p2 = new PointF(
                        centerPoint.X + (float)Math.Sin(angle) * r2,
                        centerPoint.Y - (float)Math.Cos(angle) * r2
                    );
                    g.DrawLine(minutePen, p1, p2);
                }
            }
        }

        private void DrawNumbers(Graphics g)
        {
            using Font font = new Font("Consolas", clockRadius / 10, FontStyle.Bold);
            using SolidBrush brush = new SolidBrush(Color.FromArgb(180, 0, 255, 255));

            for (int hour = 1; hour <= 12; hour++)
            {
                double angle = ((hour % 12) * 30) * Math.PI / 180; 
                float numberRadius = clockRadius - 50;

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

            // Hour hand - Hollow tech shape
            double hourAngle = ((now.Hour % 12) * 30 + now.Minute * 0.5) * Math.PI / 180;
            DrawTechHand(g, hourAngle, clockRadius * 0.5f, 8, Color.FromArgb(200, 255, 255, 255));

            // Minute hand - Longer hollow tech shape
            double minuteAngle = (now.Minute * 6 + now.Second * 0.1) * Math.PI / 180;
            DrawTechHand(g, minuteAngle, clockRadius * 0.75f, 5, Color.FromArgb(200, 0, 255, 255));

            // Draw projectiles instead of second hand
            DrawProjectiles(g);
            DrawBlasts(g);
        }

        private void DrawTechHand(Graphics g, double angle, float length, float width, Color color)
        {
            // Calculate points for a "sword" or "needle" shape
            PointF start = centerPoint;
            PointF end = new PointF(
                centerPoint.X + (float)Math.Sin(angle) * length,
                centerPoint.Y - (float)Math.Cos(angle) * length
            );
            
            // Perpendicular vector for width
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float len = (float)Math.Sqrt(dx*dx + dy*dy);
            float px = -dy / len * width;
            float py = dx / len * width;

            PointF[] points = new PointF[]
            {
                new PointF(start.X + px, start.Y + py),
                new PointF(end.X, end.Y),
                new PointF(start.X - px, start.Y - py),
                new PointF(start.X, start.Y) // Close loop
            };

            using (Pen pen = new Pen(color, 2))
            {
                g.DrawPolygon(pen, points);
                
                // Fill with low opacity
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(50, color)))
                {
                    g.FillPolygon(brush, points);
                }
            }
            
            // Draw a glowing tip
            using (SolidBrush tipBrush = new SolidBrush(color))
            {
                g.FillEllipse(tipBrush, end.X - 2, end.Y - 2, 4, 4);
            }
        }

        private void DrawBlasts(Graphics g)
        {
            DateTime now = DateTime.Now;
            for (int i = 0; i < blasts.Count; i++)
            {
                var blast = blasts[i];
                double t = (now - blast.startTime).TotalSeconds;
                if (t > 1.0) continue;

                // Balloon expands and fades
                float progress = (float)(t / 1.0);
                float size = 40f + 500f * progress; // Expands from 40 to 540
                
                // Smoother fade using a non-linear curve (e.g., squared) for more natural dissipation
                float fadeProgress = 1.0f - progress;
                int alpha = (int)(255 * fadeProgress * fadeProgress); 
                if (alpha < 0) alpha = 0;
                if (alpha > 255) alpha = 255;
                
                RectangleF rect = new RectangleF(
                    blast.position.X - size / 2, 
                    blast.position.Y - size / 2, 
                    size, size);

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(rect);
                    using (PathGradientBrush brush = new PathGradientBrush(path))
                    {
                        brush.CenterColor = Color.FromArgb(alpha, blast.color);
                        brush.SurroundColors = new Color[] { Color.FromArgb(0, blast.color) };
                        g.FillPath(brush, path);
                    }
                }
            }
        }

        private void DrawProjectiles(Graphics g)
        {
            foreach (var p in projectiles)
            {
                // Draw trail with connected lines for smoothness
                if (p.Trail.Count > 1)
                {
                    for (int i = 0; i < p.Trail.Count - 1; i++)
                    {
                        float progress = (float)i / p.Trail.Count;
                        float nextProgress = (float)(i + 1) / p.Trail.Count;
                        
                        float alpha = 255f * progress;
                        float size = 6f * progress;
                        if (size < 0.5f) size = 0.5f; // Minimum visible size
                        
                        // Draw a line segment between points
                        using (Pen pen = new Pen(Color.FromArgb((int)alpha, p.Color), size))
                        {
                            pen.StartCap = LineCap.Round;
                            pen.EndCap = LineCap.Round;
                            g.DrawLine(pen, p.Trail[i], p.Trail[i+1]);
                        }
                    }
                }
                
                // Draw head
                using (SolidBrush brush = new SolidBrush(p.Color))
                {
                    g.FillEllipse(brush, p.Position.X - 3, p.Position.Y - 3, 6, 6);
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
            
            float x = centerPoint.X - totalWidth / 2;
            float y;

            if (targetClockArea.HasValue)
            {
                // Position at bottom of the target clock area (main screen)
                float bottomOfClockArea = targetClockArea.Value.Y - this.Location.Y + targetClockArea.Value.Height;
                y = bottomOfClockArea - g.MeasureString("A", font).Height - (targetClockArea.Value.Height * 0.05f);
            }
            else
            {
                y = ClientSize.Height - g.MeasureString("A", font).Height - (ClientSize.Height * 0.05f);
            }

            foreach (var (text, color) in parts)
            {
                using SolidBrush brush = new SolidBrush(color);
                g.DrawString(text, font, brush, x, y, format);
                x += g.MeasureString(text, font, PointF.Empty, format).Width;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateClockDimensions();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cancellationTokenSource?.Cancel();
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