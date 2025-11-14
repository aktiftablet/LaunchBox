using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace LaunchBox
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<AppContainer> Containers { get; } = new ObservableCollection<AppContainer>();

        private static readonly string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string SaveDir = Path.Combine(AppDataPath, "LaunchBox");
        private static readonly string IconCacheDir = Path.Combine(SaveDir, "Icons");
        private static readonly string SaveFile = Path.Combine(SaveDir, "data.json");

        public MainWindow()
        {
            InitializeComponent();
            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            LoadData();
            Containers.Add(new AppContainer { IsAddButton = true });
            this.Closed += (sender, args) => SaveData();
            ValidateEntriesAsync();
        }

        private async void ValidateEntriesAsync()
        {
            var entriesToRemove = new List<(AppContainer container, AppEntry entry)>();
            var containersSnapshot = Containers.Where(c => !c.IsAddButton).ToList();

            foreach (var container in containersSnapshot)
            {
                foreach (var entry in container.Apps)
                {
                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(entry.FilePath);
                    }
                    catch (FileNotFoundException)
                    {
                        Debug.WriteLine($"File not found, marking for removal: {entry.FilePath}");
                        if (!string.IsNullOrEmpty(entry.IconPath) && File.Exists(entry.IconPath))
                        {
                            try { File.Delete(entry.IconPath); }
                            catch (Exception ex) { Debug.WriteLine($"Failed to delete cached icon: {ex.Message}"); }
                        }
                        entriesToRemove.Add((container, entry));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error validating {entry.FilePath}: {ex.Message}");
                    }
                }
            }

            if (entriesToRemove.Any())
            {
                foreach (var item in entriesToRemove)
                {
                    item.container.Apps.Remove(item.entry);
                }
            }
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(SaveFile)) throw new FileNotFoundException("Save file not found.");
                var json = File.ReadAllText(SaveFile);
                var loadedContainers = JsonSerializer.Deserialize<List<AppContainer>>(json);

                if (loadedContainers != null && loadedContainers.Any())
                {
                    Containers.Clear();
                    foreach (var container in loadedContainers)
                    {
                        if (container.Apps == null) container.Apps = new ObservableCollection<AppEntry>();
                        Containers.Add(container);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not load data, creating default container. Reason: {ex.Message}");
            }
            Containers.Clear();
            Containers.Add(new AppContainer { Name = "Default" });
        }

        private void SaveData()
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                var containersToSave = Containers.Where(c => !c.IsAddButton).ToList();
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(containersToSave, jsonOptions);
                File.WriteAllText(SaveFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private void GridView_DragOver(object sender, DragEventArgs e)
        {
            var grid = sender as FrameworkElement;
            if (grid == null) { e.AcceptedOperation = DataPackageOperation.None; e.Handled = true; return; }
            var target = GetContainerFromDrop(grid, e);
            if (target == null || target.IsAddButton) { e.AcceptedOperation = DataPackageOperation.None; }
            else { e.AcceptedOperation = DataPackageOperation.Copy; }
            e.Handled = true;
        }

        private async void GridView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var grid = sender as FrameworkElement;
                if (grid == null) return;
                var container = GetContainerFromDrop(grid, e);
                if (container == null || container.IsAddButton) { e.AcceptedOperation = DataPackageOperation.None; e.Handled = true; return; }

                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        // Check if an entry with the same path already exists (case-insensitive)
                        if (container.Apps.Any(app => app.FilePath.Equals(file.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue; // Skip if it already exists
                        }

                        string? iconPath = null;
                        try
                        {
                            var thumbnail = await file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, 48);
                            if (thumbnail != null)
                            {
                                using (thumbnail)
                                {
                                    byte[] iconBytes = new byte[thumbnail.Size];
                                    await thumbnail.ReadAsync(iconBytes.AsBuffer(), (uint)thumbnail.Size, InputStreamOptions.None);

                                    Directory.CreateDirectory(IconCacheDir);
                                    string iconFileName = Guid.NewGuid().ToString() + ".png";
                                    string newIconPath = Path.Combine(IconCacheDir, iconFileName);
                                    await File.WriteAllBytesAsync(newIconPath, iconBytes);
                                    iconPath = newIconPath;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Could not get thumbnail for {file.Name}. Error: {ex.Message}");
                        }

                        var entry = new AppEntry
                        {
                            DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                            FilePath = file.Path,
                            IconPath = iconPath
                        };
                        container.Apps.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Drop operation failed: {ex.Message}");
            }
            finally
            {
                e.Handled = true;
            }
        }

        private AppContainer? GetContainerFromDrop(FrameworkElement root, DragEventArgs e)
        {
            var pt = e.GetPosition(root);
            IEnumerable<DependencyObject> hits = VisualTreeHelper.FindElementsInHostCoordinates(pt, root);
            foreach (var hit in hits)
            {
                var current = hit;
                while (current != null)
                {
                    if (current is FrameworkElement fe && fe.DataContext is AppContainer ac) return ac;
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            return null;
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppContainer container)
            {
                if (container.IsAddButton)
                {
                    var addIndex = Containers.IndexOf(container);
                    var newContainer = new AppContainer { Name = $"Container {Containers.Count}" };
                    Containers.Insert(addIndex, newContainer);
                    return;
                }
                foreach (var app in container.Apps.ToList())
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(app.FilePath))
                        {
                            Process.Start(new ProcessStartInfo { FileName = app.FilePath, UseShellExecute = true });
                        }
                    }
                    catch { /* TODO: show UI error */ }
                }
            }
        }
    }

    public class ContainerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalTemplate { get; set; }
        public DataTemplate AddTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item) { if (item is AppContainer ac && ac.IsAddButton) return AddTemplate; return NormalTemplate; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) { return SelectTemplateCore(item); }
    }
}
