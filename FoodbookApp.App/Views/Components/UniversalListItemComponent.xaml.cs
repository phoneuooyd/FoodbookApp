using System.Windows.Input;

namespace Foodbook.Views.Components
{
    public partial class UniversalListItemComponent : ContentView
    {
        public static readonly BindableProperty EditCommandProperty =
            BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DeleteCommandProperty =
            BindableProperty.Create(nameof(DeleteCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty ArchiveCommandProperty =
            BindableProperty.Create(nameof(ArchiveCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty ShowSubtitleProperty =
            BindableProperty.Create(nameof(ShowSubtitle), typeof(bool), typeof(UniversalListItemComponent), false);

        public static readonly BindableProperty ShowNutritionLayoutProperty =
            BindableProperty.Create(nameof(ShowNutritionLayout), typeof(bool), typeof(UniversalListItemComponent), true);

        public static readonly BindableProperty ShowPlanLayoutProperty =
            BindableProperty.Create(nameof(ShowPlanLayout), typeof(bool), typeof(UniversalListItemComponent), false);

        public static readonly BindableProperty ShowDeleteButtonProperty =
            BindableProperty.Create(nameof(ShowDeleteButton), typeof(bool), typeof(UniversalListItemComponent), true);

        public static readonly BindableProperty ShowArchiveButtonProperty =
            BindableProperty.Create(nameof(ShowArchiveButton), typeof(bool), typeof(UniversalListItemComponent), false);

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

        public ICommand ArchiveCommand
        {
            get => (ICommand)GetValue(ArchiveCommandProperty);
            set => SetValue(ArchiveCommandProperty, value);
        }

        public bool ShowSubtitle
        {
            get => (bool)GetValue(ShowSubtitleProperty);
            set => SetValue(ShowSubtitleProperty, value);
        }

        public bool ShowNutritionLayout
        {
            get => (bool)GetValue(ShowNutritionLayoutProperty);
            set => SetValue(ShowNutritionLayoutProperty, value);
        }

        public bool ShowPlanLayout
        {
            get => (bool)GetValue(ShowPlanLayoutProperty);
            set => SetValue(ShowPlanLayoutProperty, value);
        }

        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        public bool ShowArchiveButton
        {
            get => (bool)GetValue(ShowArchiveButtonProperty);
            set => SetValue(ShowArchiveButtonProperty, value);
        }

        public UniversalListItemComponent()
        {
            InitializeComponent();
        }
    }
}