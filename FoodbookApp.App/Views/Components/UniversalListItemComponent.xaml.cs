using System.Windows.Input;

namespace Foodbook.Views.Components
{
    public partial class UniversalListItemComponent : ContentView
    {
        public static readonly BindableProperty EditCommandProperty =
            BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DeleteCommandProperty =
            BindableProperty.Create(nameof(DeleteCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty ShowSubtitleProperty =
            BindableProperty.Create(nameof(ShowSubtitle), typeof(bool), typeof(UniversalListItemComponent), false);

        public ICommand EditCommand
        {
            get => (ICommand)GetValue(EditCommandProperty);
            set => SetValue(EditCommandProperty, value);
        }

        public ICommand DeleteCommand
        {
            get => (ICommand)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        public bool ShowSubtitle
        {
            get => (bool)GetValue(ShowSubtitleProperty);
            set => SetValue(ShowSubtitleProperty, value);
        }

        public UniversalListItemComponent()
        {
            InitializeComponent();
        }
    }
}