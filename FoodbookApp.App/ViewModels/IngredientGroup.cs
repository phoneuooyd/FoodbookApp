using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Foodbook.Models;

namespace Foodbook.ViewModels
{
    // UI helper group that mirrors a source ObservableCollection without creating a nested scroll container
    public class IngredientGroup : ObservableCollection<Ingredient>
    {
        public string Key { get; }
        public bool IsUnchecked { get; }

        private readonly ObservableCollection<Ingredient> _source;

        public IngredientGroup(string key, ObservableCollection<Ingredient> source, bool isUnchecked)
        {
            Key = key;
            IsUnchecked = isUnchecked;
            _source = source;

            foreach (var item in source)
                Add(item);

            _source.CollectionChanged += OnSourceCollectionChanged;
        }

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        int insertIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : Count;
                        foreach (Ingredient item in e.NewItems)
                        {
                            if (insertIndex >= 0 && insertIndex <= Count)
                                Insert(insertIndex++, item);
                            else
                                Add(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (Ingredient item in e.OldItems)
                        {
                            Remove(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        foreach (Ingredient _ in e.OldItems)
                        {
                            if (e.OldStartingIndex >= 0 && e.OldStartingIndex < Count)
                                RemoveAt(e.OldStartingIndex);
                        }
                    }
                    if (e.NewItems != null)
                    {
                        int idx = e.NewStartingIndex >= 0 ? e.NewStartingIndex : Count;
                        foreach (Ingredient item in e.NewItems)
                        {
                            if (idx >= 0 && idx <= Count)
                                Insert(idx++, item);
                            else
                                Add(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex < Count && e.NewStartingIndex < Count)
                    {
                        var item = this[e.OldStartingIndex];
                        base.RemoveAt(e.OldStartingIndex);
                        Insert(e.NewStartingIndex, item);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    base.ClearItems();
                    foreach (var item in _source)
                        Add(item);
                    break;
            }
        }

        // Optionally allow manual detaching if group is ever discarded
        public void Detach()
        {
            _source.CollectionChanged -= OnSourceCollectionChanged;
        }
    }
}
