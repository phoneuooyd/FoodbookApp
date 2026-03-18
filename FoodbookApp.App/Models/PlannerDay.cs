using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Graphics;

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

            try
            {
                var provider = global::FoodbookApp.MauiProgram.ServiceProvider;
                if (provider != null)
                {
                    var mgr = provider.GetService(typeof(Foodbook.Services.LocalizationResourceManager)) as INotifyPropertyChanged;
                    if (mgr != null)
                    {
                        mgr.PropertyChanged += (_, __) =>
                        {
                            OnPropertyChanged(nameof(DateLabel));
                            OnPropertyChanged(nameof(DayName));
                        };
                    }

                    var locSvc = provider.GetService(typeof(FoodbookApp.Interfaces.ILocalizationService));
                    if (locSvc != null)
                    {
                        try
                        {
                            var evt = locSvc.GetType().GetEvent("CultureChanged");
                            if (evt != null)
                            {
                                void Handler(object? s, EventArgs e)
                                {
                                    OnPropertyChanged(nameof(DateLabel));
                                    OnPropertyChanged(nameof(DayName));
                                }
                                evt.AddEventHandler(locSvc, (EventHandler)Handler);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void OnMealsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (PlannedMeal meal in e.OldItems)
                    meal.PropertyChanged -= OnMealPropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (PlannedMeal meal in e.NewItems)
                    meal.PropertyChanged += OnMealPropertyChanged;
            }

            RaiseNutritionalTotalsChanged();
        }

        private void OnMealPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlannedMeal.Recipe) || e.PropertyName == nameof(PlannedMeal.Portions))
                RaiseNutritionalTotalsChanged();
        }

        private void RaiseNutritionalTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalCalories));
            OnPropertyChanged(nameof(TotalProtein));
            OnPropertyChanged(nameof(TotalFat));
            OnPropertyChanged(nameof(TotalCarbs));
        }

        public double TotalCalories => Meals.Where(m => m.Recipe != null).Sum(m => m.Recipe!.Calories);
        public double TotalProtein => Meals.Where(m => m.Recipe != null).Sum(m => m.Recipe!.Protein);
        public double TotalFat => Meals.Where(m => m.Recipe != null).Sum(m => m.Recipe!.Fat);
        public double TotalCarbs => Meals.Where(m => m.Recipe != null).Sum(m => m.Recipe!.Carbs);

        public string DateLabel
        {
            get
            {
                try
                {
                    var culture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
                    var dayName = Date.ToString("dddd", culture);
                    var dayNameCapitalized = string.IsNullOrWhiteSpace(dayName)
                        ? dayName
                        : culture.TextInfo.ToTitleCase(dayName);

                    var datePart = Date.ToString("dd.MM.yyyy", culture);
                    return $"{dayNameCapitalized} {datePart}";
                }
                catch
                {
                    return Date.ToString("dd.MM.yyyy");
                }
            }
        }

        public string DayName
        {
            get
            {
                try
                {
                    var culture = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
                    var dayName = Date.ToString("dddd", culture);
                    return string.IsNullOrWhiteSpace(dayName) ? dayName : culture.TextInfo.ToTitleCase(dayName);
                }
                catch
                {
                    return Date.DayOfWeek.ToString();
                }
            }
        }

        private static Color GetAppColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
                return c;
            return fallback;
        }

        public Color DayAccentColor
        {
            get
            {
                var idx = ((int)Date.DayOfWeek + 6) % 7; // Mon=0..Sun=6
                var primary = GetAppColor("Primary", Color.FromArgb("#5B3FE8"));

                return idx switch
                {
                    0 => primary,
                    1 => Color.FromArgb("#10B981"),
                    2 => Color.FromArgb("#F59E0B"),
                    3 => Color.FromArgb("#F43F5E"),
                    4 => Color.FromArgb("#8B5CF6"),
                    5 => Color.FromArgb("#3B82F6"),
                    _ => Color.FromArgb("#EC4899")
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
