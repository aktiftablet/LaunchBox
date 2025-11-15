using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LaunchBox
{
    public class AppContainer : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAddButton { get; set; }

        // Observable collection of apps
        public ObservableCollection<AppEntry> Apps { get; set; } = new ObservableCollection<AppEntry>();

        private bool _isInEditMode;
        [JsonIgnore]
        public bool IsInEditMode
        {
            get => _isInEditMode;
            set
            {
                if (_isInEditMode == value) return;
                _isInEditMode = value;
                // propagate to app entries so item template can bind directly
                foreach (var app in Apps)
                {
                    app.IsInEditMode = value;
                }
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppEntry : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? IconPath { get; set; }

        private bool _isInEditMode;
        [JsonIgnore]
        public bool IsInEditMode
        {
            get => _isInEditMode;
            set
            {
                if (_isInEditMode == value) return;
                _isInEditMode = value;
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
