using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class ArchivePage : ContentPage
{
    private readonly ArchiveViewModel _viewModel;

    public ArchivePage(ArchiveViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadArchivedPlansAsync();
    }
}