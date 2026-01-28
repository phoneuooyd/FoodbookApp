using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using System.Threading.Tasks;
using System;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel ?? throw new InvalidOperationException("ViewModel not set");
    
    // ? CRITICAL: Track whether we're awaiting load to prevent race conditions
    private Task? _loadTask;

    public IngredientFormPage(IngredientFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Constructor called");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] OnAppearing");
        
        // ? CRITICAL: Wait for any pending load operation to complete before showing the page
        if (_loadTask != null)
        {
            try
            {
                await _loadTask;
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Load task completed in OnAppearing");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error awaiting load task: {ex.Message}");
            }
        }
    }

    private Guid _itemId;

    public string? ItemId
    {
        get => _itemId == Guid.Empty ? null : _itemId.ToString();
        set
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] ItemId setter called with value: '{value}'");
            
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Valid GUID parsed for EDIT mode: {parsed}");

                // If same ID is set again (for example after returning from a popup),
                // avoid reloading the ingredient which would overwrite user changes made in-place.
                if (_itemId == parsed)
                {
                    // If there is an in-flight load that hasn't completed, we allow it to continue.
                    if (_loadTask != null && !_loadTask.IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Same ID received and load in progress - waiting for existing load");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Same ID received - skipping reload to avoid overwriting UI state");
                        return;
                    }
                }

                _itemId = parsed;
                
                // ? CRITICAL: Store the load task so OnAppearing can await it
                // This ensures the form is fully loaded before being displayed
                _loadTask = LoadIngredientAsync(parsed);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] No valid ID - NEW ingredient mode");
                _itemId = Guid.Empty;
                
                // ? CRITICAL: Reset the form for new ingredient mode
                _loadTask = Task.Run(() =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            ViewModel.Reset();
                            System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Form reset for new ingredient");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error resetting form: {ex.Message}");
                        }
                    });
                });
            }
        }
    }

    private async Task LoadIngredientAsync(Guid id)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] LoadIngredientAsync started for ID: {id}");
            await ViewModel.LoadAsync(id);
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] LoadIngredientAsync completed for ID: {id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error loading ingredient: {ex.Message}");
            
            // Show error to user on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert(
                    "B³¹d ³adowania", 
                    $"Nie uda³o siê za³adowaæ sk³adnika: {ex.Message}", 
                    "OK");
            });
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (ViewModel?.CancelCommand?.CanExecute(null) == true)
            ViewModel.CancelCommand.Execute(null);
        return true;
    }
}
