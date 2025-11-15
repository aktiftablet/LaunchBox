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
                    string saveDir = System.IO.Path.Combine(appDataPath, "LaunchBox");
                    string saveFile = System.IO.Path.Combine(saveDir, "data.json");

                    // Delete the data file
                    if (File.Exists(saveFile))
                    {
                        File.Delete(saveFile);
                        System.Diagnostics.Debug.WriteLine("Data file deleted successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Data file not found, nothing to delete");
                    }

                    // Delete the icon cache directory
                    //if (Directory.Exists(iconCacheDir))
                    //{
                    //    Directory.Delete(iconCacheDir, recursive: true);
                    //    System.Diagnostics.Debug.WriteLine("Icon cache directory deleted successfully");
                    //}
                    //else
                    //{
                    //    System.Diagnostics.Debug.WriteLine("Icon cache directory not found, nothing to delete");
                    //}

                    // If the main directory is now empty, remove it as well
                    if (Directory.Exists(saveDir) && !Directory.GetFileSystemEntries(saveDir).Any())
                    {
                        Directory.Delete(saveDir);
                        System.Diagnostics.Debug.WriteLine("LaunchBox directory deleted successfully");
                    }

                    // Exit the application immediately after clearing data
                    System.Diagnostics.Debug.WriteLine("Data cleared successfully. Exiting application.");
                    Environment.Exit(0);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to clear data: {ex.Message}");
                    Environment.Exit(1); // Exit with error code
                    return;
                }
            }

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
