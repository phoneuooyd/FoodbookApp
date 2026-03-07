using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class FoodbookTemplatesViewModel
{
    private readonly IFoodbookTemplateService _templateService;
    private readonly IAccountService _accountService;

    public ObservableCollection<FoodbookTemplate> Templates { get; } = new();

    public ICommand LoadCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ApplyCommand { get; }

    public FoodbookTemplatesViewModel(IFoodbookTemplateService templateService, IAccountService accountService)
    {
        _templateService = templateService;
        _accountService = accountService;

        LoadCommand = new Command(async () => await LoadAsync());
        CreateCommand = new Command(async () => await Shell.Current.Navigation.PushAsync(new FoodbookTemplateFormPage()));
        EditCommand = new Command<FoodbookTemplate>(async t => await EditAsync(t));
        DeleteCommand = new Command<FoodbookTemplate>(async t => await DeleteAsync(t));
        ApplyCommand = new Command<FoodbookTemplate>(async t => await ApplyAsync(t));
    }

    public async Task LoadAsync()
    {
        Templates.Clear();
        var userId = await ResolveUserIdAsync();
        var items = await _templateService.GetTemplatesAsync(userId, includePublic: true);
        foreach (var item in items)
        {
            Templates.Add(item);
        }
    }

    private async Task EditAsync(FoodbookTemplate? template)
    {
        if (template == null)
        {
            return;
        }

        await Shell.Current.Navigation.PushAsync(new FoodbookTemplateFormPage(template.Id));
    }

    private async Task DeleteAsync(FoodbookTemplate? template)
    {
        if (template == null)
        {
            return;
        }

        var confirm = await Shell.Current.DisplayAlert("Usuń szablon", $"Czy usunąć szablon '{template.Name}'?", "Usuń", "Anuluj");
        if (!confirm)
        {
            return;
        }

        await _templateService.DeleteTemplateAsync(template.Id);
        Templates.Remove(template);
    }

    private async Task ApplyAsync(FoodbookTemplate? template)
    {
        if (template == null)
        {
            return;
        }

        var dateText = await Shell.Current.DisplayPromptAsync("Data startowa", "Podaj datę startową (rrrr-MM-dd)", "Zastosuj", "Anuluj", placeholder: DateTime.Today.ToString("yyyy-MM-dd"));
        if (string.IsNullOrWhiteSpace(dateText) || !DateTime.TryParse(dateText, out var startDate))
        {
            return;
        }

        var createdPlan = await _templateService.ApplyTemplateAsync(template.Id, startDate.Date);
        if (createdPlan == null)
        {
            await Shell.Current.DisplayAlert("Błąd", "Nie udało się zastosować szablonu.", "OK");
            return;
        }

        Foodbook.Services.AppEvents.RaisePlanChanged();
        await Shell.Current.DisplayAlert("Gotowe", $"Utworzono planer '{createdPlan.Name}'.", "OK");
    }

    private async Task<string> ResolveUserIdAsync()
    {
        var account = await _accountService.GetActiveAccountAsync();
        return account?.SupabaseUserId ?? "local-user";
    }
}
