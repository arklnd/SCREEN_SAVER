using System;
using System.Windows.Forms;

namespace SCREEN_SAVER
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                if (args.Length > 0)
            {
                string firstArgument = args[0].ToLower().Trim();
                string? secondArgument = null;

                // Handle cases where arguments are separated by colon
                if (firstArgument.Length > 2)
                {
                    secondArgument = firstArgument.Substring(3).Trim();
                    firstArgument = firstArgument.Substring(0, 2);
                }
                else if (args.Length > 1)
                {
                    secondArgument = args[1];
                }

                switch (firstArgument)
                {
                    case "/c":
                        // Configuration dialog
                        ShowSettings();
                        break;
                    case "/p":
                        // Preview mode
                        if (secondArgument != null)
                        {
                            ShowPreview(secondArgument);
                        }
                        else
                        {
                            MessageBox.Show("Preview mode requires a window handle.", "Expanding Circle Screensaver",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    case "/s":
                        // Full-screen screensaver mode
                        ShowScreensaver();
                        break;
                    default:
                        // Undefined argument
                        MessageBox.Show("Invalid command line argument.", "Expanding Circle Screensaver",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                }
            }
            else
            {
                // No arguments - run as screensaver
                ShowScreensaver();
            }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void ShowSettings()
        {
            Application.Run(new SettingsForm());
        }

        static void ShowPreview(string previewHandle)
        {
            IntPtr handle = new(long.Parse(previewHandle));
            Application.Run(new AnalogClockForm(handle, true));
        }

        static void ShowScreensaver()
        {
            // Run one form covering all screens
            Rectangle virtualBounds = SystemInformation.VirtualScreen;
            Rectangle primaryBounds = Screen.PrimaryScreen.Bounds;

            AnalogClockForm form = new AnalogClockForm(virtualBounds, primaryBounds);
            form.Show();
            Application.Run(form);
        }
    }
}