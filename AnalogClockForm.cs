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

        private CancellationTokenSource cancellationTokenSource = null!;
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

        private List<Projectile> projectiles = new List<Projectile>();
        private List<Blast> blasts = new List<Blast>();
        private Random random = new Random();
        private int lastSecond = -1;
        private Color currentPulseColor = Color.Cyan;
        private Settings settings = null!;

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
            settings = Settings.Load();
            projectiles = new List<Projectile>();
            blasts = new List<Blast>();

            // Calculate clock center and radius
            UpdateClockDimensions();

            StartAnimation();
        }

        private void UpdateClockDimensions()
        {
            if (settings == null) return;

            if (targetClockArea.HasValue)
            {
                float centerX = targetClockArea.Value.X - this.Location.X + targetClockArea.Value.Width / 2f;
                float centerY = targetClockArea.Value.Y - this.Location.Y + targetClockArea.Value.Height / 2f;
                centerPoint = new PointF(centerX, centerY);
                clockRadius = Math.Min(targetClockArea.Value.Width, targetClockArea.Value.Height) / settings.ClockSize;
            }
            else
            {
                centerPoint = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
                clockRadius = Math.Min(ClientSize.Width, ClientSize.Height) / settings.ClockSize;
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
                float speed = clockRadius * settings.SpeedMultiplier; // Adjusted speed for real-time update
                float vx = (float)Math.Sin(angle) * speed;
                float vy = -(float)Math.Cos(angle) * speed;
                
                Color projColor;
                if (settings.UseRandomColors)
                {
                    projColor = Color.FromArgb(
                        random.Next(100, 255),
                        random.Next(100, 255),
                        random.Next(100, 255));
                }
                else
                {
                    projColor = settings.FixedColor;
                }
                
                currentPulseColor = projColor;

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
                double projectileLifespan = settings.ProjectileLifespan; // seconds 
                var p = projectiles[i];
                double age = (now - p.CreationTime).TotalSeconds;
                
                // Update trail - monotonically decreasing length
                int maxTrail = (int)(settings.TrailLength * (1.0 - age / projectileLifespan));
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

            if (clockRadius < 5 || ClientSize.Width < 5 || ClientSize.Height < 5) return;

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
            if (float.IsNaN(centerPoint.X) || float.IsNaN(centerPoint.Y) || float.IsNaN(clockRadius)) return;

            DateTime now = DateTime.Now;
            
            // Arc Reactor Colors - Use currentPulseColor for the glow to match projectiles
            Color glowColor = currentPulseColor;
            Color coreColor = Color.White;
            Color metalColor = Color.FromArgb(40, 40, 45);
            Color darkMetalColor = Color.FromArgb(15, 15, 20);

            // 1. Background / Housing
            using (SolidBrush bgBrush = new SolidBrush(darkMetalColor))
            {
                g.FillEllipse(bgBrush, centerPoint.X - clockRadius, centerPoint.Y - clockRadius, clockRadius * 2, clockRadius * 2);
            }
            
            // Outer Rim
            using (Pen rimPen = new Pen(metalColor, clockRadius * 0.05f))
            {
                g.DrawEllipse(rimPen, centerPoint.X - clockRadius * 0.975f, centerPoint.Y - clockRadius * 0.975f, clockRadius * 1.95f, clockRadius * 1.95f);
            }

            // 2. Glowing Segments (The "Power Cells")
            int segmentCount = 12;
            float innerR = clockRadius * 0.45f;
            float outerR = clockRadius * 0.85f;
            float angleStep = 360f / segmentCount;
            float gap = 10f; // degrees

            for (int i = 0; i < segmentCount; i++)
            {
                float startAngle = i * angleStep + gap / 2;
                float sweepAngle = angleStep - gap;

                using (GraphicsPath segmentPath = new GraphicsPath())
                {
                    segmentPath.AddArc(centerPoint.X - outerR, centerPoint.Y - outerR, outerR * 2, outerR * 2, startAngle, sweepAngle);
                    segmentPath.AddArc(centerPoint.X - innerR, centerPoint.Y - innerR, innerR * 2, innerR * 2, startAngle + sweepAngle, -sweepAngle);
                    segmentPath.CloseFigure();

                    // Fill with glow gradient
                    using (PathGradientBrush brush = new PathGradientBrush(segmentPath))
                    {
                        brush.CenterColor = Color.FromArgb(200, glowColor);
                        brush.CenterPoint = new PointF(
                            centerPoint.X + (float)Math.Cos((startAngle + sweepAngle/2) * Math.PI / 180) * (innerR + outerR) / 2,
                            centerPoint.Y + (float)Math.Sin((startAngle + sweepAngle/2) * Math.PI / 180) * (innerR + outerR) / 2
                        );
                        brush.SurroundColors = new Color[] { Color.FromArgb(20, glowColor) };
                        g.FillPath(brush, segmentPath);
                    }
                    
                    // Outline
                    using (Pen segPen = new Pen(Color.FromArgb(150, glowColor), 2))
                    {
                        g.DrawPath(segPen, segmentPath);
                    }
                }
            }

            // 3. Inner Ring Structure
            using (Pen innerRingPen = new Pen(metalColor, clockRadius * 0.05f))
            {
                g.DrawEllipse(innerRingPen, centerPoint.X - innerR, centerPoint.Y - innerR, innerR * 2, innerR * 2);
            }
            
            // 4. Center Core
            float coreRadius = clockRadius * 0.3f;
            
            // Pulse effect
            float t = now.Millisecond / 1000.0f;
            float pulseIntensity = (float)Math.Pow(1.0f - t, 4); // Pulse decay
            int alpha = (int)(150 + 105 * pulseIntensity);
            
            // Core Glow
            using (GraphicsPath corePath = new GraphicsPath())
            {
                corePath.AddEllipse(centerPoint.X - coreRadius, centerPoint.Y - coreRadius, coreRadius * 2, coreRadius * 2);
                using (PathGradientBrush brush = new PathGradientBrush(corePath))
                {
                    brush.CenterColor = Color.FromArgb(alpha, coreColor);
                    brush.SurroundColors = new Color[] { Color.FromArgb(50, glowColor) };
                    g.FillPath(brush, corePath);
                }
            }

            // Core Detail (Rings)
            using (Pen gridPen = new Pen(Color.FromArgb(100, 0, 0, 0), 2))
            {
                g.DrawEllipse(gridPen, centerPoint.X - coreRadius * 0.7f, centerPoint.Y - coreRadius * 0.7f, coreRadius * 1.4f, coreRadius * 1.4f);
                g.DrawEllipse(gridPen, centerPoint.X - coreRadius * 0.4f, centerPoint.Y - coreRadius * 0.4f, coreRadius * 0.8f, coreRadius * 0.8f);
            }

            // 5. Reactive arcs for blasts on the rim
            foreach (var blast in blasts)
            {
                float dx = blast.position.X - centerPoint.X;
                float dy = blast.position.Y - centerPoint.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (Math.Abs(dist - clockRadius) < 30)
                {
                    double age = (now - blast.startTime).TotalSeconds;
                    if (age < 0.8) 
                    {
                        float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
                        float sweep = (float)(60 * (1.0 - age / 0.8)); 
                        int blastAlpha = (int)(255 * (1.0 - age / 0.8));
                        if (blastAlpha > 255) blastAlpha = 255;
                        if (blastAlpha < 0) blastAlpha = 0;
                        
                        try
                        {
                            if (sweep > 0.5f)
                            {
                                using (Pen blastPen = new Pen(Color.FromArgb(blastAlpha, blast.color), 6))
                                {
                                    g.DrawArc(blastPen, centerPoint.X - clockRadius, centerPoint.Y - clockRadius, 
                                        clockRadius * 2, clockRadius * 2, angle - sweep/2, sweep);
                                }
                            }
                        }
                        catch { } 
                    }
                }
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
            float fontSize = Math.Max(5, clockRadius / 12);
            using Font font = new Font("Consolas", fontSize, FontStyle.Bold);
            using SolidBrush brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));

            for (int hour = 1; hour <= 12; hour++)
            {
                double angle = ((hour % 12) * 30) * Math.PI / 180; 
                float numberRadius = clockRadius * 0.85f;

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
            if (length < 1) return;

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
                if (p.Trail.Count < 2)
                {
                    // Just draw head if no trail yet
                    using (SolidBrush brush = new SolidBrush(Color.White))
                        g.FillEllipse(brush, p.Position.X - 2, p.Position.Y - 2, 4, 4);
                    continue;
                }

                // Generate jittered points for lightning effect
                PointF[] lightningPoints = new PointF[p.Trail.Count];
                for (int i = 0; i < p.Trail.Count; i++)
                {
                    // High jitter everywhere to break the smooth curve
                    float jitter = 12f; 
                    float jx = (float)(random.NextDouble() * 2 - 1) * jitter;
                    float jy = (float)(random.NextDouble() * 2 - 1) * jitter;
                    lightningPoints[i] = new PointF(p.Trail[i].X + jx, p.Trail[i].Y + jy);
                }
                
                // Ensure tip is somewhat near the actual position but still jagged
                lightningPoints[lightningPoints.Length - 1] = new PointF(
                    p.Position.X + (float)(random.NextDouble() * 8 - 4),
                    p.Position.Y + (float)(random.NextDouble() * 8 - 4)
                );

                for (int i = 0; i < lightningPoints.Length - 1; i++)
                {
                    float progress = (float)i / lightningPoints.Length;
                    int alpha = (int)(255 * progress);
                    if (alpha > 255) alpha = 255;
                    
                    // Main Bolt
                    using (Pen glowPen = new Pen(Color.FromArgb(alpha / 2, p.Color), 4f + 4f * progress))
                    {
                        glowPen.StartCap = LineCap.Round;
                        glowPen.EndCap = LineCap.Round;
                        g.DrawLine(glowPen, lightningPoints[i], lightningPoints[i+1]);
                    }

                    using (Pen corePen = new Pen(Color.FromArgb(alpha, Color.White), 1f + 1.5f * progress))
                    {
                        g.DrawLine(corePen, lightningPoints[i], lightningPoints[i+1]);
                    }

                    // Branching
                    // Spawn branches randomly along the trail - increased probability
                    if (i < lightningPoints.Length - 2 && random.NextDouble() < 0.4) 
                    {
                        float dx = lightningPoints[i+1].X - lightningPoints[i].X;
                        float dy = lightningPoints[i+1].Y - lightningPoints[i].Y;
                        
                        // Branch out with higher depth (5 levels)
                        DrawLightningBranch(g, lightningPoints[i], new PointF(dx, dy), p.Color, alpha, 5);
                    }
                }

                // Draw Head (Spark) - Jagged burst
                PointF head = lightningPoints[lightningPoints.Length - 1];
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(200, p.Color)))
                {
                    // Draw a few random lines crossing at the head instead of a ball
                    for(int k=0; k<3; k++) {
                        float len = 15f;
                        float ang = (float)(random.NextDouble() * Math.PI * 2);
                        g.FillPolygon(glowBrush, new PointF[] {
                            new PointF(head.X + (float)Math.Cos(ang)*len, head.Y + (float)Math.Sin(ang)*len),
                            new PointF(head.X + (float)Math.Cos(ang+2)*2, head.Y + (float)Math.Sin(ang+2)*2),
                            new PointF(head.X - (float)Math.Cos(ang)*len, head.Y - (float)Math.Sin(ang)*len),
                            new PointF(head.X - (float)Math.Cos(ang+2)*2, head.Y - (float)Math.Sin(ang+2)*2)
                        });
                    }
                }
                using (SolidBrush coreBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(coreBrush, head.X - 3, head.Y - 3, 6, 6);
                }
            }
        }

        private void DrawLightningBranch(Graphics g, PointF start, PointF mainDir, Color color, int alpha, int depth)
        {
            if (depth <= 0 || alpha < 10) return;

            // Calculate angle of main direction
            double baseAngle = Math.Atan2(mainDir.Y, mainDir.X);
            
            // Determine number of sub-branches at this node (1 to 3)
            int branchCount = random.Next(1, 3);
            if (depth >= 4) branchCount = random.Next(1, 4); // More branches at root levels

            for (int b = 0; b < branchCount; b++)
            {
                // Branch deviates from main path
                // Tighter angle for forward momentum: +/- 10 to 45 degrees (0.17 to 0.78 radians)
                double deviation = (random.NextDouble() * 0.6 + 0.17) * (random.Next(2) == 0 ? 1 : -1); 
                double angle = baseAngle + deviation;
                
                // Length decreases with depth
                float length = (float)(random.NextDouble() * (10 + depth * 3) + 5);
                
                PointF end = new PointF(
                    start.X + (float)Math.Cos(angle) * length,
                    start.Y + (float)Math.Sin(angle) * length
                );

                // Draw branch segment
                using (Pen branchPen = new Pen(Color.FromArgb(alpha / 2, color), Math.Max(0.5f, depth * 0.6f)))
                {
                    g.DrawLine(branchPen, start, end);
                }
                
                // Draw core for branch (thinner)
                using (Pen branchCore = new Pen(Color.FromArgb(alpha / 2, Color.White), 0.5f))
                {
                    g.DrawLine(branchCore, start, end);
                }

                // Recursive call for sub-branches
                // Pass the NEW direction (end - start) to maintain forward flow relative to the branch
                DrawLightningBranch(g, end, new PointF(end.X - start.X, end.Y - start.Y), color, (int)(alpha * 0.75), depth - 1);
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