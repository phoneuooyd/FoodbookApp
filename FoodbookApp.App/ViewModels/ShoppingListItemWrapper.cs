using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace Foodbook.ViewModels
{
    /// <summary>
    /// Header item for group sections ("To Buy" / "Collected")
    /// Used with SizedDataTemplate in Sharpnado CollectionView
    /// </summary>
    public class ShoppingListHeader : IShoppingListItem, INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isCheckedSection;
        private bool _isVisible = true;

        /// <summary>
        /// Indicates this is a header (true).
        /// </summary>
        public bool IsHeader => true;

        public string Title
        {
            get => _title;
            set { if (_title != value) { _title = value; OnPropertyChanged(); } }
        }

        public bool IsCheckedSection
        {
            get => _isCheckedSection;
            set { if (_isCheckedSection != value) { _isCheckedSection = value; OnPropertyChanged(); OnPropertyChanged(nameof(HeaderColor)); } }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
        }

        public Color HeaderColor
        {
            get
            {
                if (IsCheckedSection)
                {
                    return Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark
                        ? Color.FromArgb("#9E9E9E") // Gray400 dark
                        : Color.FromArgb("#757575"); // Gray500 light
                }
                return (Color)(Application.Current?.Resources["Primary"] ?? Colors.Blue);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
