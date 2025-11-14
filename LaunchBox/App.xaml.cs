using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LaunchBox
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Handle -clear argument
            var commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Contains("-clear", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string saveDir = Path.Combine(appDataPath, "LaunchBox");
                    string saveFile = Path.Combine(saveDir, "data.json");
                    string iconCacheDir = Path.Combine(saveDir, "Icons");

                    if (File.Exists(saveFile))
                    {
                        File.Delete(saveFile);
                    }
                    if (Directory.Exists(iconCacheDir))
                    {
                        Directory.Delete(iconCacheDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    // Optionally, log this error. For now, we'll just let the app continue.
                    System.Diagnostics.Debug.WriteLine($"Failed to clear data: {ex.Message}");
                }
            }

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
