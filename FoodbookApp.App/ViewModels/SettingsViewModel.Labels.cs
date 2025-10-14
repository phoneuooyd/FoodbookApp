using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace Foodbook.ViewModels;

public partial class SettingsViewModel
{
    private IRecipeLabelService? _labelsService;

    public ObservableCollection<RecipeLabel> Labels { get; } = new();

    private RecipeLabel? _selectedLabel;
    public RecipeLabel? SelectedLabel
    {
        get => _selectedLabel;
        set
        {
            if (_selectedLabel == value) return;
            _selectedLabel = value;
            OnPropertyChanged(nameof(SelectedLabel));
            EditLabelName = _selectedLabel?.Name ?? string.Empty;
            EditLabelColor = _selectedLabel?.ColorHex ?? "#FF7F50"; // default Coral
            ((Command)UpdateLabelCommand).ChangeCanExecute();
            ((Command)DeleteLabelCommand).ChangeCanExecute();
        }
    }

    public string NewLabelName { get => _newLabelName; set { _newLabelName = value; OnPropertyChanged(nameof(NewLabelName)); ((Command)AddLabelCommand).ChangeCanExecute(); } }
    private string _newLabelName = string.Empty;

    public string NewLabelColor { get => _newLabelColor; set { _newLabelColor = value; OnPropertyChanged(nameof(NewLabelColor)); } }
    private string _newLabelColor = "#FF7F50";

    public string EditLabelName { get => _editLabelName; set { _editLabelName = value; OnPropertyChanged(nameof(EditLabelName)); ((Command)UpdateLabelCommand).ChangeCanExecute(); } }
    private string _editLabelName = string.Empty;

    public string EditLabelColor { get => _editLabelColor; set { _editLabelColor = value; OnPropertyChanged(nameof(EditLabelColor)); } }
    private string _editLabelColor = "#FF7F50";

    public ICommand AddLabelCommand { get; private set; } = null!;
    public ICommand UpdateLabelCommand { get; private set; } = null!;
    public ICommand DeleteLabelCommand { get; private set; } = null!;

    partial void InitializeLabelsFeature()
    {
        try
        {
            _labelsService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService(typeof(IRecipeLabelService)) as IRecipeLabelService;
        }
        catch
        {
            _labelsService = null;
        }

        AddLabelCommand = new Command(async () => await AddLabelAsync(), CanAddLabel);
        UpdateLabelCommand = new Command(async () => await UpdateLabelAsync(), CanUpdateLabel);
        DeleteLabelCommand = new Command(async () => await DeleteLabelAsync(), CanDeleteLabel);

        // Preload labels
        _ = LoadLabelsAsync();
    }

    private async Task LoadLabelsAsync()
    {
        try
        {
            var list = _labelsService != null ? await _labelsService.GetAllAsync() : new List<RecipeLabel>();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Labels.Clear();
                foreach (var l in list)
                    Labels.Add(l);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] LoadLabels error: {ex.Message}");
        }
    }

    private bool CanAddLabel()
    {
        return !string.IsNullOrWhiteSpace(NewLabelName);
    }

    private async Task AddLabelAsync()
    {
        if (_labelsService == null) return;
        try
        {
            var created = await _labelsService.AddAsync(new RecipeLabel { Name = NewLabelName.Trim(), ColorHex = NewLabelColor });
            Labels.Add(created);
            NewLabelName = string.Empty;
            NewLabelColor = "#FF7F50";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] AddLabel error: {ex.Message}");
        }
    }

    private bool CanUpdateLabel()
    {
        return SelectedLabel != null && !string.IsNullOrWhiteSpace(EditLabelName);
    }

    private async Task UpdateLabelAsync()
    {
        if (_labelsService == null || SelectedLabel == null) return;
        try
        {
            SelectedLabel.Name = EditLabelName.Trim();
            SelectedLabel.ColorHex = EditLabelColor;
            var updated = await _labelsService.UpdateAsync(SelectedLabel);
            // Refresh list item
            var idx = Labels.IndexOf(SelectedLabel);
            if (idx >= 0)
            {
                Labels[idx] = updated;
                SelectedLabel = updated;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] UpdateLabel error: {ex.Message}");
        }
    }

    private bool CanDeleteLabel()
    {
        return SelectedLabel != null;
    }

    private async Task DeleteLabelAsync()
    {
        if (_labelsService == null || SelectedLabel == null) return;
        try
        {
            var id = SelectedLabel.Id;
            var ok = await _labelsService.DeleteAsync(id);
            if (ok)
            {
                Labels.Remove(SelectedLabel);
                SelectedLabel = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] DeleteLabel error: {ex.Message}");
        }
    }
}
