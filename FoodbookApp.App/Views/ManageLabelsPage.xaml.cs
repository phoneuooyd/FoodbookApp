using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class ManageLabelsPage : ContentPage
{
    private readonly ManageLabelsViewModel _viewModel;

    public ManageLabelsPage(ManageLabelsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    /// <summary>
    /// Pozwala przekazaæ pocz¹tkowe ID zaznaczonych etykiet (opcjonalnie).
    /// </summary>
    public void SetInitialSelection(IEnumerable<Guid> selectedIds)
    {
        _viewModel.SetSelectedLabelIds(selectedIds);
    }
}