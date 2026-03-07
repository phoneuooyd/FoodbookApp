using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class FoodbookTemplateFormViewModel : INotifyPropertyChanged
{
    private readonly IFoodbookTemplateService _templateService;
    private readonly FoodbookTemplate? _editingTemplate;

    private Guid _sourcePlanId;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }

    public string SourcePlanId
    {
        get => _sourcePlanId == Guid.Empty ? string.Empty : _sourcePlanId.ToString();
        set
        {
            if (Guid.TryParse(value, out var parsed))
            {
                _sourcePlanId = parsed;
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public FoodbookTemplateFormViewModel(IFoodbookTemplateService templateService)
    {
        _templateService = templateService;
        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.Navigation.PopAsync());
    }

    public FoodbookTemplateFormViewModel(IFoodbookTemplateService templateService, FoodbookTemplate template)
        : this(templateService)
    {
        _editingTemplate = template;
        Name = template.Name;
        Description = template.Description;
        IsPublic = template.IsPublic;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Foodbook", "Name is required", "OK");
            return;
        }

        if (_editingTemplate is null)
        {
            if (_sourcePlanId == Guid.Empty)
            {
                await Shell.Current.DisplayAlert("Foodbook", "Missing source plan", "OK");
                return;
            }

            await _templateService.SaveTemplateFromPlanAsync(_sourcePlanId, Name, Description, IsPublic);
        }
        else
        {
            _editingTemplate.Name = Name;
            _editingTemplate.Description = Description;
            _editingTemplate.IsPublic = IsPublic;
            await _templateService.UpdateTemplateAsync(_editingTemplate);
        }

        await Shell.Current.Navigation.PopAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
