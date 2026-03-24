using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Foodbook.Services;

public class RecipeImporter
{
    private readonly HttpClient _httpClient;
    private readonly IIngredientService _ingredientService;
    private readonly IAIService? _aiService;

    private const string Tag = "[RecipeImporter]";
    private const double MinIngredientQuantity = 0.1;

    private static readonly string[] IngredientKeywords =
    [
        "składniki", "ingredients", "zutaten", "ingrédients",
        "ingredienti", "ingredientes"
    ];

    private static readonly string[] PreparationKeywords =
    [
        "opis przygotowania", "opis wykonania", "opis przyrzadzenia",
        "sposob przygotowania", "sposob wykonania", "sposob przyrzadzenia",
        "przygotowanie", "wykonanie", "przyrzadzenie",
        "jak przygotowac", "jak wykonac", "jak przyrzadzic",
        "kroki", "krok po kroku", "instrukcja", "instrukcje",
        "preparation", "instructions", "directions", "method",
        "how to make", "steps", "procedure", "cooking steps",
        "zubereitung", "anleitung",
        "etapes", "methode",
        "preparacion", "instrucciones", "pasos", "elaboracion",
        "preparazione", "istruzioni", "procedimento",
    ];

    public RecipeImporter(
        HttpClient httpClient,
        IIngredientService ingredientService,
        IAIService? aiService = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
        _aiService = aiService;
    }

    // ─── Publiczne API ────────────────────────────────────────────────────────

