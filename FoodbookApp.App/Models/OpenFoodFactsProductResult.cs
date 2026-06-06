namespace Foodbook.Models;

public class OpenFoodFactsProductResult
{
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Calories100g { get; set; }
    public double Protein100g { get; set; }
    public double Fat100g { get; set; }
    public double Carbs100g { get; set; }
}
