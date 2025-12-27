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

        static void ShowSettings()
        {
            MessageBox.Show("This screensaver has no configurable settings.",
                "Expanding Circle Screensaver",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static void ShowPreview(string previewHandle)
        {
            IntPtr handle = new(long.Parse(previewHandle));
            Application.Run(new AnalogClockForm(handle, true));
        }

        static void ShowScreensaver()
        {
            // Create a form for each screen
            AnalogClockForm[] forms = new AnalogClockForm[Screen.AllScreens.Length];
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                forms[i] = new AnalogClockForm(Screen.AllScreens[i].Bounds);
                forms[i].Show();
            }

            // If only one screen, run the application with that form
            if (forms.Length == 1)
            {
                Application.Run(forms[0]);
            }
            else
            {
                // For multiple screens, run without a main form
                Application.Run();
            }
        }
    }
}