    public async Task<Recipe> ImportFromUrlAsync(string url, bool allowAiFallback)
    {
        Debug.WriteLine($"{Tag} ══════════════════════════════════════════");
        Debug.WriteLine($"{Tag} START import: {url}");
        Debug.WriteLine($"{Tag} AI fallback dozwolony: {(allowAiFallback ? "TAK" : "NIE")}");
        Debug.WriteLine($"{Tag} AI fallback dostępny technicznie: {(_aiService is not null ? "TAK" : "NIE")}");

        var html = await _httpClient.GetStringAsync(url);
        Debug.WriteLine($"{Tag} HTML pobrany, rozmiar: {html.Length:N0} znaków");

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var recipe = new Recipe
        {
            Name = ExtractTitle(url, doc),
            Ingredients = []
        };
        Debug.WriteLine($"{Tag} Tytuł przepisu: \"{recipe.Name}\"");

        var prepSteps = ExtractPreparationSteps(doc);
        if (prepSteps.Count > 0)
        {
            recipe.Description = string.Join(Environment.NewLine + Environment.NewLine, prepSteps);
            Debug.WriteLine($"{Tag} Opis przygotowania: {prepSteps.Count} kroków ({recipe.Description.Length} znaków łącznie)");
            for (int i = 0; i < prepSteps.Count; i++)
                Debug.WriteLine($"{Tag}   Krok {i + 1}: {Truncate(prepSteps[i], 120)}");
        }
        else
        {
            Debug.WriteLine($"{Tag} Opis przygotowania: BRAK (żadna strategia nie znalazła treści)");
        }

        var availableIngredients = await _ingredientService.GetIngredientsAsync();
        Debug.WriteLine($"{Tag} Baza składników: {availableIngredients.Count} pozycji");

        var parsedIngredients = new List<Ingredient>();
        var localUnmatchedIngredients = new List<Ingredient>();
        var unmatchedLines = new List<string>();

        // 1. SKŁADNIKI
        var ingredientHeaders = FindAllNodesByKeywords(doc, IngredientKeywords);
        Debug.WriteLine($"{Tag} Nagłówki sekcji składników: {ingredientHeaders.Count}");
        foreach (var h in ingredientHeaders)
            Debug.WriteLine($"{Tag}   → <{h.Name}> \"{Truncate(HtmlEntity.DeEntitize(h.InnerText).Trim(), 80)}\" (pos={h.StreamPosition})");

        var seenLists = new HashSet<HtmlNode>(ReferenceEqualityComparer.Instance);

        foreach (var header in ingredientHeaders)
        {
            var ul = header.SelectSingleNode("following::ul[1]");
            if (ul is null)
            {
                Debug.WriteLine($"{Tag}   Nagłówek \"{Truncate(header.InnerText, 50)}\": brak <ul> po nagłówku — pomijam");
                continue;
            }
            if (!seenLists.Add(ul))
            {
                Debug.WriteLine($"{Tag}   Nagłówek \"{Truncate(header.InnerText, 50)}\": lista pos={ul.StreamPosition} już przetworzona — pomijam duplikat");
                continue;
            }

            var nextHeader = header.SelectSingleNode("following::*[self::h2 or self::h3 or self::h4][1]");
            if (nextHeader is not null && nextHeader.StreamPosition < ul.StreamPosition)
            {
                Debug.WriteLine($"{Tag}   Nagłówek \"{Truncate(header.InnerText, 50)}\": kolejny nagłówek (pos={nextHeader.StreamPosition}) jest przed listą (pos={ul.StreamPosition}) — pomijam");
                continue;
            }

            var liNodes = ul.SelectNodes("li");
            Debug.WriteLine($"{Tag}   Przetwarzam <ul> pos={ul.StreamPosition}: {liNodes?.Count ?? 0} elementów <li>");

            foreach (var li in liNodes ?? Enumerable.Empty<HtmlNode>())
            {
                var rawText = HtmlEntity.DeEntitize(li.InnerText).Trim();
                var (parsed, isMatched) = ParseIngredientWithMatchFlag(rawText, availableIngredients);
                if (parsed is null)
                {
                    Debug.WriteLine($"{Tag}     POMINIĘTY (pusty/null): \"{rawText}\"");
                    continue;
                }

                if (isMatched)
                {
                    Debug.WriteLine($"{Tag}     ✓ DOPASOWANY: \"{rawText}\" → \"{parsed.Name}\" qty={parsed.Quantity} unit={parsed.Unit}");
                    parsedIngredients.Add(parsed);
                }
                else
                {
                    Debug.WriteLine($"{Tag}     ✗ NIEDOPASOWANY: \"{rawText}\" → \"{parsed.Name}\" (brak w bazie)");
                    localUnmatchedIngredients.Add(parsed);
                    unmatchedLines.Add(rawText);
                }
            }
        }

        Debug.WriteLine($"{Tag} Składniki po parsowaniu lokalnym: {parsedIngredients.Count} dopasowanych, {unmatchedLines.Count} niedopasowanych");
        recipe.HasUnmatchedIngredients = unmatchedLines.Count > 0;

        // 2. WARTOŚCI ODŻYWCZE
        ExtractNutrition(doc, recipe);
        Debug.WriteLine($"{Tag} Wartości odżywcze: kcal={recipe.Calories} | B={recipe.Protein}g | T={recipe.Fat}g | W={recipe.Carbs}g");

        // 3. AI FALLBACK
        var aiSuccess = false;
        var localParserWasInsufficient = unmatchedLines.Count > 0;
        if (allowAiFallback && localParserWasInsufficient && _aiService is not null)
        {
            recipe.WasAiFallbackUsed = true;
            Debug.WriteLine($"{Tag} ── AI FALLBACK ──────────────────────────────");
            Debug.WriteLine($"{Tag} Wywołuję AI dla {unmatchedLines.Count} niedopasowanych składników:");
            for (int i = 0; i < unmatchedLines.Count; i++)
                Debug.WriteLine($"{Tag}   {i + 1}. {unmatchedLines[i]}");

            try
            {
                var aiResult = await TryEnhanceWithAiAsync(recipe.Name, unmatchedLines, availableIngredients);
                if (aiResult is not null)
                {
                    aiSuccess = true;
                    Debug.WriteLine($"{Tag} AI odpowiedź OK: {aiResult.Ingredients.Count} składników");

                    if (!string.IsNullOrWhiteSpace(aiResult.Title))
                    {
                        Debug.WriteLine($"{Tag}   Tytuł z AI: \"{aiResult.Title}\" (poprzedni: \"{recipe.Name}\")");
                        recipe.Name = aiResult.Title;
                    }
                    if (aiResult.Calories > 0 && recipe.Calories == 0) { recipe.Calories = aiResult.Calories; Debug.WriteLine($"{Tag}   Kalorie z AI: {aiResult.Calories}"); }
                    if (aiResult.Protein > 0 && recipe.Protein == 0) { recipe.Protein = aiResult.Protein; Debug.WriteLine($"{Tag}   Białko z AI: {aiResult.Protein}"); }
                    if (aiResult.Fat > 0 && recipe.Fat == 0) { recipe.Fat = aiResult.Fat; Debug.WriteLine($"{Tag}   Tłuszcz z AI: {aiResult.Fat}"); }
                    if (aiResult.Carbs > 0 && recipe.Carbs == 0) { recipe.Carbs = aiResult.Carbs; Debug.WriteLine($"{Tag}   Węglowodany z AI: {aiResult.Carbs}"); }

                    foreach (var aiIng in aiResult.Ingredients)
                    {
                        var unit = aiIng.Unit.ToLowerInvariant() switch
                        {
                            "gram" => Unit.Gram,
                            "milliliter" => Unit.Milliliter,
                            _ => Unit.Piece
                        };
                        var newIng = new Ingredient
                        {
                            Name = aiIng.RawName,
                            Quantity = EnsurePositiveQuantity(aiIng.Quantity),
                            Unit = unit
                        };

                        if (!string.IsNullOrWhiteSpace(aiIng.MatchedDbName))
                        {
                            var dbMatch = availableIngredients.FirstOrDefault(i =>
                                string.Equals(i.Name, aiIng.MatchedDbName, StringComparison.OrdinalIgnoreCase));
                            if (dbMatch is not null)
                            {
                                newIng.Name = dbMatch.Name;
                                newIng.Calories = dbMatch.Calories;
                                newIng.Protein = dbMatch.Protein;
                                newIng.Fat = dbMatch.Fat;
                                newIng.Carbs = dbMatch.Carbs;
                                Debug.WriteLine($"{Tag}   ✓ AI: \"{aiIng.RawName}\" → \"{dbMatch.Name}\" qty={newIng.Quantity} unit={unit}");
                            }
                            else
                            {
                                Debug.WriteLine($"{Tag}   ~ AI sugerował \"{aiIng.MatchedDbName}\" ale NIE ZNALEZIONO w bazie — używam raw: \"{aiIng.RawName}\"");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"{Tag}   ✗ AI nie dopasował: \"{aiIng.RawName}\" (matched_db_name=null)");
                        }
                        parsedIngredients.Add(newIng);
                    }
                }
                else
                {
                    Debug.WriteLine($"{Tag} AI zwróciło null — graceful degradation do lokalnego parsera");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{Tag} AI WYJĄTEK: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else if (!allowAiFallback)
        {
            Debug.WriteLine($"{Tag} AI fallback: pominięty — brak uprawnień (allowAiFallback=false)");
        }
        else if (!localParserWasInsufficient)
        {
            Debug.WriteLine($"{Tag} AI fallback: pominięty — wszystkie składniki dopasowane lokalnie");
        }
        else
        {
            Debug.WriteLine($"{Tag} AI fallback: pominięty — brak IAIService");
        }

        if (!aiSuccess && localUnmatchedIngredients.Count > 0)
        {
            Debug.WriteLine($"{Tag} Dołączam {localUnmatchedIngredients.Count} niedopasowanych składników lokalnie (bez AI)");
            parsedIngredients.AddRange(localUnmatchedIngredients);
        }

        recipe.Ingredients = parsedIngredients;

        Debug.WriteLine($"{Tag} ── WYNIK KOŃCOWY ────────────────────────────");
        Debug.WriteLine($"{Tag} Tytuł:      {recipe.Name}");
        Debug.WriteLine($"{Tag} Składniki:  {recipe.Ingredients.Count} szt.");
        Debug.WriteLine($"{Tag} Opis:       {(string.IsNullOrEmpty(recipe.Description) ? "(brak)" : $"{recipe.Description.Length} znaków")}");
        Debug.WriteLine($"{Tag} Kalorie:    {recipe.Calories} kcal | B={recipe.Protein}g T={recipe.Fat}g W={recipe.Carbs}g");
        Debug.WriteLine($"{Tag} ══════════════════════════════════════════");

        return recipe;
    }

    // ─── Wartości odżywcze ────────────────────────────────────────────────────

    private static void ExtractNutrition(HtmlDocument doc, Recipe recipe)
    {
        var header = FindAllNodesByKeywords(doc,
            ["wartosci odzywcze", "nutrition", "nahrwert", "valeur nutritive"])
            .FirstOrDefault();

        if (header is null)
        {
            Debug.WriteLine($"{Tag} ExtractNutrition: brak nagłówka — pomijam");
            return;
        }

        Debug.WriteLine($"{Tag} ExtractNutrition: nagłówek <{header.Name}> \"{Truncate(header.InnerText, 60)}\" pos={header.StreamPosition}");

        var node = header.SelectSingleNode("following::p[1]")
                ?? header.SelectSingleNode("following::ul[1]");
        if (node is null)
        {
            Debug.WriteLine($"{Tag} ExtractNutrition: brak <p>/<ul> po nagłówku");
            return;
        }

        Debug.WriteLine($"{Tag} ExtractNutrition: parsowanie z <{node.Name}>");

        foreach (var line in HtmlEntity.DeEntitize(node.InnerText)
            .Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (t.StartsWith("Kaloryczno", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("kcal", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("Kalori", StringComparison.OrdinalIgnoreCase))
                recipe.Calories = ParseFirstNumber(t);
            else if (t.StartsWith("Białko", StringComparison.OrdinalIgnoreCase) ||
                     t.StartsWith("Protein", StringComparison.OrdinalIgnoreCase) ||
                     t.StartsWith("białka", StringComparison.OrdinalIgnoreCase))
                recipe.Protein = ParseFirstNumber(t);
            else if (t.StartsWith("Tłuszcze", StringComparison.OrdinalIgnoreCase) ||
                     t.StartsWith("Fat", StringComparison.OrdinalIgnoreCase))
                recipe.Fat = ParseFirstNumber(t);
            else if (t.StartsWith("Węglowod", StringComparison.OrdinalIgnoreCase) ||
                     t.StartsWith("Carb", StringComparison.OrdinalIgnoreCase))
                recipe.Carbs = ParseFirstNumber(t);
        }
    }

    // ─── Sposób przygotowania ─────────────────────────────────────────────────

    private static List<string> ExtractPreparationSteps(HtmlDocument doc)
    {
        Debug.WriteLine($"{Tag} ExtractPreparationSteps: START");

        // Strategia 1: schema.org @itemprop
        var schemaNodes = doc.DocumentNode
            .SelectNodes("//*[@itemprop='recipeInstructions' or @itemprop='step']");
        if (schemaNodes is not null)
        {
            var steps = schemaNodes.Select(CleanNodeText).Where(s => s.Length > 5).ToList();
            Debug.WriteLine($"{Tag}   S1 schema.org: {schemaNodes.Count} węzłów → {steps.Count} kroków");
            if (IsPreparationAcceptable(steps))
            {
                Debug.WriteLine($"{Tag}   → WYNIK S1: schema.org ({steps.Count} kroków)");
                return steps;
            }
            Debug.WriteLine($"{Tag}   → S1 odrzucona (zbyt krótka lub zawiera tagi nawigacyjne)");
        }
        else
        {
            Debug.WriteLine($"{Tag}   S1 schema.org: brak @itemprop węzłów");
        }

        // Strategia 2: nagłówki sekcji → treść
        var allSteps = new List<string>();
        var processedPos = new HashSet<int>();
        var matchedCount = 0;

        foreach (var keyword in PreparationKeywords)
        {
            foreach (var header in FindAllNodesByKeywords(doc, [keyword]))
            {
                if (!processedPos.Add(header.StreamPosition)) continue;
                matchedCount++;

                Debug.WriteLine($"{Tag}   S2 nagłówek: <{header.Name}> \"{Truncate(HtmlEntity.DeEntitize(header.InnerText).Trim(), 70)}\" keyword=\"{keyword}\" pos={header.StreamPosition}");

                var content = ExtractContentAfterHeader(doc, header);
                if (content is not null)
                {
                    Debug.WriteLine($"{Tag}     → {content.Count} elementów ({string.Join(" ", content).Length} znaków)");
                    allSteps.AddRange(content);
                }
                else
                {
                    Debug.WriteLine($"{Tag}     → brak treści po tym nagłówku");
                }
            }
        }

        Debug.WriteLine($"{Tag}   S2: przetworzono {matchedCount} nagłówków, zebrano {allSteps.Count} elementów");

        if (IsPreparationAcceptable(allSteps))
        {
            var deduped = allSteps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Debug.WriteLine($"{Tag}   → WYNIK S2: nagłówki ({deduped.Count} kroków po deduplikacji)");
            return deduped;
        }

        Debug.WriteLine($"{Tag}   → S2 odrzucona (zbyt krótka lub zawiera tagi)");

        // Strategia 3: pierwsza <ol>
        var ol = doc.DocumentNode.SelectSingleNode("//ol");
        if (ol is not null)
        {
            var liTexts = ExtractLiTexts(ol);
            Debug.WriteLine($"{Tag}   S3 pierwsza <ol>: {liTexts?.Count ?? 0} elementów");
            if (liTexts is not null && liTexts.Count >= 3 && IsPreparationAcceptable(liTexts))
            {
                Debug.WriteLine($"{Tag}   → WYNIK S3: <ol> ({liTexts.Count} kroków)");
                return liTexts;
            }
            Debug.WriteLine($"{Tag}   → S3 odrzucona");
        }
        else
        {
            Debug.WriteLine($"{Tag}   S3: brak <ol> w dokumencie");
        }

        Debug.WriteLine($"{Tag}   → WYNIK: BRAK treści przygotowania");
        return [];
    }

    private static List<string>? ExtractContentAfterHeader(HtmlDocument doc, HtmlNode header)
    {
        var nextHeadingPos = doc.DocumentNode
            .SelectNodes("//*[self::h2 or self::h3 or self::h4]")?
            .Where(h => h.StreamPosition > header.StreamPosition)
            .Select(h => h.StreamPosition)
            .DefaultIfEmpty(int.MaxValue)
            .Min() ?? int.MaxValue;

        Debug.WriteLine($"{Tag}     Sekcja: pos={header.StreamPosition}..{nextHeadingPos}");

        var result = new List<string>();

        var listCount = 0;
        for (int i = 1; i <= 10; i++)
        {
            var list = header.SelectSingleNode($"following::*[self::ol or self::ul][{i}]");
            if (list is null || list.StreamPosition >= nextHeadingPos) break;
            var items = ExtractLiTexts(list);
            if (items is not null)
            {
                Debug.WriteLine($"{Tag}     <{list.Name}> pos={list.StreamPosition}: {items.Count} <li>");
                result.AddRange(items);
                listCount += items.Count;
            }
        }

        var paraCount = 0;
        for (int i = 1; i <= 30; i++)
        {
            var p = header.SelectSingleNode($"following::p[{i}]");
            if (p is null || p.StreamPosition >= nextHeadingPos) break;
            var text = CleanNodeText(p);
            if (text.Length > 10) { result.Add(text); paraCount++; }
        }

        Debug.WriteLine($"{Tag}     Zebrano: {listCount} li + {paraCount} p = {result.Count} elementów");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = result.Where(s => seen.Add(s)).ToList();
        return deduped.Count > 0 ? deduped : null;
    }

    private static bool IsPreparationAcceptable(List<string>? steps)
    {
        if (steps is null || steps.Count == 0) return false;
        var joined = string.Join(" ", steps);
        if (joined.Length < 30) return false;
        if (Regex.IsMatch(joined,
            @"\b(tagi|kategorie|udostepnij|dietaonline|find similar|related recipes|powiazane)\b",
            RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    // ─── Parsowanie składników ────────────────────────────────────────────────

    private (Ingredient? ingredient, bool isMatched) ParseIngredientWithMatchFlag(
        string raw, List<Ingredient> available)
    {
        var ingredient = ParseIngredient(raw, available);
        if (ingredient is null) return (null, false);
        bool isMatched = available.Any(i => i.Name == ingredient.Name);
        return (ingredient, isMatched);
    }

    private Ingredient? ParseIngredient(string raw, List<Ingredient> available)
    {
        double quantity = 1;
        var unit = Unit.Piece;
        var rawLower = raw.ToLowerInvariant();

        var wm = Regex.Match(rawLower, @"\((\d+([.,]\d+)?)\s*g\)");
        if (wm.Success &&
            double.TryParse(wm.Groups[1].Value.Replace(",", "."),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
        {
            quantity = w;
            unit = Unit.Gram;
        }
        else
        {
            var rm = Regex.Match(rawLower, @"^(\d+)\s*[-–]\s*(\d+)");
            if (rm.Success &&
                double.TryParse(rm.Groups[1].Value, out var lo) &&
                double.TryParse(rm.Groups[2].Value, out var hi))
                quantity = (lo + hi) / 2.0;
            else
            {
                var fm = Regex.Match(rawLower, @"^(\d+)/(\d+)");
                if (fm.Success &&
                    double.TryParse(fm.Groups[1].Value, out var num) &&
                    double.TryParse(fm.Groups[2].Value, out var den) && den > 0)
                    quantity = num / den;
                else
                {
                    var qm = Regex.Match(rawLower, @"^(\d+([.,]\d+)?)");
                    if (qm.Success &&
                        double.TryParse(qm.Groups[1].Value.Replace(",", "."),
                            NumberStyles.Any, CultureInfo.InvariantCulture, out var q))
                        quantity = q;
                }
            }

            if (Regex.IsMatch(rawLower, @"\b(szklank[aię]|szkl|cup)\b"))
            { unit = Unit.Milliliter; quantity *= 250; }
            else if (Regex.IsMatch(rawLower, @"\b(łyżk[iaę]|łyżek|łyż|tbsp|tablespoon)\b"))
            { unit = Unit.Milliliter; quantity *= 15; }
            else if (Regex.IsMatch(rawLower, @"\b(łyżeczk[aię]|łyżeczek|łyżecz|tsp|teaspoon)\b"))
            { unit = Unit.Milliliter; quantity *= 5; }
            else if (Regex.IsMatch(rawLower, @"\b(kg|kilogram[yów]?)\b"))
            { unit = Unit.Gram; quantity *= 1000; }
            else if (Regex.IsMatch(rawLower, @"\b(dkg|dag|dekagram[yów]?)\b"))
            { unit = Unit.Gram; quantity *= 10; }
            else if (Regex.IsMatch(rawLower, @"\b(g|gram[yów]?)\b"))
                unit = Unit.Gram;
            else if (Regex.IsMatch(rawLower, @"\b(ml|mililitr[yów]?)\b"))
                unit = Unit.Milliliter;
            else if (Regex.IsMatch(rawLower, @"\b(l|litr[yów]?)\b"))
            { unit = Unit.Milliliter; quantity *= 1000; }
            else if (Regex.IsMatch(rawLower, @"\b(garść|garści)\b"))
            { unit = Unit.Gram; quantity *= 30; }
            else if (Regex.IsMatch(rawLower, @"\b(szczyp[aót]|szczypty|pinch)\b"))
            { unit = Unit.Gram; quantity = 1; }
            else
                unit = Unit.Piece;
        }

        var name = Regex.Replace(rawLower, @"^[\d\s/.,\-–()-]+", "").Trim();
        name = Regex.Replace(name,
            @"^(szklank[aię]|szkl|łyżk[iaę]|łyżek|łyż|łyżeczk[aię]|łyżeczek|łyżecz|" +
            @"kg|kilogram[yów]?|dkg|dag|dekagram[yów]?|g\b|gram[yów]?|" +
            @"ml|mililitr[yów]?|l\b|litr[yów]?|garść|garści|" +
            @"szczyp[aót]|szczypty|szt|sztuk[ai]?|opakowani[ae]|puszek|puszk[aię]|plastr[yów]?|" +
            @"tbsp|tsp|cup|tablespoon|teaspoon)\b", "").Trim();
        name = Regex.Replace(name, @"\(.*?\)", "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = raw;

        var matched = FindBestMatch(name, available);
        var ingredient = new Ingredient { Quantity = EnsurePositiveQuantity(quantity), Unit = unit };

        if (matched is not null)
        {
            ingredient.Name = matched.Name;
            ingredient.Calories = matched.Calories;
            ingredient.Protein = matched.Protein;
            ingredient.Fat = matched.Fat;
            ingredient.Carbs = matched.Carbs;
        }
        else
        {
            ingredient.Name = name;
        }

        return ingredient;
    }

    private static double EnsurePositiveQuantity(double quantity)
        => quantity <= 0 ? MinIngredientQuantity : quantity;

    private Ingredient? FindBestMatch(string ingredientName, List<Ingredient> available)
    {
        var norm = NormalizeName(ingredientName);
        if (string.IsNullOrWhiteSpace(norm)) return null;

        var exact = available.FirstOrDefault(i =>
            string.Equals(i.Name, ingredientName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var normExact = available.FirstOrDefault(i =>
            string.Equals(NormalizeName(i.Name), norm, StringComparison.OrdinalIgnoreCase));
        if (normExact is not null) return normExact;

        var contains = available.FirstOrDefault(i =>
        {
            var nDb = NormalizeName(i.Name);
            return (nDb.Length >= 4 && norm.Contains(nDb, StringComparison.OrdinalIgnoreCase)) ||
                   (nDb.Length >= 4 && nDb.Contains(norm, StringComparison.OrdinalIgnoreCase));
        });
        if (contains is not null) return contains;

        return available
            .Select(i => (Ingredient: i, Dist: LevenshteinDistance(norm, NormalizeName(i.Name))))
            .Where(x => x.Dist <= Math.Max(2, Math.Min(norm.Length, NormalizeName(x.Ingredient.Name).Length) / 3))
            .OrderBy(x => x.Dist)
            .FirstOrDefault().Ingredient;
    }

    // ─── Pomocnicze metody HTML ───────────────────────────────────────────────

    private static List<HtmlNode> FindAllNodesByKeywords(HtmlDocument doc, string[] keywords)
    {
        var nodes = doc.DocumentNode
            .SelectNodes("//*[self::h1 or self::h2 or self::h3 or self::h4 or self::strong]");
        if (nodes is null) return [];

        return nodes
            .Where(n =>
            {
                var normalized = NormalizeForSearch(HtmlEntity.DeEntitize(n.InnerText));
                return keywords.Any(kw =>
                    normalized.Contains(NormalizeForSearch(kw), StringComparison.Ordinal));
            })
            .ToList();
    }

    private static List<string>? ExtractLiTexts(HtmlNode listNode)
    {
        var items = listNode.SelectNodes(".//li");
        if (items is null) return null;
        var texts = items.Select(CleanNodeText).Where(t => t.Length > 5).ToList();
        return texts.Count > 0 ? texts : null;
    }

    private static string CleanNodeText(HtmlNode node)
    {
        var text = string.Join(" ", node.DescendantsAndSelf()
            .Where(n => n.NodeType == HtmlNodeType.Text)
            .Select(n => HtmlEntity.DeEntitize(n.InnerText).Trim())
            .Where(s => !string.IsNullOrEmpty(s)));
        return Regex.Replace(
            text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " "),
            @"\s{2,}", " ").Trim();
    }

    private static double ParseFirstNumber(string line)
    {
        var m = Regex.Match(line, @"\d+([.,]\d+)?");
        return m.Success && double.TryParse(m.Value.Replace(",", "."),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "…";

    // ─── Tytuł ────────────────────────────────────────────────────────────────

    private static string ExtractTitle(string url, HtmlDocument doc)
    {
        try
        {
            var h1El = doc.DocumentNode
                .SelectSingleNode("//h1[contains(@class,'elementor-heading-title')]");
            if (h1El is not null)
            {
                var t = HtmlEntity.DeEntitize(h1El.InnerText).Trim();
                Debug.WriteLine($"{Tag} Tytuł: <h1.elementor-heading-title> → \"{t}\"");
                return t;
            }

            var h1 = doc.DocumentNode.SelectSingleNode("//h1");
            if (h1 is not null && !string.IsNullOrWhiteSpace(h1.InnerText))
            {
                var t = HtmlEntity.DeEntitize(h1.InnerText).Trim();
                Debug.WriteLine($"{Tag} Tytuł: <h1> → \"{t}\"");
                return t;
            }

            var title = doc.DocumentNode.SelectSingleNode("//title");
            if (title is not null)
            {
                var t = HtmlEntity.DeEntitize(title.InnerText).Trim();
                foreach (var sep in new[] { " | ", " – ", " - ", " — " })
                {
                    var idx = t.IndexOf(sep, StringComparison.Ordinal);
                    if (idx > 0) { t = t[..idx].Trim(); break; }
                }
                if (!string.IsNullOrWhiteSpace(t))
                {
                    Debug.WriteLine($"{Tag} Tytuł: <title> → \"{t}\"");
                    return t;
                }
            }

            var slug = new Uri(url).Segments
                .LastOrDefault(s => s != "/")?.Trim('/').Replace("-", " ") ?? "Przepis";
            var result = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slug);
            Debug.WriteLine($"{Tag} Tytuł: URL slug → \"{result}\"");
            return result;
        }
        catch
        {
            Debug.WriteLine($"{Tag} Tytuł: wyjątek, fallback");
            return "Importowany przepis";
        }
    }

    // ─── Normalizacja ─────────────────────────────────────────────────────────

    private static string NormalizeName(string name)
        => name.ToLowerInvariant()
            .Replace("ą", "a").Replace("ć", "c").Replace("ę", "e")
            .Replace("ł", "l").Replace("ń", "n").Replace("ó", "o")
            .Replace("ś", "s").Replace("ź", "z").Replace("ż", "z")
            .Replace("sz", "s").Replace("cz", "c").Replace("ch", "h")
            .Replace("rz", "z").Replace("dz", "z")
            .Trim();

    private static string NormalizeForSearch(string s)
        => s.ToLowerInvariant()
            .Replace("ą", "a").Replace("ć", "c").Replace("ę", "e")
            .Replace("ł", "l").Replace("ń", "n").Replace("ó", "o")
            .Replace("ś", "s").Replace("ź", "z").Replace("ż", "z")
            .Trim();

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[a.Length, b.Length];
    }

    // ─── AI Fallback ──────────────────────────────────────────────────────────

    private async Task<LlmParsedRecipe?> TryEnhanceWithAiAsync(
        string title, List<string> unmatchedRawNames, List<Ingredient> db)
    {
        if (_aiService is null) return null;

        const string systemPrompt =
            "Jesteś ekspertem kulinarnym parsującym polskie przepisy.\n" +
            "Odpowiadaj WYŁĄCZNIE poprawnym JSON — zero preambuły, zero markdown, zero bloków kodu.\n\n" +
            "KONTRAKT WYJŚCIOWY:\n" +
            "{\n" +
            "  \"title\": \"string\",\n" +
            "  \"calories\": 0.0, \"protein\": 0.0, \"fat\": 0.0, \"carbs\": 0.0,\n" +
            "  \"ingredients\": [\n" +
            "    { \"raw_name\": \"string\", \"quantity\": 0.0, \"unit\": \"gram|milliliter|piece\", \"matched_db_name\": \"string|null\" }\n" +
            "  ]\n" +
            "}\n\n" +
            "ZASADY:\n" +
            "- unit: TYLKO \"gram\", \"milliliter\" lub \"piece\"\n" +
            "- 1 łyżka=15ml, 1 łyżeczka=5ml, 1 szklanka=250ml, 1 dag=10g, 1 kg=1000g\n" +
            "- Zakres \"12-15 szt\" -> srednia; ułamek \"1/2\" -> 0.5; \"(62g)\" -> quantity=62, unit=\"gram\"\n" +
            "- raw_name: oryginalna linia; matched_db_name: DOKŁADNA nazwa z bazy lub null\n" +
            "- calories/protein/fat/carbs: cały przepis lub 0.0";

        var dbNames = string.Join("\n", db.Select(i => $"- {i.Name}"));
        var ingredients = string.Join("\n", unmatchedRawNames.Select((l, i) => $"{i + 1}. {l}"));
        var userPrompt = $"PRZEPIS: {title}\n\nSKŁADNIKI:\n{ingredients}\n\nBAZA:\n{dbNames}";

        Debug.WriteLine($"{Tag} ┌─ AI SYSTEM PROMPT ─────────────────────────");
        foreach (var line in systemPrompt.Split('\n'))
            Debug.WriteLine($"{Tag} │ {line}");
        Debug.WriteLine($"{Tag} └────────────────────────────────────────────");

        Debug.WriteLine($"{Tag} ┌─ AI USER PROMPT ───────────────────────────");
        foreach (var line in userPrompt.Split('\n'))
            Debug.WriteLine($"{Tag} │ {line}");
        Debug.WriteLine($"{Tag} └────────────────────────────────────────────");

        var json = await _aiService.GetAIResponseAsync(systemPrompt, userPrompt, CancellationToken.None);

        Debug.WriteLine($"{Tag} ┌─ AI RAW RESPONSE ──────────────────────────");
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.WriteLine($"{Tag} │ (pusta odpowiedź)");
            Debug.WriteLine($"{Tag} └────────────────────────────────────────────");
            return null;
        }
        foreach (var line in json.Split('\n'))
            Debug.WriteLine($"{Tag} │ {line}");
        Debug.WriteLine($"{Tag} └────────────────────────────────────────────");

        json = Regex.Replace(json.Trim(), @"^```(?:json)?|```$", "", RegexOptions.Multiline).Trim();

        try
        {
            var result = JsonSerializer.Deserialize<LlmParsedRecipe>(json);
            Debug.WriteLine($"{Tag} AI JSON sparsowany: {result?.Ingredients.Count ?? 0} składników");
            return result;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"{Tag} AI JSON BŁĄD PARSOWANIA: {ex.Message}");
            Debug.WriteLine($"{Tag} Problematyczny JSON: {Truncate(json, 400)}");
            return null;
        }
    }
}

// ─── Modele LLM ──────────────────────────────────────────────────────────────

internal sealed record LlmParsedRecipe(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("calories")] double Calories,
    [property: JsonPropertyName("protein")] double Protein,
    [property: JsonPropertyName("fat")] double Fat,
    [property: JsonPropertyName("carbs")] double Carbs,
    [property: JsonPropertyName("ingredients")] IReadOnlyList<LlmParsedIngredient> Ingredients
);

internal sealed record LlmParsedIngredient(
    [property: JsonPropertyName("raw_name")] string RawName,
    [property: JsonPropertyName("quantity")] double Quantity,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("matched_db_name")] string? MatchedDbName
);
