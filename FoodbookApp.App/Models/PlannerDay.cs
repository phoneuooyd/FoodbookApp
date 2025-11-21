using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Globalization;

namespace Foodbook.Models
{
    public class PlannerDay : INotifyPropertyChanged
    {
        public DateTime Date { get; }
        public ObservableCollection<PlannedMeal> Meals { get; } = new();

        public PlannerDay(DateTime date)
        {
            Date = date;
            Meals.CollectionChanged += OnMealsCollectionChanged;

            // Subscribe to localization changes so DateLabel updates when app culture changes.
            try
            {
                var provider = global::FoodbookApp.MauiProgram.ServiceProvider;
                if (provider != null)
                {
                    // Prefer LocalizationResourceManager if available (it raises PropertyChanged)
                    var mgr = provider.GetService(typeof(Foodbook.Services.LocalizationResourceManager)) as INotifyPropertyChanged;
                    if (mgr != null)
                    {
                        mgr.PropertyChanged += (_, __) => OnPropertyChanged(nameof(DateLabel));
                    }

                    // Also subscribe to ILocalizationService.CultureChanged as a fallback
                    var locSvc = provider.GetService(typeof(FoodbookApp.Interfaces.ILocalizationService));
                    if (locSvc != null)
                    {
                        // Use dynamic invocation to avoid adding a project reference here
                        try
                        {
                            var evt = locSvc.GetType().GetEvent("CultureChanged");
                            if (evt != null)
                            {
                                // attach a simple handler that raises property changed
                                void Handler(object? s, EventArgs e) => OnPropertyChanged(nameof(DateLabel));
                                evt.AddEventHandler(locSvc, (EventHandler)Handler);
                            }
                        }
                        catch
                        {
                            // ignore if reflection subscription fails
                        }
                    }
                }
            }
            catch
            {
                // Ignore when DI not available (e.g., unit tests)
            }
        }

        private void OnMealsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Unsubscribe from old items
            if (e.OldItems != null)
            {
                foreach (PlannedMeal meal in e.OldItems)
                {
                    meal.PropertyChanged -= OnMealPropertyChanged;
                }
            }

            // Subscribe to new items
            if (e.NewItems != null)
            {
                foreach (PlannedMeal meal in e.NewItems)
                {
                    meal.PropertyChanged += OnMealPropertyChanged;
                }
            }

            // Recalculate totals
            RaiseNutritionalTotalsChanged();
        }

        private void OnMealPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Recalculate when meal recipe or portions change
            if (e.PropertyName == nameof(PlannedMeal.Recipe) || 
                e.PropertyName == nameof(PlannedMeal.Portions))
            {
                RaiseNutritionalTotalsChanged();
            }
        }

        private void RaiseNutritionalTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalCalories));
            OnPropertyChanged(nameof(TotalProtein));
            OnPropertyChanged(nameof(TotalFat));
            OnPropertyChanged(nameof(TotalCarbs));
        }

        // Computed totals for the day - values are reported per 1 portion (do NOT multiply by meal.Portions)
        public double TotalCalories => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Calories / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalProtein => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Protein / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalFat => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Fat / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalCarbs => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Carbs / Math.Max(m.Recipe.IloscPorcji, 1)));

        // Localized label for the day, e.g. "Pi¹tek 21.12.2025". Uses current UI culture and capitalizes day name.
        public string DateLabel
        {
            get
            {
                try
                {
                    var culture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
                    var dayName = Date.ToString("dddd", culture);

                    // Capitalize using culture-specific TextInfo
                    var textInfo = culture.TextInfo;
                    var dayNameCapitalized = string.IsNullOrWhiteSpace(dayName)
                        ? dayName
                        : textInfo.ToTitleCase(dayName);

                    var datePart = Date.ToString("dd.MM.yyyy", culture);
                    return $"{dayNameCapitalized} {datePart}";
                }
                catch
                {
                    return Date.ToString("dd.MM.yyyy");
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
