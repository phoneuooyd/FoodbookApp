using Foodbook.Models;

namespace Foodbook.Services
{
    public interface IShoppingListService
    {
        /// <summary>
        /// Pobiera list� sk�adnik�w dla okre�lonego zakresu dat (stara metoda - zachowana dla kompatybilno�ci)
        /// </summary>
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
        
        /// <summary>
        /// Pobiera pozycje listy zakup�w dla okre�lonego planu z powi�zaniami do przepis�w
        /// </summary>
        Task<List<ShoppingListItem>> GetShoppingListItemsAsync(int planId);
        
        /// <summary>
        /// Generuje pozycje listy zakup�w dla planu
        /// </summary>
        Task<List<ShoppingListItem>> GenerateShoppingListAsync(int planId);
        
        /// <summary>
        /// Aktualizuje stan pozycji listy zakup�w (np. zaznaczenie jako zakupione)
        /// </summary>
        Task UpdateShoppingListItemAsync(ShoppingListItem item);
        
        /// <summary>
        /// Usuwa pozycj� z listy zakup�w
        /// </summary>
        Task DeleteShoppingListItemAsync(int itemId);
    }
}
