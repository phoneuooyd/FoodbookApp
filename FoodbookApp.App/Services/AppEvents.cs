namespace Foodbook.Services;

/// <summary>
/// Static class for application-wide events
/// </summary>
public static class AppEvents
{
    /// <summary>
    /// Event triggered when a plan is changed (created, updated, archived, or meals modified)
    /// </summary>
    public static event Func<Task>? PlanChangedAsync;

    /// <summary>
    /// Raises the PlanChangedAsync event
    /// </summary>
    public static async Task RaisePlanChangedAsync()
    {
        if (PlanChangedAsync != null)
        {
            try
            {
                var handlers = PlanChangedAsync.GetInvocationList().Cast<Func<Task>>();
                var tasks = handlers.Select(handler => handler.Invoke());
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaisePlanChangedAsync: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Synchronous version of RaisePlanChangedAsync - fires and forgets the async handlers
    /// </summary>
    public static void RaisePlanChanged()
    {
        // Fire and forget the async event
        _ = RaisePlanChangedAsync();
    }

    /// <summary>
    /// Event triggered when the ingredients catalog changes (add/update/delete)
    /// </summary>
    public static event Func<Task>? IngredientsChangedAsync;

    /// <summary>
    /// Raises the IngredientsChangedAsync event
    /// </summary>
    public static async Task RaiseIngredientsChangedAsync()
    {
        if (IngredientsChangedAsync != null)
        {
            try
            {
                var handlers = IngredientsChangedAsync.GetInvocationList().Cast<Func<Task>>();
                var tasks = handlers.Select(handler => handler.Invoke());
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaiseIngredientsChangedAsync: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Fire-and-forget version of RaiseIngredientsChangedAsync
    /// </summary>
    public static void RaiseIngredientsChanged()
    {
        _ = RaiseIngredientsChangedAsync();
    }

    /// <summary>
    /// Synchronous version of RaiseIngredientsChangedAsync - fires and waits for the async handlers
    /// </summary>
    public static void RaiseIngredientsChangedSync()
    {
        if (IngredientsChangedAsync != null)
        {
            try
            {
                var handlers = IngredientsChangedAsync.GetInvocationList().Cast<Func<Task>>();
                var tasks = handlers.Select(handler => handler.Invoke());
                Task.WhenAll(tasks).Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaiseIngredientsChangedSync: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Simple synchronous event raised when a single ingredient is saved. Parameter: ingredient id.
    /// </summary>
    public static event Action<int>? IngredientSaved;

    /// <summary>
    /// Raise IngredientSaved synchronously
    /// </summary>
    public static void RaiseIngredientSaved(int id)
    {
        try
        {
            IngredientSaved?.Invoke(id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaiseIngredientSaved: {ex.Message}");
        }
    }
}