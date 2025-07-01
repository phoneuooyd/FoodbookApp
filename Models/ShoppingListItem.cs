using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    /// <summary>
    /// Model reprezentuj�cy pozycj� na li�cie zakup�w z powi�zaniem do przepis�w
    /// </summary>
    public class ShoppingListItem
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Nazwa sk�adnika
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Ilo�� sk�adnika
        /// </summary>
        public double Quantity { get; set; }
        
        /// <summary>
        /// Jednostka sk�adnika
        /// </summary>
        public Unit Unit { get; set; }
        
        /// <summary>
        /// Czy sk�adnik zosta� zakupiony
        /// </summary>
        [NotMapped]
        public bool IsChecked { get; set; }
        
        /// <summary>
        /// Identyfikatory przepis�w, z kt�rych pochodzi ten sk�adnik
        /// Przechowywane jako string JSON dla prostoty
        /// </summary>
        public string RecipeIds { get; set; } = string.Empty;
        
        /// <summary>
        /// Nazwy przepis�w, z kt�rych pochodzi ten sk�adnik  
        /// Przechowywane jako string JSON dla prostoty
        /// </summary>
        public string RecipeNames { get; set; } = string.Empty;
        
        /// <summary>
        /// ID planu, do kt�rego nale�y ta pozycja
        /// </summary>
        public int PlanId { get; set; }
        
        /// <summary>
        /// Referencja do planu
        /// </summary>
        public Plan? Plan { get; set; }
        
        /// <summary>
        /// Lista ID przepis�w jako kolekcja (dla wygody u�ycia)
        /// </summary>
        [NotMapped]
        public List<int> RecipeIdsList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RecipeIds))
                    return new List<int>();
                
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<int>>(RecipeIds) ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                RecipeIds = System.Text.Json.JsonSerializer.Serialize(value);
            }
        }
        
        /// <summary>
        /// Lista nazw przepis�w jako kolekcja (dla wygody u�ycia)
        /// </summary>
        [NotMapped]
        public List<string> RecipeNamesList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RecipeNames))
                    return new List<string>();
                
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(RecipeNames) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                RecipeNames = System.Text.Json.JsonSerializer.Serialize(value);
            }
        }
        
        /// <summary>
        /// Formatowana lista nazw przepis�w do wy�wietlania
        /// </summary>
        [NotMapped]
        public string RecipesDisplayText
        {
            get
            {
                var names = RecipeNamesList;
                if (names.Count == 0)
                    return "Brak powi�zanych przepis�w";
                if (names.Count == 1)
                    return names[0];
                if (names.Count <= 3)
                    return string.Join(", ", names);
                return $"{string.Join(", ", names.Take(2))} i {names.Count - 2} innych";
            }
        }
    }
}