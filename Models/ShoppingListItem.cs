using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    /// <summary>
    /// Model reprezentuj¹cy pozycjê na liœcie zakupów z powi¹zaniem do przepisów
    /// </summary>
    public class ShoppingListItem
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Nazwa sk³adnika
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Iloœæ sk³adnika
        /// </summary>
        public double Quantity { get; set; }
        
        /// <summary>
        /// Jednostka sk³adnika
        /// </summary>
        public Unit Unit { get; set; }
        
        /// <summary>
        /// Czy sk³adnik zosta³ zakupiony
        /// </summary>
        [NotMapped]
        public bool IsChecked { get; set; }
        
        /// <summary>
        /// Identyfikatory przepisów, z których pochodzi ten sk³adnik
        /// Przechowywane jako string JSON dla prostoty
        /// </summary>
        public string RecipeIds { get; set; } = string.Empty;
        
        /// <summary>
        /// Nazwy przepisów, z których pochodzi ten sk³adnik  
        /// Przechowywane jako string JSON dla prostoty
        /// </summary>
        public string RecipeNames { get; set; } = string.Empty;
        
        /// <summary>
        /// ID planu, do którego nale¿y ta pozycja
        /// </summary>
        public int PlanId { get; set; }
        
        /// <summary>
        /// Referencja do planu
        /// </summary>
        public Plan? Plan { get; set; }
        
        /// <summary>
        /// Lista ID przepisów jako kolekcja (dla wygody u¿ycia)
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
        /// Lista nazw przepisów jako kolekcja (dla wygody u¿ycia)
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
        /// Formatowana lista nazw przepisów do wyœwietlania
        /// </summary>
        [NotMapped]
        public string RecipesDisplayText
        {
            get
            {
                var names = RecipeNamesList;
                if (names.Count == 0)
                    return "Brak powi¹zanych przepisów";
                if (names.Count == 1)
                    return names[0];
                if (names.Count <= 3)
                    return string.Join(", ", names);
                return $"{string.Join(", ", names.Take(2))} i {names.Count - 2} innych";
            }
        }
    }
}