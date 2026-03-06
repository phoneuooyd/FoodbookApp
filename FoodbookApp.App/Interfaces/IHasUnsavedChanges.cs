namespace FoodbookApp.Interfaces;

/// <summary>
/// Interface for pages or ViewModels that track unsaved changes.
/// Used by TabBarComponent to prompt before navigating away.
/// Replaces reflection-based _isDirty / HasUnsavedChanges discovery.
/// </summary>
public interface IHasUnsavedChanges
{
    /// <summary>
    /// Returns true if the page/ViewModel has changes that have not been persisted yet.
    /// </summary>
    bool HasUnsavedChanges { get; }
}
