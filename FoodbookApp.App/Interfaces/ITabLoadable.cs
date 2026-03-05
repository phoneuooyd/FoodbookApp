namespace FoodbookApp.Interfaces;

/// <summary>
/// Interface for pages/ViewModels that can be loaded when their tab is activated
/// in the TabBarComponent. Replaces reflection-based load method discovery.
/// </summary>
public interface ITabLoadable
{
    /// <summary>
    /// Called when the tab hosting this page is activated (selected).
    /// Implementations should load/refresh their data here.
    /// </summary>
    Task OnTabActivatedAsync();
}
