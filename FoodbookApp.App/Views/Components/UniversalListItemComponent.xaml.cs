using System.Windows.Input;
using Foodbook.Views.Base;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Behaviors;

namespace Foodbook.Views.Components
{
    public enum DropIntent { On, Before, After }

    public record DragDropInfo(object? Source, object? Target, DropIntent Intent = DropIntent.On);

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

        // New: folder edit support
        public static readonly BindableProperty FolderEditCommandProperty =
            BindableProperty.Create(nameof(FolderEditCommand), typeof(ICommand), typeof(UniversalListItemComponent));

        public static readonly BindableProperty ShowFolderEditButtonProperty =
            BindableProperty.Create(nameof(ShowFolderEditButton), typeof(bool), typeof(UniversalListItemComponent), false);

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

        public ICommand? FolderEditCommand
        {
            get => (ICommand?)GetValue(FolderEditCommandProperty);
            set => SetValue(FolderEditCommandProperty, value);
        }

        public bool ShowFolderEditButton
        {
            get => (bool)GetValue(ShowFolderEditButtonProperty);
            set => SetValue(ShowFolderEditButtonProperty, value);
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
            _themeHelper.ThemeChanged += OnThemeChanged;
            
            // Apply initial tint color
            RefreshIconTintColors();
        }

        private void OnComponentUnloaded(object? sender, EventArgs e)
        {
            _themeHelper.ThemeChanged -= OnThemeChanged;
            _themeHelper.Cleanup();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(RefreshIconTintColors);
        }

        private void RefreshIconTintColors()
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) return;

                Color? tintColor = null;
                if (app.Resources.TryGetValue("TabBarIconTint", out var iconTintObj))
                {
                    if (iconTintObj is Color c)
                        tintColor = c;
                    else if (iconTintObj is SolidColorBrush b)
                        tintColor = b.Color;
                }

                if (tintColor == null) return;

                // Apply tint to Archive button
                ApplyTintToImageButton(ArchiveImageButton, tintColor);
                
                // Apply tint to Restore button
                ApplyTintToImageButton(RestoreImageButton, tintColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UniversalListItemComponent] RefreshIconTintColors failed: {ex.Message}");
            }
        }

        private static void ApplyTintToImageButton(ImageButton? imageButton, Color tintColor)
        {
            if (imageButton == null) return;

            try
            {
                var behavior = imageButton.Behaviors.OfType<IconTintColorBehavior>().FirstOrDefault();
                if (behavior != null)
                {
                    behavior.TintColor = tintColor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UniversalListItemComponent] ApplyTintToImageButton failed: {ex.Message}");
            }
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
            var payload = new DragDropInfo(source, BindingContext, DropIntent.On);
            if (DropCommand?.CanExecute(payload) == true)
            {
                DropCommand.Execute(payload);
            }
        }

        private void OnTopInsertDragOver(object? sender, DragEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            // Show the top indicator
            if (TopInsertZone != null) TopInsertZone.Opacity = 0.6;
        }

        private void OnTopInsertDragLeave(object? sender, DragEventArgs e)
        {
            if (TopInsertZone != null) TopInsertZone.Opacity = 0;
        }

        private void OnTopInsertDrop(object? sender, DropEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            if (TopInsertZone != null) TopInsertZone.Opacity = 0;

            e.Data.Properties.TryGetValue("SourceItem", out var source);
            var payload = new DragDropInfo(source, BindingContext, DropIntent.Before);
            if (DropCommand?.CanExecute(payload) == true)
            {
                DropCommand.Execute(payload);
            }
        }

        private void OnBottomInsertDragOver(object? sender, DragEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            if (BottomInsertZone != null) BottomInsertZone.Opacity = 0.6;
        }

        private void OnBottomInsertDragLeave(object? sender, DragEventArgs e)
        {
            if (BottomInsertZone != null) BottomInsertZone.Opacity = 0;
        }

        private void OnBottomInsertDrop(object? sender, DropEventArgs e)
        {
            if (!ShowDragAndDrop) return;
            if (BottomInsertZone != null) BottomInsertZone.Opacity = 0;

            e.Data.Properties.TryGetValue("SourceItem", out var source);
            var payload = new DragDropInfo(source, BindingContext, DropIntent.After);
            if (DropCommand?.CanExecute(payload) == true)
            {
                DropCommand.Execute(payload);
            }
        }
    }
}