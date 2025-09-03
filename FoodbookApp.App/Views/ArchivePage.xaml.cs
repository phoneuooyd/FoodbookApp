using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

public partial class ArchivePage : ContentPage
{
    private readonly ArchiveViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;

    public ArchivePage(ArchiveViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
        await _viewModel.LoadArchivedPlansAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
    }
}