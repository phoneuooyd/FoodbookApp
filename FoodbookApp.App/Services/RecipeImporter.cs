using Foodbook.Models;
using FoodbookApp.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml;

namespace Foodbook.Services;

public class RecipeImporter
{
    private readonly HttpClient _httpClient;
    private readonly IIngredientService _ingredientService;

    public RecipeImporter(HttpClient httpClient, IIngredientService ingredientService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
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

        // Załaduj listę dostępnych składników z bazy
        var availableIngredients = await _ingredientService.GetIngredientsAsync();

        // 1. SKŁADNIKI
        var ingredientHeader = doc.DocumentNode.SelectSingleNode("//h3[contains(translate(.,'ABCDEFGHIJKLMNOPQRSTUVWXYZĄĆĘŁŃÓŚŹŻ','abcdefghijklmnopqrstuvwxyząćęłńóśźż'),'składniki')]");
        if (ingredientHeader != null)
        {
            // czasami lista składników jest oddzielona dodatkowymi węzłami
            var ingredientList = ingredientHeader.SelectSingleNode("following::ul[1]");
            if (ingredientList != null)
            {
                foreach (var li in ingredientList.SelectNodes("li"))
                {
                    var parsed = ParseIngredient(li.InnerText.Trim(), availableIngredients);

                    if (parsed != null)
                        recipe.Ingredients.Add(parsed);
                }
            }
        }

        // 2. WARTOŚCI ODŻYWCZE
        var nutritionHeader = doc.DocumentNode.SelectSingleNode("//h3[contains(translate(.,'ABCDEFGHIJKLMNOPQRSTUVWXYZĄĆĘŁŃÓŚŹŻ','abcdefghijklmnopqrstuvwxyząćęłńóśźż'),'wartości odżywcze')]");
        if (nutritionHeader != null)
        {
            var p = nutritionHeader.SelectSingleNode("following::p[1]");
            if (p != null)
            {
                // InnerText konwertuje znaczniki <br> na nowe linie
                var lines = HtmlEntity.DeEntitize(p.InnerText).Split('\n');
                foreach (var line in lines)
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

    private Ingredient? ParseIngredient(string raw, List<Ingredient> availableIngredients)
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
        var extractedName = Regex.Replace(raw, @"^[\d\s/.,()-]+", "").Trim();
        
        if (string.IsNullOrWhiteSpace(extractedName))
            return null;

        // Próba dopasowania do istniejących składników w bazie
        var matchedIngredient = FindBestMatch(extractedName, availableIngredients);
        
        var ingredient = new Ingredient
        {
            Quantity = quantity,
            Unit = unit
        };

        if (matchedIngredient != null)
        {
            // Użyj danych z bazy
            ingredient.Name = matchedIngredient.Name;
            ingredient.Calories = matchedIngredient.Calories;
            ingredient.Protein = matchedIngredient.Protein;
            ingredient.Fat = matchedIngredient.Fat;
            ingredient.Carbs = matchedIngredient.Carbs;
        }
        else
        {
            // Użyj pierwotnej nazwy jeśli nie znaleziono dopasowania
            ingredient.Name = extractedName;
            ingredient.Calories = 0;
            ingredient.Protein = 0;
            ingredient.Fat = 0;
            ingredient.Carbs = 0;
        }

        return ingredient;
    }

    private Ingredient? FindBestMatch(string ingredientName, List<Ingredient> availableIngredients)
    {
        var normalizedName = NormalizeName(ingredientName);
        
        // 1. Dokładne dopasowanie (bez normalizacji)
        var exactMatch = availableIngredients.FirstOrDefault(i => 
            string.Equals(i.Name, ingredientName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null) return exactMatch;

        // 2. Dokładne dopasowanie (z normalizacją)
        var normalizedExactMatch = availableIngredients.FirstOrDefault(i => 
            string.Equals(NormalizeName(i.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
        if (normalizedExactMatch != null) return normalizedExactMatch;

        // 3. Dopasowanie zawierające (nazwa składnika zawiera się w nazwie z bazy)
        var containsMatch = availableIngredients.FirstOrDefault(i => 
            NormalizeName(i.Name).Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains(NormalizeName(i.Name), StringComparison.OrdinalIgnoreCase));
        if (containsMatch != null) return containsMatch;

        // 4. Podobieństwo Levenshtein (dla przypadków typu pomidor/pomidory/pomidorki)
        var bestMatch = availableIngredients
            .Select(i => new { Ingredient = i, Distance = LevenshteinDistance(normalizedName, NormalizeName(i.Name)) })
            .Where(x => x.Distance <= Math.Max(2, Math.Min(normalizedName.Length, NormalizeName(x.Ingredient.Name).Length) / 3))
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return bestMatch?.Ingredient;
    }

    private static string NormalizeName(string name)
    {
        // Usuń diakrytyki, zmień na małe litery i usuń niepotrzebne znaki
        return name.ToLowerInvariant()
            .Replace("ą", "a").Replace("ć", "c").Replace("ę", "e")
            .Replace("ł", "l").Replace("ń", "n").Replace("ó", "o")
            .Replace("ś", "s").Replace("ź", "z").Replace("ż", "z")
            .Replace("sz", "s").Replace("cz", "c").Replace("ch", "h")
            .Replace("rz", "z").Replace("dz", "z")
            .Trim();
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
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
