using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;
    private bool _isInitialized;

    public IngredientsPage(IngredientsViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"IngredientsPage.OnAppearing - IsInitialized: {_isInitialized}");
            
            // Upewnij się, że baza została zainicjalizowana
            await MauiProgram.EnsureDatabaseInitializedAsync();
            
            // Only load once or if explicitly needed
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("First time loading ingredients");
                await _viewModel.LoadAsync();
                _isInitialized = true;

                // Check for seeding only after initial load is complete
                System.Diagnostics.Debug.WriteLine($"Ingredients count: {_viewModel.Ingredients.Count}, IsLoading: {_viewModel.IsLoading}");
                if (_viewModel.Ingredients.Count == 0 && !_viewModel.IsLoading)
                {
                    System.Diagnostics.Debug.WriteLine("No ingredients found, showing seed dialog");
                    await HandleEmptyIngredientsAsync();
                }
            }
            else
            {
                // If we're returning to the page, just refresh if needed
                // This handles cases where ingredients might have been added/modified
                System.Diagnostics.Debug.WriteLine("Reloading ingredients on return");
                await _viewModel.ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
            await DisplayAlert("Błąd", $"Wystąpił problem podczas ładowania strony: {ex.Message}", "OK");
        }
    }

    private async Task HandleEmptyIngredientsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("HandleEmptyIngredientsAsync started");
            
            bool create = await DisplayAlert("Brak składników", 
                "Nie znaleziono żadnych składników w bazie danych.\n\nCzy chcesz utworzyć listę przykładowych składników?", 
                "Tak, utwórz", "Nie, dodaj ręcznie");
            
            System.Diagnostics.Debug.WriteLine($"User choice for seeding: {create}");
            
            if (create && MauiProgram.ServiceProvider != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Starting manual seeding process");
                    
                    using var scope = MauiProgram.ServiceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    // Sprawdź jeszcze raz czy nie ma składników (race condition)
                    var hasIngredients = await db.Ingredients.AnyAsync();
                    System.Diagnostics.Debug.WriteLine($"Double-check ingredients exist: {hasIngredients}");
                    
                    if (!hasIngredients)
                    {
                        await SeedData.SeedIngredientsAsync(db);
                        System.Diagnostics.Debug.WriteLine("Manual seeding completed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Ingredients found during double-check, skipping seed");
                    }
                    
                    // Reload the data after seeding
                    System.Diagnostics.Debug.WriteLine("Reloading view model after seeding");
                    await _viewModel.ReloadAsync();
                    
                    if (_viewModel.Ingredients.Count > 0)
                    {
                        await DisplayAlert("Sukces", 
                            $"Pomyślnie dodano {_viewModel.Ingredients.Count} przykładowych składników!", 
                            "OK");
                    }
                }
                catch (Exception seedEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during manual seeding: {seedEx.Message}");
                    await DisplayAlert("Błąd seedowania", 
                        $"Nie udało się załadować przykładowych składników: {seedEx.Message}\n\nMożesz dodać składniki ręcznie.", 
                        "OK");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("User chose not to seed or ServiceProvider is null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling empty ingredients: {ex.Message}");
            await DisplayAlert("Błąd", "Wystąpił problem podczas ładowania składników.", "OK");
        }
    }
}
