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
}