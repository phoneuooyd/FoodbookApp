using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FoodbookApp.Interfaces;
using Foodbook.Models;

namespace Foodbook.ViewModels;

public class ManageLabelsViewModel : INotifyPropertyChanged
{
    private readonly IRecipeLabelService _labelService;

    private string _newLabelName = string.Empty;
    private string _newLabelColorHex = "#8B72FF";
    private string _validationMessage = string.Empty;
    private bool _isAddFormVisible;
    private Guid? _editingLabelId;

    private string _editingName = string.Empty;
    private string _editingColorHex = string.Empty;
    private string _editingValidationMessage = string.Empty;

    private readonly HashSet<Guid> _selectedLabelIds = new();

    public ManageLabelsViewModel(IRecipeLabelService labelService)
    {
        _labelService = labelService ?? throw new ArgumentNullException(nameof(labelService));

        Labels = new ObservableCollection<RecipeLabel>();
        SelectedLabels = new ObservableCollection<RecipeLabel>();

        ShowAddFormCommand = new Command(ShowAddForm);
        CancelAddFormCommand = new Command(CancelAddForm);
        SaveNewLabelCommand = new Command(async () => await SaveNewLabelAsync(), CanSaveNewLabel);
        EditLabelCommand = new Command<RecipeLabel>(StartEditLabel);
        CancelEditCommand = new Command(CancelEditLabel);
        SaveEditCommand = new Command(async () => await SaveEditAsync());
        DeleteLabelCommand = new Command<RecipeLabel>(async label => await DeleteLabelAsync(label));
        SetColorCommand = new Command<string>(SetColor);
        SetEditColorCommand = new Command<string>(SetEditColor);
        CloseCommand = new Command(async () => await Shell.Current.Navigation.PopModalAsync());
        ToggleLabelSelectionCommand = new Command<RecipeLabel>(ToggleLabelSelection);
    }

    public ObservableCollection<RecipeLabel> Labels { get; }
    public ObservableCollection<RecipeLabel> SelectedLabels { get; }
    public IEnumerable<Guid> SelectedLabelIds => _selectedLabelIds;

    public bool IsEmpty => !Labels.Any();
    public bool HasLabels => Labels.Any();

    // ─── ADD FORM ────────────────────────────────────────────────────────────
    public bool IsAddFormVisible
    {
        get => _isAddFormVisible;
        private set { _isAddFormVisible = value; OnPropertyChanged(); }
    }

    public string NewLabelName
    {
        get => _newLabelName;
        set { _newLabelName = value; OnPropertyChanged(); ValidateNewLabel(); ((Command)SaveNewLabelCommand).ChangeCanExecute(); }
    }

    public string NewLabelColorHex
    {
        get => _newLabelColorHex;
        set { _newLabelColorHex = value; OnPropertyChanged(); OnPropertyChanged(nameof(NewLabelColorPreview)); ValidateNewLabel(); ((Command)SaveNewLabelCommand).ChangeCanExecute(); }
    }

    public Color NewLabelColorPreview
    {
        get { try { return Color.FromArgb(_newLabelColorHex); } catch { return Colors.Gray; } }
    }

