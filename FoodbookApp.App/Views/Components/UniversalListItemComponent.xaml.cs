using System.Windows.Input;
using Foodbook.Views.Base;
using Microsoft.Maui.Controls;

namespace Foodbook.Views.Components
{
    public record DragDropInfo(object? Source, object? Target);

    public partial class UniversalListItemComponent : ContentView
    {
        public static readonly BindableProperty EditCommandProperty =
            BindableProperty.Create(nameof(EditCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DeleteCommandProperty =
            BindableProperty.Create(nameof(DeleteCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty ArchiveCommandProperty =
            BindableProperty.Create(nameof(ArchiveCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty RestoreCommandProperty =
            BindableProperty.Create(nameof(RestoreCommand), typeof(ICommand), typeof(UniversalListItemComponent));

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

        public static readonly BindableProperty ShowRestoreButtonProperty =
            BindableProperty.Create(nameof(ShowRestoreButton), typeof(bool), typeof(UniversalListItemComponent), false);

        // Drag & Drop support
        public static readonly BindableProperty ShowDragAndDropProperty =
            BindableProperty.Create(nameof(ShowDragAndDrop), typeof(bool), typeof(UniversalListItemComponent), false);

        public static readonly BindableProperty DragStartingCommandProperty =
            BindableProperty.Create(nameof(DragStartingCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DragOverCommandProperty =
            BindableProperty.Create(nameof(DragOverCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DragLeaveCommandProperty =
            BindableProperty.Create(nameof(DragLeaveCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty DropCommandProperty =
            BindableProperty.Create(nameof(DropCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        private readonly PageThemeHelper _themeHelper;

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

        public ICommand RestoreCommand
        {
            get => (ICommand)GetValue(RestoreCommandProperty);
            set => SetValue(RestoreCommandProperty, value);
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

        public bool ShowRestoreButton
        {
            get => (bool)GetValue(ShowRestoreButtonProperty);
            set => SetValue(ShowRestoreButtonProperty, value);
        }

        public bool ShowDragAndDrop
        {
            get => (bool)GetValue(ShowDragAndDropProperty);
            set => SetValue(ShowDragAndDropProperty, value);
        }

        public ICommand? DragStartingCommand
        {
            get => (ICommand?)GetValue(DragStartingCommandProperty);
            set => SetValue(DragStartingCommandProperty, value);
        }

        public ICommand? DragOverCommand
        {
            get => (ICommand?)GetValue(DragOverCommandProperty);
            set => SetValue(DragOverCommandProperty, value);
        }

        public ICommand? DragLeaveCommand
        {
            get => (ICommand?)GetValue(DragLeaveCommandProperty);
            set => SetValue(DragLeaveCommandProperty, value);
        }

        public ICommand? DropCommand
        {
            get => (ICommand?)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        public UniversalListItemComponent()
        {
            InitializeComponent();
            _themeHelper = new PageThemeHelper();
            
            // Initialize theme handling when component is loaded
            Loaded += OnComponentLoaded;
            Unloaded += OnComponentUnloaded;
        }

        private void OnComponentLoaded(object? sender, EventArgs e)
        {
            _themeHelper.Initialize();
        }

        private void OnComponentUnloaded(object? sender, EventArgs e)
        {
            _themeHelper.Cleanup();
        }

        // Handlers to forward gestures to bound commands
        private void OnDragStarting(object? sender, DragStartingEventArgs e)
        {
            if (!ShowDragAndDrop) { e.Cancel = true; return; }

            // Store source item in the drag data so it is available on drop
            e.Data.Properties["SourceItem"] = BindingContext;
            // RequestedOperation is not available in MAUI cross-platform DataPackage; skip

            if (DragStartingCommand?.CanExecute(BindingContext) == true)
            {
                DragStartingCommand.Execute(BindingContext);
            }
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            if (DragOverCommand?.CanExecute(BindingContext) == true)
            {
                DragOverCommand.Execute(BindingContext);
            }
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            if (DragLeaveCommand?.CanExecute(BindingContext) == true)
            {
                DragLeaveCommand.Execute(BindingContext);
            }
        }

        private void OnDrop(object? sender, DropEventArgs e)
        {
            if (!ShowDragAndDrop) return;

            e.Data.Properties.TryGetValue("SourceItem", out var source);
            var payload = new DragDropInfo(source, BindingContext);
            if (DropCommand?.CanExecute(payload) == true)
            {
                DropCommand.Execute(payload);
            }
        }
    }
}