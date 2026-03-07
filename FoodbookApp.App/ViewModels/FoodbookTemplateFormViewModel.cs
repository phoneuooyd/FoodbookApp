using System.Collections.ObjectModel;
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
    private readonly IAccountService _accountService;

    private Guid? _templateId;
    private string _name = string.Empty;
    private string? _description;
    private bool _isPublic;

    public ObservableCollection<TemplateMeal> DraftMeals { get; } = new();

    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; OnPropertyChanged(); }
    }

    public string? Description
    {
        get => _description;
        set { if (_description == value) return; _description = value; OnPropertyChanged(); }
    }

    public bool IsPublic
    {
        get => _isPublic;
        set { if (_isPublic == value) return; _isPublic = value; OnPropertyChanged(); }
    }

    public ICommand SaveCommand { get; }

    public FoodbookTemplateFormViewModel(IFoodbookTemplateService templateService, IAccountService accountService)
    {
        _templateService = templateService;
        _accountService = accountService;
        SaveCommand = new Command(async () => await SaveAsync());
    }

    public async Task InitializeForCreateAsync(string? suggestedName, string? suggestedDescription, IReadOnlyCollection<TemplateMeal> meals, int mealsPerDay)
    {
        _templateId = null;
        Name = suggestedName ?? string.Empty;
        Description = suggestedDescription;
        DraftMeals.Clear();
        foreach (var meal in meals)
        {
            DraftMeals.Add(meal);
        }

        _pendingMealsPerDay = mealsPerDay;
        OnPropertyChanged(nameof(DraftMeals));
        await Task.CompletedTask;
    }

    public async Task InitializeForEditAsync(Guid templateId)
    {
        _templateId = templateId;
        var template = await _templateService.GetTemplateWithMealsAsync(templateId);
        if (template == null)
        {
            return;
        }

        Name = template.Name;
        Description = template.Description;
        IsPublic = template.IsPublic;
        _pendingMealsPerDay = template.MealsPerDay;

        DraftMeals.Clear();
        foreach (var meal in template.Meals.OrderBy(m => m.DayOffset).ThenBy(m => m.SlotIndex))
        {
            DraftMeals.Add(new TemplateMeal
            {
                Id = meal.Id,
                DayOffset = meal.DayOffset,
                SlotIndex = meal.SlotIndex,
                RecipeId = meal.RecipeId,
                Portions = meal.Portions
            });
        }
    }

    private int _pendingMealsPerDay = 3;

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Walidacja", "Nazwa szablonu jest wymagana.", "OK");
            return;
        }

        if (_templateId.HasValue)
        {
            await _templateService.UpdateTemplateMetadataAsync(_templateId.Value, Name.Trim(), Description, IsPublic);
            await Shell.Current.DisplayAlert("Zapisano", "Zaktualizowano szablon.", "OK");
            await Shell.Current.Navigation.PopAsync();
            return;
        }

        if (DraftMeals.Count == 0)
        {
            await Shell.Current.DisplayAlert("Brak danych", "Nie znaleziono posiłków do zapisania jako szablon.", "OK");
            return;
        }

        var userId = (await _accountService.GetActiveAccountAsync())?.SupabaseUserId ?? "local-user";
        await _templateService.CreateTemplateFromPlanAsync(
            userId,
            Name.Trim(),
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DateTime.Today,
            _pendingMealsPerDay,
            DraftMeals.ToList(),
            IsPublic);

        await Shell.Current.DisplayAlert("Zapisano", "Utworzono nowy szablon.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
