using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class FoodbookViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;
    private readonly ILocalizationService _localizationService;

    private Guid? _editingPlanId;
    private bool _isLoading;
    private int _selectedTabIndex;
    private string _name = string.Empty;
    private string _emoji = "??";
    private string _accentColor = "#5B3FE8";
    private int _durationDays = 7;
    private int _mealsPerDay = 3;
    private bool _daysLoaded;
    private List<PlannedMeal>? _initialMeals;

    public ObservableCollection<Recipe> Recipes { get; } = new();
    public ObservableCollection<PlannerDay> Days { get; } = new();

    public bool IsEditing => _editingPlanId.HasValue;
    public bool RecipesLoaded => Recipes.Count > 0;
    public string CtaButtonText => SelectedTabIndex == 0
        ? L("CtaNextToDishes", "Przejdz do Dan ?")
        : L("CtaSave", "Zapisz Foodbook");
    public ICommand CtaCommand => SelectedTabIndex == 0 ? GoToDishesCommand : SaveCommand;

    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex == value) return;
            _selectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CtaButtonText));
            OnPropertyChanged(nameof(CtaCommand));

            if (_selectedTabIndex == 1)
                _ = EnsureDaysLoadedAsync();
        }
    }

    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Emoji
    {
        get => _emoji;
        set { if (_emoji == value) return; _emoji = value; OnPropertyChanged(); }
    }

    public string AccentColor
    {
        get => _accentColor;
        set { if (_accentColor == value) return; _accentColor = value; OnPropertyChanged(); }
    }

    public int DurationDays
    {
        get => _durationDays;
        set
        {
            var clamped = Math.Clamp(value, 1, 30);
            if (_durationDays == clamped) return;
            _durationDays = clamped;
            OnPropertyChanged();
            AdjustDaysCollection();
        }
    }

    public int MealsPerDay
    {
        get => _mealsPerDay;
        set
        {
            var clamped = Math.Clamp(value, 1, 10);
            if (_mealsPerDay == clamped) return;
            _mealsPerDay = clamped;
            OnPropertyChanged();
            AdjustMealsPerDay();
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Foodbook" : Name;

    public ICommand GoToDishesCommand { get; }
    public ICommand GoToIdentityCommand { get; }
    public ICommand AddMealCommand { get; }
    public ICommand RemoveMealCommand { get; }
    public ICommand IncreasePortionsCommand { get; }
    public ICommand DecreasePortionsCommand { get; }
    public ICommand IncreaseDurationCommand { get; }
    public ICommand DecreaseDurationCommand { get; }
    public ICommand IncreaseMealsPerDayCommand { get; }
    public ICommand DecreaseMealsPerDayCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public FoodbookViewModel(IPlannerService plannerService, IRecipeService recipeService, IPlanService planService, ILocalizationService localizationService)
    {
        _plannerService = plannerService;
        _recipeService = recipeService;
        _planService = planService;
        _localizationService = localizationService;

        _localizationService.CultureChanged += OnCultureChanged;

        GoToDishesCommand = new Command(async () => await GoToDishesAsync());
        GoToIdentityCommand = new Command(() => SelectedTabIndex = 0);

        AddMealCommand = new Command<PlannerDay>(AddMeal);
        RemoveMealCommand = new Command<PlannedMeal>(RemoveMeal);
        IncreasePortionsCommand = new Command<PlannedMeal>(m => { if (m != null && m.Portions < 20) m.Portions++; });
        DecreasePortionsCommand = new Command<PlannedMeal>(m => { if (m != null && m.Portions > 1) m.Portions--; });

        IncreaseDurationCommand = new Command(() => DurationDays++);
        DecreaseDurationCommand = new Command(() => DurationDays--);
        IncreaseMealsPerDayCommand = new Command(() => MealsPerDay++);
        DecreaseMealsPerDayCommand = new Command(() => MealsPerDay--);

        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task InitializeAsync(Guid? planId = null)
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            await LoadRecipesAsync();
            ResetEditorState();

            if (planId.HasValue)
            {
                var plan = await _planService.GetPlanAsync(planId.Value);
                if (plan is { IsFoodbook: true })
                {
                    _editingPlanId = plan.Id;
                    _name = plan.Title ?? string.Empty;
                    _emoji = plan.DisplayEmoji;
                    _accentColor = plan.DisplayColor;
                    _durationDays = Math.Clamp(plan.DurationDays, 1, 30);
                    _initialMeals = await _plannerService.GetPlannedMealsAsync(plan.Id);

                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(Emoji));
                    OnPropertyChanged(nameof(AccentColor));
                    OnPropertyChanged(nameof(DurationDays));
                    OnPropertyChanged(nameof(IsEditing));
                }
            }

            if (SelectedTabIndex == 1)
                await EnsureDaysLoadedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FoodbookVM] InitializeAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadRecipesAsync()
    {
        var recipes = await _recipeService.GetRecipesAsync();
        Recipes.Clear();
        foreach (var recipe in recipes)
            Recipes.Add(recipe);

        OnPropertyChanged(nameof(RecipesLoaded));
    }

    public void Reset()
    {
        ResetEditorState();
    }

    private void ResetEditorState()
    {
        foreach (var day in Days)
        {
            foreach (var meal in day.Meals)
                meal.PropertyChanged -= OnMealRecipeChanged;
        }

        Days.Clear();
        _editingPlanId = null;
        _initialMeals = null;
        _daysLoaded = false;
        _name = string.Empty;
        _emoji = "??";
        _accentColor = "#5B3FE8";
        _durationDays = 7;
        _mealsPerDay = 3;
        _selectedTabIndex = 0;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Emoji));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(DurationDays));
        OnPropertyChanged(nameof(MealsPerDay));
        OnPropertyChanged(nameof(SelectedTabIndex));
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(CtaButtonText));
        OnPropertyChanged(nameof(CtaCommand));
    }

    private async Task GoToDishesAsync()
    {
        await EnsureDaysLoadedAsync();
        SelectedTabIndex = 1;
    }

    private Task EnsureDaysLoadedAsync()
    {
        if (_daysLoaded)
            return Task.CompletedTask;

        try
        {
            if (_initialMeals is { Count: > 0 })
                BuildDaysFromMeals(_initialMeals);
            else
                BuildEmptyDays();

            _daysLoaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FoodbookVM] EnsureDaysLoadedAsync error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void BuildEmptyDays()
    {
        Days.Clear();
        var baseDate = DateTime.Today;
        for (int i = 0; i < _durationDays; i++)
        {
            var day = new PlannerDay(baseDate.AddDays(i));
            for (int j = 0; j < _mealsPerDay; j++)
            {
                var meal = new PlannedMeal { Date = day.Date, Portions = 1 };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
            }
            Days.Add(day);
        }
    }

    private void BuildDaysFromMeals(List<PlannedMeal> meals)
    {
        Days.Clear();

        var minDate = meals.Count > 0 ? meals.Min(m => m.Date).Date : DateTime.Today;
        int maxMeals = 0;

        for (int i = 0; i < _durationDays; i++)
        {
            var date = minDate.AddDays(i);
            var day = new PlannerDay(date);
            var mealsForDay = meals.Where(m => m.Date.Date == date.Date).ToList();

            foreach (var existing in mealsForDay)
            {
                var recipe = Recipes.FirstOrDefault(r => r.Id == existing.RecipeId);
                var meal = new PlannedMeal
                {
                    Id = existing.Id,
                    RecipeId = existing.RecipeId,
                    Recipe = recipe,
                    Date = existing.Date,
                    Portions = existing.Portions,
                    PlanId = existing.PlanId
                };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
            }

            maxMeals = Math.Max(maxMeals, day.Meals.Count);
            Days.Add(day);
        }

        if (maxMeals > 0)
            _mealsPerDay = Math.Max(_mealsPerDay, maxMeals);

        OnPropertyChanged(nameof(MealsPerDay));
        AdjustMealsPerDay();
    }

    private void AdjustDaysCollection()
    {
        if (!_daysLoaded)
            return;

        var baseDate = Days.Count > 0 ? Days[0].Date : DateTime.Today;

        while (Days.Count > _durationDays)
        {
            var removed = Days[Days.Count - 1];
            foreach (var meal in removed.Meals)
                meal.PropertyChanged -= OnMealRecipeChanged;
            Days.RemoveAt(Days.Count - 1);
        }

        while (Days.Count < _durationDays)
        {
            var date = baseDate.AddDays(Days.Count);
            var day = new PlannerDay(date);
            for (int j = 0; j < _mealsPerDay; j++)
            {
                var meal = new PlannedMeal { Date = date, Portions = 1, PlanId = _editingPlanId };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
            }
            Days.Add(day);
        }
    }

    private void AdjustMealsPerDay()
    {
        if (!_daysLoaded)
            return;

        foreach (var day in Days)
        {
            while (day.Meals.Count < _mealsPerDay)
            {
                var meal = new PlannedMeal { Date = day.Date, Portions = 1, PlanId = _editingPlanId };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
            }
            while (day.Meals.Count > _mealsPerDay)
            {
                var last = day.Meals[day.Meals.Count - 1];
                last.PropertyChanged -= OnMealRecipeChanged;
                day.Meals.RemoveAt(day.Meals.Count - 1);
            }
        }
    }

    private void AddMeal(PlannerDay? day)
    {
        if (day == null) return;
        var meal = new PlannedMeal { Date = day.Date, Portions = 1, PlanId = _editingPlanId };
        meal.PropertyChanged += OnMealRecipeChanged;
        day.Meals.Add(meal);
    }

    private void RemoveMeal(PlannedMeal? meal)
    {
        if (meal == null) return;
        var day = Days.FirstOrDefault(d => d.Meals.Contains(meal));
        if (day != null)
        {
            meal.PropertyChanged -= OnMealRecipeChanged;
            day.Meals.Remove(meal);
        }
    }

    private void OnMealRecipeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannedMeal.Recipe) && sender is PlannedMeal meal && meal.Recipe != null)
            meal.Portions = meal.Recipe.IloscPorcji;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CtaButtonText));
    }

    private string L(string key, string fallback)
    {
        var value = _localizationService.GetString("FoodbookPageResources", key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                SelectedTabIndex = 0;
                await Shell.Current.DisplayAlert(
                    L("MissingNameTitle", "Brak nazwy"),
                    L("MissingNameMessage", "Podaj nazwê Foodbooka."),
                    "OK");
                return;
            }

            await EnsureDaysLoadedAsync();

            Plan plan;
            if (_editingPlanId.HasValue)
            {
                plan = await _planService.GetPlanAsync(_editingPlanId.Value) ?? new Plan();
                plan.Type = PlanType.Foodbook;
                plan.Title = Name.Trim();
                plan.Emoji = Emoji;
                plan.AccentColor = AccentColor;
                plan.DurationDays = DurationDays;
                await _planService.UpdatePlanAsync(plan);

                var existingMeals = await _plannerService.GetPlannedMealsAsync(plan.Id);
                foreach (var meal in existingMeals)
                    await _plannerService.RemovePlannedMealAsync(meal.Id);
            }
            else
            {
                plan = new Plan
                {
                    Type = PlanType.Foodbook,
                    Title = Name.Trim(),
                    Emoji = Emoji,
                    AccentColor = AccentColor,
                    DurationDays = DurationDays,
                    StartDate = Days.Count > 0 ? Days[0].Date : DateTime.Today,
                    EndDate = Days.Count > 0 ? Days[Days.Count - 1].Date : DateTime.Today.AddDays(DurationDays - 1),
                };
                await _planService.AddPlanAsync(plan);
                _editingPlanId = plan.Id;
            }

            int saved = 0;
            foreach (var day in Days)
            {
                foreach (var meal in day.Meals)
                {
                    if (meal.Recipe != null)
                        meal.RecipeId = meal.Recipe.Id;
                    if (meal.RecipeId != Guid.Empty)
                    {
                        await _plannerService.AddPlannedMealAsync(new PlannedMeal
                        {
                            RecipeId = meal.RecipeId,
                            Date = meal.Date,
                            Portions = meal.Portions,
                            PlanId = plan.Id
                        });
                        saved++;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FoodbookVM] Saved {saved} meals for foodbook {plan.Id}");

            AppEvents.RaisePlanChanged();

            var savedTemplate = L("SaveSuccessMessageFormat", "Foodbook \"{0}\" zosta³ zapisany.");
            await Shell.Current.DisplayAlert(
                L("SaveSuccessTitle", "Zapisano"),
                string.Format(savedTemplate, plan.Title),
                "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FoodbookVM] SaveAsync error: {ex.Message}");
            await Shell.Current.DisplayAlert(
                L("SaveErrorTitle", "B³¹d"),
                L("SaveErrorMessage", "Nie uda³o siê zapisaæ Foodbooka."),
                "OK");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
