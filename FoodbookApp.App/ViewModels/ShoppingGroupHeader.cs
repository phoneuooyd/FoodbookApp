using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace Foodbook.ViewModels
{
    /// <summary>
    /// Represents a group header in the shopping list (e.g., "To Buy" or "Collected").
    /// Used in Sharpnado CollectionView as a flat list item with different DataTemplate.
    /// </summary>
    public class ShoppingGroupHeader : IShoppingListItem, INotifyPropertyChanged
    {
        public bool IsHeader => true;

        private string _groupName = string.Empty;
        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isCheckedGroup;
        /// <summary>
        /// True if this header is for the "Checked/Collected" section.
        /// </summary>
        public bool IsCheckedGroup
        {
            get => _isCheckedGroup;
            set
            {
                if (_isCheckedGroup != value)
                {
                    _isCheckedGroup = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GroupHeaderColor));
                }
            }
        }

        private bool _isVisible = true;
        /// <summary>
        /// Controls visibility of this header (hide when section is empty).
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Header color: Primary for unchecked section, gray for checked section.
        /// </summary>
        public Color GroupHeaderColor
        {
            get
            {
                if (IsCheckedGroup)
                {
                    return Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark
                        ? Color.FromArgb("#9E9E9E")  // Gray400 dark
                        : Color.FromArgb("#757575"); // Gray500 light
                }
                return (Color)(Application.Current?.Resources["Primary"] ?? Colors.Blue);
            }
        }

        public ShoppingGroupHeader(string groupName, bool isCheckedGroup)
        {
            GroupName = groupName;
            IsCheckedGroup = isCheckedGroup;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
