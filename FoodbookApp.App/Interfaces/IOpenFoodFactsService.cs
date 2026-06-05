using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IOpenFoodFactsService
{
    Task<OpenFoodFactsProductResult?> GetProductByBarcodeAsync(string barcode);
}