    public string NewLabelValidationMessage
    {
        get => _validationMessage;
        private set { _validationMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNewValidationError)); }
    }

    public bool HasNewValidationError => !string.IsNullOrEmpty(_validationMessage);

    // ─── EDITING ─────────────────────────────────────────────────────────────
    public Guid? EditingLabelId
    {
        get => _editingLabelId;
        private set { _editingLabelId = value; OnPropertyChanged(); }
    }

    public string EditingName
    {
        get => _editingName;
        set { _editingName = value; OnPropertyChanged(); ValidateEditLabel(); }
    }

    public string EditingColorHex
    {
        get => _editingColorHex;
        set { _editingColorHex = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditingColorPreview)); ValidateEditLabel(); }
    }

    public Color EditingColorPreview
    {
        get { try { return Color.FromArgb(_editingColorHex); } catch { return Colors.Gray; } }
    }

    public string EditingValidationMessage
    {
        get => _editingValidationMessage;
        private set { _editingValidationMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEditValidationError)); }
    }

    public bool HasEditValidationError => !string.IsNullOrEmpty(_editingValidationMessage);

    public ICommand ShowAddFormCommand { get; }
    public ICommand CancelAddFormCommand { get; }
    public ICommand SaveNewLabelCommand { get; }
    public ICommand EditLabelCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand DeleteLabelCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand SetEditColorCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ToggleLabelSelectionCommand { get; }

    public async Task LoadAsync()
    {
        var labels = await _labelService.GetAllAsync();
        Labels.Clear();
        foreach (var label in labels)
        {
            label.IsSelected = _selectedLabelIds.Contains(label.Id);
            label.IsEditing = false;
            Labels.Add(label);
        }

        RestoreSelectedLabels();
        RefreshEmptyState();
    }

    public void SetSelectedLabelIds(IEnumerable<Guid> selectedLabelIds)
    {
        _selectedLabelIds.Clear();
        if (selectedLabelIds != null)
        {
            foreach (var id in selectedLabelIds)
                _selectedLabelIds.Add(id);
        }

        foreach (var label in Labels)
            label.IsSelected = _selectedLabelIds.Contains(label.Id);

        RestoreSelectedLabels();
    }

    // ─── ADD LOGIC ────────────────────────────────────────────────────────────
    private void ShowAddForm()
    {
        NewLabelName = string.Empty;
        NewLabelColorHex = "#8B72FF";
        NewLabelValidationMessage = string.Empty;
        IsAddFormVisible = true;
    }

    private void CancelAddForm()
    {
        IsAddFormVisible = false;
        NewLabelValidationMessage = string.Empty;
    }

    private async Task SaveNewLabelAsync()
    {
        if (!ValidateNewLabel()) return;

        var newLabel = new RecipeLabel
        {
            Name = NewLabelName.Trim(),
            ColorHex = NewLabelColorHex.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await _labelService.AddAsync(newLabel);
        Labels.Add(newLabel);

        CancelAddForm();
        RefreshEmptyState();
    }

    private bool ValidateNewLabel()
    {
        if (string.IsNullOrWhiteSpace(NewLabelName))
        {
            NewLabelValidationMessage = "Nazwa etykiety jest wymagana.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(NewLabelColorHex))
        {
            NewLabelValidationMessage = "Kolor jest wymagany.";
            return false;
        }
        try { Color.FromArgb(NewLabelColorHex.Trim()); }
        catch { NewLabelValidationMessage = "Nieprawidłowy format koloru."; return false; }

        NewLabelValidationMessage = string.Empty;
        return true;
    }

    private bool CanSaveNewLabel() =>
        !string.IsNullOrWhiteSpace(NewLabelName) && !string.IsNullOrWhiteSpace(NewLabelColorHex);

    // ─── EDIT LOGIC ───────────────────────────────────────────────────────────
    private void StartEditLabel(RecipeLabel label)
    {
        if (label == null) return;

        // Wyłącz edycję na wszystkich, włącz na tym
        foreach (var l in Labels) l.IsEditing = false;
        label.IsEditing = true;

        EditingLabelId = label.Id;
        EditingName = label.Name;
        EditingColorHex = label.ColorHex ?? "#8B72FF";
        EditingValidationMessage = string.Empty;
    }

    private void CancelEditLabel()
    {
        if (EditingLabelId != null)
        {
            var label = Labels.FirstOrDefault(l => l.Id == EditingLabelId);
            if (label != null) label.IsEditing = false;
        }

        EditingLabelId = null;
        EditingName = string.Empty;
        EditingColorHex = string.Empty;
        EditingValidationMessage = string.Empty;
    }

    private async Task SaveEditAsync()
    {
        if (!ValidateEditLabel()) return;

        var label = Labels.FirstOrDefault(l => l.Id == EditingLabelId);
        if (label == null) return;

        label.Name = EditingName.Trim();
        label.ColorHex = EditingColorHex.Trim();
        label.UpdatedAt = DateTime.UtcNow;
        label.IsEditing = false;

        await _labelService.UpdateAsync(label);

        var index = Labels.IndexOf(label);
        if (index >= 0)
        {
            Labels.RemoveAt(index);
            Labels.Insert(index, label);
        }

        EditingLabelId = null;
    }

    private bool ValidateEditLabel()
    {
        if (string.IsNullOrWhiteSpace(EditingName))
        {
            EditingValidationMessage = "Nazwa jest wymagana.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(EditingColorHex))
        {
            EditingValidationMessage = "Kolor jest wymagany.";
            return false;
        }
        try { Color.FromArgb(EditingColorHex.Trim()); }
        catch { EditingValidationMessage = "Nieprawidłowy format koloru."; return false; }

        EditingValidationMessage = string.Empty;
        return true;
    }

    private void SetColor(string hex) => NewLabelColorHex = hex;
    private void SetEditColor(string hex) => EditingColorHex = hex;

    // ─── DELETE ──────────────────────────────────────────────────────────────
    private async Task DeleteLabelAsync(RecipeLabel label)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "Usuń etykietę",
            $"Czy na pewno chcesz usunąć etykietę \"{label.Name}\"?",
            "Usuń",
            "Anuluj");

        if (!confirm) return;

        await _labelService.DeleteAsync(label.Id);
        Labels.Remove(label);
        _selectedLabelIds.Remove(label.Id);
        SelectedLabels.Remove(label);

        if (EditingLabelId == label.Id)
            CancelEditLabel();

        RefreshEmptyState();
    }

    // ─── SELECTION ───────────────────────────────────────────────────────────
    private void ToggleLabelSelection(RecipeLabel label)
    {
        if (label == null) return;
        if (_selectedLabelIds.Contains(label.Id))
        {
            _selectedLabelIds.Remove(label.Id);
            label.IsSelected = false;
            SelectedLabels.Remove(label);
        }
        else
        {
            _selectedLabelIds.Add(label.Id);
            label.IsSelected = true;
            if (!SelectedLabels.Contains(label)) SelectedLabels.Add(label);
        }
    }

    private void RestoreSelectedLabels()
    {
        SelectedLabels.Clear();
        foreach (var label in Labels.Where(l => _selectedLabelIds.Contains(l.Id)))
            SelectedLabels.Add(label);
    }

    private void RefreshEmptyState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasLabels));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}