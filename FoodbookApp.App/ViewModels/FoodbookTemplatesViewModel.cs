using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class FoodbookTemplatesViewModel : INotifyPropertyChanged
{
    private readonly IFoodbookTemplateService _templateService;

    public ObservableCollection<FoodbookTemplate> Templates { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ApplyCommand { get; }

    public FoodbookTemplatesViewModel(IFoodbookTemplateService templateService)
    {
        _templateService = templateService;
        LoadCommand = new Command(async () => await LoadAsync());
        AddCommand = new Command(async () => await Shell.Current.Navigation.PushAsync(new FoodbookTemplateFormPage()));
        EditCommand = new Command<FoodbookTemplate>(async t => await EditAsync(t));
        DeleteCommand = new Command<FoodbookTemplate>(async t => await DeleteAsync(t));
        ApplyCommand = new Command<FoodbookTemplate>(async t => await ApplyAsync(t));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Templates.Clear();
            var items = await _templateService.GetTemplatesAsync();
            foreach (var item in items)
            {
                Templates.Add(item);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EditAsync(FoodbookTemplate? template)
    {
        if (template is null) return;
        var page = new FoodbookTemplateFormPage
        {
            BindingContext = new FoodbookTemplateFormViewModel(_templateService, template)
        };
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async Task DeleteAsync(FoodbookTemplate? template)
    {
        if (template is null) return;
        var ok = await Shell.Current.DisplayAlert("Foodbook", "Delete template?", "OK", "Cancel");
        if (!ok) return;

        await _templateService.RemoveTemplateAsync(template.Id);
        await LoadAsync();
    }

    private async Task ApplyAsync(FoodbookTemplate? template)
    {
        if (template is null) return;

        var result = await Shell.Current.DisplayPromptAsync("Foodbook", "Start date (yyyy-MM-dd)", initialValue: DateTime.Today.ToString("yyyy-MM-dd"));
        if (string.IsNullOrWhiteSpace(result) || !DateTime.TryParse(result, out var startDate))
            return;

        var plan = await _templateService.ApplyTemplateAsync(template.Id, startDate.Date);
        await Shell.Current.DisplayAlert("Foodbook", $"Created plan: {plan.Title}", "OK");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
