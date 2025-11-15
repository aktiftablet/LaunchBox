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
using System.Numerics;
using Microsoft.UI;
using Windows.System;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace LaunchBox
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<AppContainer> Containers { get; } = new ObservableCollection<AppContainer>();

        private static readonly string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string SaveDir = Path.Combine(AppDataPath, "LaunchBox");
        private static readonly string IconCacheDir = Path.Combine(SaveDir, "Icons");
        private static readonly string SaveFile = Path.Combine(SaveDir, "data.json");

        // --- Fields for Delete/Edit Mode (no UI changes) ---
        private AppContainer? _containerInEditMode = null;
        private FrameworkElement? _activeContainerUi = null;

        // Win32 interop
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public MainWindow()
        {
            InitializeComponent();

            this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            // Load saved data (containers + window bounds)
            LoadData();

            // Restore window bounds if present inside LoadData (LoadData calls RestoreWindowBounds when available)

            Containers.Add(new AppContainer { IsAddButton = true });
            this.Closed += (sender, args) =>
            {
                ExitDeleteMode(); // Ensure state is saved on close
                SaveData();
            };
            ValidateEntriesAsync();

            // --- Event handlers for exiting delete mode ---
            if (this.Content != null)
            {
                this.Content.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Window_PointerPressed), true);
                this.Content.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Window_KeyDown), true);
            }
        }

        private WindowBounds? GetCurrentWindowBounds()
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                if (GetWindowRect(hwnd, out RECT rc))
                {
                    int width = Math.Max(0, rc.Right - rc.Left);
                    int height = Math.Max(0, rc.Bottom - rc.Top);
                    return new WindowBounds { Left = rc.Left, Top = rc.Top, Width = width, Height = height };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetCurrentWindowBounds failed: {ex.Message}");
            }
            return null;
        }

        private void RestoreWindowBounds(WindowBounds? bounds)
        {
            if (bounds == null) return;
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                // Use SetWindowPos to position and size the window
                SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestoreWindowBounds failed: {ex.Message}");
            }
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

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Try wrapper format first (containers + window bounds)
                var persisted = JsonSerializer.Deserialize<PersistedState?>(json, options);
                if (persisted != null && persisted.Containers != null && persisted.Containers.Any())
                {
                    Containers.Clear();
                    foreach (var container in persisted.Containers)
                    {
                        if (container.Apps == null) container.Apps = new ObservableCollection<AppEntry>();
                        Containers.Add(container);
                    }

                    // Restore window bounds if present
                    if (persisted.WindowBounds != null)
                    {
                        // Restore after window created
                        RestoreWindowBounds(persisted.WindowBounds);
                    }
                    return;
                }

                // Fallback: legacy format (just list of containers)
                var loadedContainers = JsonSerializer.Deserialize<List<AppContainer>>(json, options);
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

                // Filter out the "Add" button before saving
                var containersToSave = Containers.Where(c => !c.IsAddButton).ToList();

                var state = new PersistedState
                {
                    Containers = containersToSave,
                    WindowBounds = GetCurrentWindowBounds()
                };

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(state, jsonOptions);

                File.WriteAllText(SaveFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        // GridView seviyesinde DragOver: hangi container hedef bunu kontrol et, AddButton ise reddet
        private void GridView_DragOver(object sender, DragEventArgs e)
        {
            var grid = sender as FrameworkElement;
            if (grid == null)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                e.Handled = true;
                return;
            }

            var target = GetContainerFromDrop(grid, e);
            if (target == null || target.IsAddButton)
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }

            e.Handled = true;
        }

        // GridView seviyesinde Drop: hit-test ile hedef container'ı al, dosyaları ekle
        private async void GridView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var grid = sender as FrameworkElement;
                if (grid == null) return;

                var container = GetContainerFromDrop(grid, e);
                if (container == null || container.IsAddButton)
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                    e.Handled = true;
                    return;
                }

                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
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

                        // Avoid duplicates
                        if (container.Apps.Any(app => app.FilePath.Equals(file.Path, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        var entry = new AppEntry
                        {
                            DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                            FilePath = file.Path,
                            IconPath = iconPath
                        };

                        // ensure new entry reflects current container edit state
                        entry.IsInEditMode = container.IsInEditMode;

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

        // Yardımcı: drop noktasındaki elementlerden AppContainer DataContext'ini bul
        private AppContainer? GetContainerFromDrop(FrameworkElement root, DragEventArgs e)
        {
            // Point relative to root
            var pt = e.GetPosition(root);

            // FindElementsInHostCoordinates returns elements under the point
            IEnumerable<DependencyObject> hits = VisualTreeHelper.FindElementsInHostCoordinates(pt, root);
            foreach (var hit in hits)
            {
                // try climb up to find a DataContext that is AppContainer
                var current = hit;
                while (current != null)
                {
                    if (current is FrameworkElement fe && fe.DataContext is AppContainer ac)
                        return ac;

                    current = VisualTreeHelper.GetParent(current);
                }
            }

            return null;
        }

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppContainer container)
            {
                if (_containerInEditMode != null && _containerInEditMode != container)
                {
                    ExitDeleteMode();
                    return;
                }

                if (_containerInEditMode != null && _containerInEditMode == container)
                {
                    return;
                }

                if (container.IsAddButton)
                {
                    var addIndex = Containers.IndexOf(container);
                    var newContainer = new AppContainer { Name = $"Container {Containers.Count}" };
                    Containers.Insert(addIndex, newContainer);
                    return;
                }

                // If not in edit mode, launch all apps in the container
                if (!container.IsInEditMode)
                {
                    foreach (var appEntry in container.Apps)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(appEntry.FilePath))
                            {
                                Process.Start(new ProcessStartInfo { FileName = appEntry.FilePath, UseShellExecute = true });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to launch app {appEntry.DisplayName}: {ex.Message}");
                        }
                    }
                    // e.Handled = true; // Removed as ItemClickEventArgs does not have a Handled property
                }
            }
        }

        private void App_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AppEntry appEntry)
            {
                // Find the AppContainer that owns this AppEntry by climbing the visual tree.
                DependencyObject? current = element;
                AppContainer? parentContainer = null;
                while (current != null)
                {
                    if (current is FrameworkElement fe && fe.DataContext is AppContainer ac)
                    {
                        parentContainer = ac;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // If the parent container is in edit mode, remove the clicked app from that container.
                if (parentContainer != null && parentContainer.IsInEditMode)
                {
                    if (parentContainer.Apps.Contains(appEntry))
                    {
                        parentContainer.Apps.Remove(appEntry);
                        e.Handled = true;
                        return;
                    }
                }

                // If not in edit mode, do nothing here. The GridView_ItemClick will handle launching all apps.
                e.Handled = true;
            }
        }

        private void GridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // kept for backward compatibility; prefer per-template Container_RightTapped.
            if (_containerInEditMode != null)
            {
                ExitDeleteMode();
                e.Handled = true;
                return;
            }

            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null && !(current is GridViewItem))
            {
                current = VisualTreeHelper.GetParent(current);
            }

            if (current is GridViewItem gvi && gvi.DataContext is AppContainer container && !container.IsAddButton)
            {
                EnterDeleteMode(container, gvi as FrameworkElement);
                e.Handled = true;
            }
        }

        private void EnterDeleteMode(AppContainer container, FrameworkElement element)
        {
            if (container == null || element == null) return;

            // Exit previous
            if (_containerInEditMode != null && _containerInEditMode != container)
            {
                _containerInEditMode.IsInEditMode = false;
            }

            _containerInEditMode = container;
            _activeContainerUi = element;

            _containerInEditMode.IsInEditMode = true;
        }

        private void ExitDeleteMode()
        {
            if (_containerInEditMode != null)
            {
                _containerInEditMode.IsInEditMode = false;
            }

            _containerInEditMode = null;
            _activeContainerUi = null;
            SaveData();
        }

        private void Window_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_containerInEditMode != null)
            {
                var uiElement = this.Content as UIElement;
                if (uiElement != null)
                {
                    var point = e.GetCurrentPoint(uiElement);
                    var hitTestResult = VisualTreeHelper.FindElementsInHostCoordinates(point.Position, uiElement);

                    bool clickedInsideActiveContainer = hitTestResult.Any(element =>
                    {
                        var current = element as DependencyObject;
                        while (current != null)
                        {
                            if (current == _activeContainerUi) return true;
                            current = VisualTreeHelper.GetParent(current);
                        }
                        return false;
                    });

                    if (!clickedInsideActiveContainer)
                    {
                        ExitDeleteMode();
                    }
                }
            }
        }

        private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && _containerInEditMode != null)
            {
                ExitDeleteMode();
                e.Handled = true;
            }
        }

        public static T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (child as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }
                else
                {
                    T? childOfChild = FindVisualChild<T>(child, childName);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }   

        private void Container_RightTapped(object? sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppContainer container && !container.IsAddButton)
            {
                EnterDeleteMode(container, fe);
                e.Handled = true;
            }
        }
    }

    // Persisted types
    internal class PersistedState
    {
        public List<AppContainer>? Containers { get; set; }
        public WindowBounds? WindowBounds { get; set; }
    }

    internal class WindowBounds
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class ContainerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NormalTemplate { get; set; }
        public DataTemplate? AddTemplate { get; set; }
        protected override DataTemplate? SelectTemplateCore(object item) { if (item is AppContainer ac && ac.IsAddButton) return AddTemplate; return NormalTemplate; }
        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) { return SelectTemplateCore(item); }
    }
}
