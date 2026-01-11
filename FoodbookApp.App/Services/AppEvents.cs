namespace Foodbook.Services;

/// <summary>
/// Static class for application-wide events
/// </summary>
public static class AppEvents
{
    // ? NEW: Queue for recipe-saved events that occur before subscribers are ready
    private static Queue<Guid> _pendingRecipeSavedEvents = new();
    private static object _recipeSavedQueueLock = new();

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
    public static event Action<Guid>? IngredientSaved;

    /// <summary>
    /// Raise IngredientSaved synchronously
    /// </summary>
    public static void RaiseIngredientSaved(Guid id)
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

    /// <summary>
    /// Event triggered when the recipes catalog changes (add/update/delete)
    /// </summary>
    public static event Func<Task>? RecipesChangedAsync;

    /// <summary>
    /// Raises the RecipesChangedAsync event
    /// </summary>
    public static async Task RaiseRecipesChangedAsync()
    {
        if (RecipesChangedAsync != null)
        {
            try
            {
                var handlers = RecipesChangedAsync.GetInvocationList().Cast<Func<Task>>();
                var tasks = handlers.Select(handler => handler.Invoke());
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaiseRecipesChangedAsync: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Fire-and-forget version of RaiseRecipesChangedAsync
    /// </summary>
    public static void RaiseRecipesChanged()
    {
        _ = RaiseRecipesChangedAsync();
    }

    /// <summary>
    /// Simple synchronous event raised when a single recipe is saved. Parameter: recipe id.
    /// ? If no subscribers yet, queues the event to be delivered when subscribers attach.
    /// </summary>
    public static event Action<Guid>? RecipeSaved;

    /// <summary>
    /// Raise RecipeSaved synchronously
    /// ? Queues event if no active subscribers
    /// </summary>
    public static void RaiseRecipeSaved(Guid id)
    {
        try
        {
            if (RecipeSaved == null)
            {
                // ? No subscribers yet - queue this event
                lock (_recipeSavedQueueLock)
                {
                    _pendingRecipeSavedEvents.Enqueue(id);
                    System.Diagnostics.Debug.WriteLine($"[AppEvents] RecipeSaved queued (id={id}), queue size: {_pendingRecipeSavedEvents.Count}");
                }
            }
            else
            {
                RecipeSaved?.Invoke(id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in AppEvents.RaiseRecipeSaved: {ex.Message}");
        }
    }

    /// <summary>
    /// ? NEW: Drain pending recipe-saved events to a newly subscribed handler
    /// Call this when subscribing to RecipeSaved to process any queued events
    /// </summary>
    public static void DrainPendingRecipeSavedEvents(Action<Guid> handler)
    {
        try
        {
            lock (_recipeSavedQueueLock)
            {
                while (_pendingRecipeSavedEvents.TryDequeue(out var id))
                {
                    System.Diagnostics.Debug.WriteLine($"[AppEvents] Draining pending RecipeSaved event (id={id})");
                    handler?.Invoke(id);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error draining pending RecipeSaved events: {ex.Message}");
        }
    }
}