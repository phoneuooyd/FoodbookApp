using Foodbook.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml;

namespace Foodbook.Services;

public class RecipeImporter
{
    private readonly HttpClient _httpClient;

    public RecipeImporter(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<Recipe> ImportFromUrlAsync(string url)
    {
        var html = await _httpClient.GetStringAsync(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var recipe = new Recipe
        {
            Name = ExtractTitleFromUrl(url), // Można później pobierać <title>
            Ingredients = new List<Ingredient>()
        };

        // 1. SKŁADNIKI
        var ingredientHeader = doc.DocumentNode.SelectSingleNode("//h3[contains(., 'Składniki')]");
        if (ingredientHeader != null)
        {
            var ingredientList = ingredientHeader.SelectSingleNode("following-sibling::ul[1]");
            if (ingredientList != null)
            {
                foreach (var li in ingredientList.SelectNodes("li"))
                {
                    var parsed = ParseIngredient(li.InnerText.Trim());

                    if (parsed != null)
                        recipe.Ingredients.Add(parsed);
                }
            }
        }

        // 2. WARTOŚCI ODŻYWCZE
        var nutritionHeader = doc.DocumentNode.SelectSingleNode("//h3[contains(., 'Wartości odżywcze')]");
        if (nutritionHeader != null)
        {
            var p = nutritionHeader.SelectSingleNode("following-sibling::p[1]");
            if (p != null)
            {
                var lines = p.InnerHtml.Split("<br>");
                foreach (var line in lines.Select(HtmlEntity.DeEntitize))
                {
                    var text = line.Trim();

                    if (text.StartsWith("Kaloryczność:"))
                        recipe.Calories = ParseDoubleFromLine(text);
                    else if (text.StartsWith("Białko:"))
                        recipe.Protein = ParseDoubleFromLine(text);
                    else if (text.StartsWith("Tłuszcze:"))
                        recipe.Fat = ParseDoubleFromLine(text);
                    else if (text.StartsWith("Węglowodany:"))
                        recipe.Carbs = ParseDoubleFromLine(text);
                }
            }
        }

        return recipe;
    }

    private double ParseDoubleFromLine(string line)
    {
        var match = Regex.Match(line, @"([\d,.]+)");
        var str = match.Value.Replace(",", "."); // dla kultury PL
        return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : 0;
    }

    private Ingredient? ParseIngredient(string raw)
    {
        // Przykłady: "1/2 burraty (62g)", "1 łyżeczka oliwy", "12-15 pomidorków"
        var quantityMatch = Regex.Match(raw, @"(\d+([.,]\d+)?(/\d+)?)(\s?[a-zA-Ząćęłńóśźż]*)?");
        var unitMatch = Regex.Match(raw.ToLower(), @"(g|gram|ml|mililitr|szt|sztuk|sztuki|sztuka)");

        double quantity = 1;
        if (quantityMatch.Success)
        {
            var qStr = quantityMatch.Value.Replace(",", ".").Trim();
            if (qStr.Contains("/"))
            {
                var parts = qStr.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], out var num) && double.TryParse(parts[1], out var den))
                    quantity = num / den;
            }
            else if (double.TryParse(qStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                quantity = val;
            }
        }

        var unit = Unit.Piece;
        if (unitMatch.Success)
        {
            var u = unitMatch.Value;
            if (u.Contains("g")) unit = Unit.Gram;
            else if (u.Contains("ml")) unit = Unit.Milliliter;
            else unit = Unit.Piece;
        }

        // Cała nazwa składnika bez ilości
        var name = Regex.Replace(raw, @"^[\d\s/.,()-]+", "").Trim();

        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new Ingredient
        {
            Name = name,
            Quantity = quantity,
            Unit = unit
        };
    }

    private string ExtractTitleFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var slug = uri.Segments.Last().Replace("-", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slug);
        }
        catch
        {
            return "Imported Recipe";
        }
    }
}
