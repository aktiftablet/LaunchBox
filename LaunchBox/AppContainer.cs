using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LaunchBox
{
    public class AppContainer
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAddButton { get; set; }
        public ObservableCollection<AppEntry> Apps { get; set; } = new ObservableCollection<AppEntry>();
        public AppContainer() { }
    }

    public class AppEntry : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        private string? _iconPath;
        public string? IconPath
        {
            get => _iconPath;
            set
            {
                _iconPath = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
