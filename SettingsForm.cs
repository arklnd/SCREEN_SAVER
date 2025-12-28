using System;
using System.Drawing;
using System.Windows.Forms;

namespace SCREEN_SAVER
{
    public class SettingsForm : Form
    {
        private Settings settings;
        private TrackBar tbSpeed = null!;
        private TrackBar tbLifespan = null!;
        private TrackBar tbTrail = null!;
        private TrackBar tbSize = null!;
        private CheckBox chkRandomColors = null!;
        private Button btnColor = null!;
        private Panel pnlColorPreview = null!;
        private Button btnSave = null!;
        private Button btnReset = null!;
        private Button btnCancel = null!;

        public SettingsForm()
        {
            settings = Settings.Load();
            InitializeComponent();
            LoadSettingsToControls();
        }

        private void InitializeComponent()
        {
            this.Text = "Screensaver Settings";
            this.Size = new Size(400, 450);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            int y = 20;
            int labelWidth = 100;
            int controlWidth = 200;
            int margin = 10;

            // Animation Group
            GroupBox grpAnimation = new GroupBox();
            grpAnimation.Text = "Animation";
            grpAnimation.Bounds = new Rectangle(10, y, 360, 180);
            this.Controls.Add(grpAnimation);

            int gy = 20;

            // Speed
            Label lblSpeed = new Label();
            lblSpeed.Text = "Speed:";
            lblSpeed.Bounds = new Rectangle(margin, gy, labelWidth, 20);
            grpAnimation.Controls.Add(lblSpeed);

            tbSpeed = new TrackBar();
            tbSpeed.Minimum = 5;
            tbSpeed.Maximum = 50; // 0.5x to 5.0x
            tbSpeed.TickFrequency = 5;
            tbSpeed.Bounds = new Rectangle(margin + labelWidth, gy, controlWidth, 45);
            grpAnimation.Controls.Add(tbSpeed);
            gy += 50;

            // Lifespan
            Label lblLifespan = new Label();
            lblLifespan.Text = "Lifespan (s):";
            lblLifespan.Bounds = new Rectangle(margin, gy, labelWidth, 20);
            grpAnimation.Controls.Add(lblLifespan);

            tbLifespan = new TrackBar();
            tbLifespan.Minimum = 1;
            tbLifespan.Maximum = 30;
            tbLifespan.TickFrequency = 5;
            tbLifespan.Bounds = new Rectangle(margin + labelWidth, gy, controlWidth, 45);
            grpAnimation.Controls.Add(tbLifespan);
            gy += 50;

            // Trail
            Label lblTrail = new Label();
            lblTrail.Text = "Trail Length:";
            lblTrail.Bounds = new Rectangle(margin, gy, labelWidth, 20);
            grpAnimation.Controls.Add(lblTrail);

            tbTrail = new TrackBar();
            tbTrail.Minimum = 10;
            tbTrail.Maximum = 200;
            tbTrail.TickFrequency = 20;
            tbTrail.Bounds = new Rectangle(margin + labelWidth, gy, controlWidth, 45);
            grpAnimation.Controls.Add(tbTrail);

            y += 190;

            // Appearance Group
            GroupBox grpAppearance = new GroupBox();
            grpAppearance.Text = "Appearance";
            grpAppearance.Bounds = new Rectangle(10, y, 360, 130);
            this.Controls.Add(grpAppearance);

            gy = 20;

            // Size
            Label lblSize = new Label();
            lblSize.Text = "Clock Size:";
            lblSize.Bounds = new Rectangle(margin, gy, labelWidth, 20);
            grpAppearance.Controls.Add(lblSize);

            tbSize = new TrackBar();
            tbSize.Minimum = 1;
            tbSize.Maximum = 10; // Map to divisor
            tbSize.Bounds = new Rectangle(margin + labelWidth, gy, controlWidth, 45);
            grpAppearance.Controls.Add(tbSize);
            gy += 50;

            // Colors
            chkRandomColors = new CheckBox();
            chkRandomColors.Text = "Random Colors";
            chkRandomColors.Bounds = new Rectangle(margin, gy, 120, 25);
            chkRandomColors.CheckedChanged += ChkRandomColors_CheckedChanged;
            grpAppearance.Controls.Add(chkRandomColors);

            btnColor = new Button();
            btnColor.Text = "Pick Color";
            btnColor.Bounds = new Rectangle(margin + 130, gy, 80, 25);
            btnColor.Click += BtnColor_Click;
            grpAppearance.Controls.Add(btnColor);

            pnlColorPreview = new Panel();
            pnlColorPreview.BorderStyle = BorderStyle.FixedSingle;
            pnlColorPreview.Bounds = new Rectangle(margin + 220, gy, 25, 25);
            grpAppearance.Controls.Add(pnlColorPreview);

            y += 140;

            // Buttons
            btnReset = new Button();
            btnReset.Text = "Reset";
            btnReset.Bounds = new Rectangle(20, y, 80, 30);
            btnReset.Click += BtnReset_Click;
            this.Controls.Add(btnReset);

            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.DialogResult = DialogResult.OK;
            btnSave.Bounds = new Rectangle(200, y, 80, 30);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Bounds = new Rectangle(290, y, 80, 30);
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void LoadSettingsToControls()
        {
            tbSpeed.Value = (int)(settings.SpeedMultiplier * 10);
            tbLifespan.Value = (int)settings.ProjectileLifespan;
            tbTrail.Value = settings.TrailLength;
            
            // Map divisor 6.0 -> 1.5 to slider 1 -> 10
            // 1 -> 6.0 (Small)
            // 10 -> 1.5 (Large)
            // Linear interpolation: Divisor = 6.5 - 0.5 * Slider
            float sliderVal = (6.5f - settings.ClockSize) / 0.5f;
            tbSize.Value = Math.Max(tbSize.Minimum, Math.Min(tbSize.Maximum, (int)sliderVal));

            chkRandomColors.Checked = settings.UseRandomColors;
            pnlColorPreview.BackColor = settings.FixedColor;
            UpdateColorControls();
        }

        private void UpdateColorControls()
        {
            btnColor.Enabled = !chkRandomColors.Checked;
            pnlColorPreview.Visible = !chkRandomColors.Checked;
        }

        private void ChkRandomColors_CheckedChanged(object sender, EventArgs e)
        {
            UpdateColorControls();
        }

        private void BtnColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = pnlColorPreview.BackColor;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                pnlColorPreview.BackColor = cd.Color;
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            settings.Reset();
            LoadSettingsToControls();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            settings.SpeedMultiplier = tbSpeed.Value / 10.0f;
            settings.ProjectileLifespan = tbLifespan.Value;
            settings.TrailLength = tbTrail.Value;
            
            // Map slider 1 -> 10 to divisor 6.0 -> 1.5
            settings.ClockSize = 6.5f - (0.5f * tbSize.Value);
            
            settings.UseRandomColors = chkRandomColors.Checked;
            settings.FixedColor = pnlColorPreview.BackColor;

            settings.Save();
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
