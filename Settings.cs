using Microsoft.Win32;
using System;
using System.Drawing;

namespace SCREEN_SAVER
{
    public class Settings
    {
        public float SpeedMultiplier { get; set; } = 1.2f;
        public float ProjectileLifespan { get; set; } = 10.0f;
        public int TrailLength { get; set; } = 50;
        public float ClockSize { get; set; } = 3.5f; // Divisor: smaller is bigger
        public bool UseRandomColors { get; set; } = true;
        public Color FixedColor { get; set; } = Color.Cyan;

        // Thunder Settings
        public float ThunderJitter { get; set; } = 5.0f;
        public float BranchProbability { get; set; } = 0.4f;
        public int BranchDepth { get; set; } = 5;

        private const string RegistryPath = @"SOFTWARE\Arklnd\ArcReactorScreensaver";

        public void Reset()
        {
            SpeedMultiplier = 1.2f;
            ProjectileLifespan = 10.0f;
            TrailLength = 50;
            ClockSize = 3.5f;
            UseRandomColors = true;
            FixedColor = Color.Cyan;
            ThunderJitter = 5.0f;
            BranchProbability = 0.4f;
            BranchDepth = 5;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue("SpeedMultiplier", SpeedMultiplier);
                    key.SetValue("ProjectileLifespan", ProjectileLifespan);
                    key.SetValue("TrailLength", TrailLength);
                    key.SetValue("ClockSize", ClockSize);
                    key.SetValue("UseRandomColors", UseRandomColors ? 1 : 0);
                    key.SetValue("FixedColor", FixedColor.ToArgb());
                    key.SetValue("ThunderJitter", ThunderJitter);
                    key.SetValue("BranchProbability", BranchProbability);
                    key.SetValue("BranchDepth", BranchDepth);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to save settings: " + ex.Message);
            }
        }

        public static Settings Load()
        {
            Settings settings = new Settings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        settings.SpeedMultiplier = Convert.ToSingle(key.GetValue("SpeedMultiplier", 1.2f));
                        settings.ProjectileLifespan = Convert.ToSingle(key.GetValue("ProjectileLifespan", 10.0f));
                        settings.TrailLength = Convert.ToInt32(key.GetValue("TrailLength", 50));
                        settings.ClockSize = Convert.ToSingle(key.GetValue("ClockSize", 3.5f));
                        settings.UseRandomColors = Convert.ToInt32(key.GetValue("UseRandomColors", 1)) == 1;
                        settings.FixedColor = Color.FromArgb(Convert.ToInt32(key.GetValue("FixedColor", Color.Cyan.ToArgb())));
                        settings.ThunderJitter = Convert.ToSingle(key.GetValue("ThunderJitter", 5.0f));
                        settings.BranchProbability = Convert.ToSingle(key.GetValue("BranchProbability", 0.4f));
                        settings.BranchDepth = Convert.ToInt32(key.GetValue("BranchDepth", 5));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to load settings: " + ex.Message);
            }
            return settings;
        }
    }
}
