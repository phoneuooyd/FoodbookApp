using Foodbook.Models;

namespace Foodbook.Services
{
    public interface IShoppingListService
    {
        /// <summary>
        /// Pobiera listê sk³adników dla okreœlonego zakresu dat (stara metoda - zachowana dla kompatybilnoœci)
        /// </summary>
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
        
        /// <summary>
        /// Pobiera pozycje listy zakupów dla okreœlonego planu z powi¹zaniami do przepisów
        /// </summary>
        Task<List<ShoppingListItem>> GetShoppingListItemsAsync(int planId);
        
        /// <summary>
        /// Generuje pozycje listy zakupów dla planu
        /// </summary>
        Task<List<ShoppingListItem>> GenerateShoppingListAsync(int planId);
        
        /// <summary>
        /// Aktualizuje stan pozycji listy zakupów (np. zaznaczenie jako zakupione)
        /// </summary>
        Task UpdateShoppingListItemAsync(ShoppingListItem item);
        
        /// <summary>
        /// Usuwa pozycjê z listy zakupów
        /// </summary>
        Task DeleteShoppingListItemAsync(int itemId);
    }
}
