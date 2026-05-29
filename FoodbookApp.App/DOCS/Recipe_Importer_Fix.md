# RecipeImporter – AI Fallback: Plan Implementacji

## Kontekst

`RecipeImporter` parsuje przepisy z HTML. Lokalny parser mapuje składniki na bazę
przez dopasowanie nazw (exact → normalized → contains → Levenshtein).  
**AI fallback** uruchamia się tylko wtedy, gdy po lokalnym parsowaniu zostają
składniki bez dopasowania (`matched_db_name == null`).

---

## Cel

Po zakończeniu lokalnego parsowania:

1. Jeśli wszystkie składniki mają `matched_db_name` → **koniec, AI nie jest wywoływane**.
2. Jeśli ≥ 1 składnik nie ma dopasowania → wysłać do AI:
   - listę niedopasowanych surowych nazw składników,
   - pełną listę nazw składników z lokalnej bazy (z `ingredients.json`),
   - tytuł przepisu.
3. AI zwraca JSON z:
   - listą składników (`ingredients`) gotową do zmapowania na `Ingredient`,
   - nazwą przepisu (`title`).
4. Scalić wynik AI z wynikami lokalnego parsera.
5. Z wynikowych `Ingredient` zbudować encję `Recipe` i zapisać do bazy.

---

## Architektura

```
ImportFromUrlAsync(url)
    │
    ├─ HTML parsing (HtmlAgilityPack)
    ├─ ExtractRawIngredients()
    ├─ ParseIngredientLine() × N        ← lokalny parser
    ├─ FindBestMatch()                  ← Levenshtein / contains
    │
    ├─ unmatched.Count > 0 AND apiKey != null?
    │       │
    │       └─ TryEnhanceWithAiAsync()  ← AI fallback
    │               └─ OpenAI gpt-4.1-nano
    │                   system prompt → JSON kontrakt
    │                   user prompt   → niedopasowane składniki + baza
    │
    └─ ImportResult (title, makro, ingredients, unmatched, strategy)
            │
            └─ ToRecipe() → Recipe + List<Ingredient> → EF Core → SQLite
```

---

## Kontrakt JSON zwracany przez AI

AI musi zwrócić **wyłącznie** ten JSON (zero markdown, zero preambuły):

```json
{
  "title": "Nazwa przepisu",
  "calories": 0.0,
  "protein": 0.0,
  "fat": 0.0,
  "carbs": 0.0,
  "ingredients": [
    {
      "raw_name": "oryginalna nazwa ze strony",
      "quantity": 100.0,
      "unit": "gram",
      "matched_db_name": "Makaron"
    }
  ]
}
```

### Reguły kontraktu

| Pole | Typ | Zasada |
|---|---|---|
| `unit` | string | Tylko: `"gram"` / `"milliliter"` / `"piece"` |
| `quantity` | double | Po przeliczeniu jednostek (łyżka=15ml, szklanka=250ml, dag=10g) |
| `matched_db_name` | string\|null | Dokładna nazwa z bazy lub `null` jeśli brak dopasowania |
| `calories/protein/fat/carbs` | double | Wartości na CAŁY przepis; `0.0` jeśli nieznane |

---

## Prompt systemowy dla AI

```
Jesteś ekspertem kulinarnym parsującym polskie przepisy.
Odpowiadaj WYŁĄCZNIE poprawnym JSON — zero preambuły, zero markdown, zero bloków kodu.

KONTRAKT WYJŚCIOWY:
{
  "title": "string",
  "calories": 0.0,
  "protein": 0.0,
  "fat": 0.0,
  "carbs": 0.0,
  "ingredients": [
    {
      "raw_name": "string",
      "quantity": 0.0,
      "unit": "gram|milliliter|piece",
      "matched_db_name": "string|null"
    }
  ]
}

ZASADY PARSOWANIA SKŁADNIKÓW:
- unit: TYLKO "gram", "milliliter" lub "piece"
- Przeliczenia:
    1 łyżka stołowa  = 15 ml  → unit="milliliter", quantity *= 15
    1 łyżeczka       = 5 ml   → unit="milliliter", quantity *= 5
    1 szklanka       = 250 ml → unit="milliliter", quantity *= 250
    1 dag / 1 dkg    = 10 g   → unit="gram",       quantity *= 10
    1 kg             = 1000 g → unit="gram",       quantity *= 1000
    garść            ≈ 30 g   → unit="gram",       quantity = 30
    plaster/plasterek         → unit="piece"
- Zakres "12–15 szt" → quantity = średnia (13.5), unit="piece"
- Ułamek "1/2" → quantity = 0.5 (potem przelicz jednostkę)
- Waga w nawiasie "(62g)" → quantity=62, unit="gram" (override wszystkiego)
- raw_name: sama nazwa składnika bez ilości, jednostki i komentarzy w nawiasach
- matched_db_name: DOKŁADNA nazwa z podanej listy bazy (uwzględnij polską fleksję PL);
  np. "spaghetti" → "Makaron", "pomidorki cherry" → "Pomidorki koktajlowe";
  jeśli brak dopasowania → null
- calories/protein/fat/carbs: wartości na CAŁY przepis; jeśli nieznane → 0.0
- Jeśli przepis podaje wartości na porcję → pomnóż przez liczbę porcji
```

---

## Zmiany w istniejących klasach

### 1. `RecipeImporterOptions` — dodaj klucz OpenAI

```csharp
public sealed class RecipeImporterOptions
{
    /// <summary>
    /// Klucz API OpenAI. Null = wyłącza AI fallback.
    /// NIGDY nie wpisuj klucza w kodzie — używaj SecureStorage!
    /// Zapis:   await SecureStorage.SetAsync("openai_api_key", value);
    /// Odczyt:  await SecureStorage.GetAsync("openai_api_key");
    /// </summary>
    public string? OpenAiApiKey { get; init; }

    /// <summary>
    /// Model OpenAI. Domyślnie gpt-4.1-nano (najtańszy, wystarczający).
    /// Alternatywy: "gpt-4o-mini", "gpt-4.1-mini"
    /// </summary>
    public string LlmModel { get; init; } = "gpt-4.1-nano";

    /// <summary>Endpoint OpenAI chat completions.</summary>
    public string LlmEndpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
}
```

### 2. `LlmParsedRecipe` — rozszerz o pola przepisu

```csharp
// Zastąp obecny file record LlmParsedRecipe:
file sealed record LlmParsedRecipe(
    [property: JsonPropertyName("title")]       string?  Title,
    [property: JsonPropertyName("calories")]    double   Calories,
    [property: JsonPropertyName("protein")]     double   Protein,
    [property: JsonPropertyName("fat")]         double   Fat,
    [property: JsonPropertyName("carbs")]       double   Carbs,
    [property: JsonPropertyName("ingredients")] IReadOnlyList<LlmParsedIngredient> Ingredients
);
```

### 3. `TryEnhanceWithAiAsync` — nowa metoda (zastępuje GitHub Models)

```csharp
// Sygnatura:
private async Task<ImportResult?> TryEnhanceWithAiAsync(
    string           title,
    List<string>     unmatchedRawNames,   // tylko niedopasowane linie
    List<string>     allRawLines,         // wszystkie linie (dla kontekstu)
    List<Ingredient> db,
    CancellationToken ct)

// Endpoint: _options.LlmEndpoint  (https://api.openai.com/v1/chat/completions)
// Auth:     Authorization: Bearer {_options.OpenAiApiKey}
// Body:     OpenAiRequest z response_format: { type: "json_object" }

// User prompt zawiera:
// 1. TYTUŁ PRZEPISU: {title}
// 2. SKŁADNIKI DO DOPASOWANIA (tylko niedopasowane):
//    numerowana lista unmatchedRawNames
// 3. DOSTĘPNE NAZWY W BAZIE:
//    "- Nazwa1\n- Nazwa2\n..." z db.Select(i => i.Name)

// Po deserializacji LlmParsedRecipe:
// - Scal z już-dopasowanymi lokalnie składnikami
// - Użyj parsed.Calories/Protein/Fat/Carbs (jeśli > 0) lub zachowaj wartości z HTML
// - Zwróć ImportResult z ImportStrategy.AiFallback
```

### 4. `ImportStrategy` — dodaj nową wartość

```csharp
public enum ImportStrategy
{
    LocalParser,
    GitHubModelsLlm,   // można zostawić dla kompatybilności
    AiFallback         // nowa — OpenAI
}
```

### 5. `ImportFromUrlAsync` — logika scalania

```csharp
// Po lokalnym parsowaniu:
var alreadyMatched = parsed.Where(p => p.MatchedDbName is not null).ToList();
var unmatchedLines = parsed
    .Where(p => p.MatchedDbName is null)
    .Select(p => p.RawName)
    .ToList();

if (unmatchedLines.Count > 0 && !string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
{
    var aiResult = await TryEnhanceWithAiAsync(
        title, unmatchedLines, rawLines, availableIngredients, ct);

    if (aiResult is not null)
    {
        // Scal: lokalnie dopasowane + AI dopasowane
        var merged = alreadyMatched
            .Concat(aiResult.Ingredients)
            .ToList();

        return new ImportResult(
            aiResult.Title ?? title,
            aiResult.Calories > 0 ? aiResult.Calories : calories,
            aiResult.Protein > 0 ? aiResult.Protein  : protein,
            aiResult.Fat     > 0 ? aiResult.Fat      : fat,
            aiResult.Carbs   > 0 ? aiResult.Carbs    : carbs,
            merged,
            aiResult.UnmatchedIngredients,
            ImportStrategy.AiFallback);
    }
}
```

---

## Rejestracja w `MauiProgram.cs`

```csharp
// Wczytaj klucz z SecureStorage (async w OnStart lub lazy)
var openAiKey = await SecureStorage.GetAsync("openai_api_key");

builder.Services.AddSingleton(new RecipeImporterOptions
{
    OpenAiApiKey = openAiKey,
    LlmModel     = "gpt-4.1-nano"
});

builder.Services.AddScoped<RecipeImporter>();
builder.Services.AddHttpClient<RecipeImporter>();
```

---

## Bezpieczeństwo kluczy API

```csharp
// ZAPIS klucza (np. ekran Ustawień):
await SecureStorage.SetAsync("openai_api_key", keyFromUser);

// ODCZYT przy starcie (App.xaml.cs lub MauiProgram.cs):
var key = await SecureStorage.GetAsync("openai_api_key");

// NIGDY:
// ❌ const string key = "sk-proj-...";   ← hardcode w kodzie
// ❌ appsettings.json                    ← widoczne w paczce APK
// ❌ wiadomości do AI / commit messages  ← traktowane jako skompromitowane
```

---

## Mapowanie `ImportResult` → `Recipe` + `List<Ingredient>` → EF Core

Metoda `ToRecipe()` już istnieje w `RecipeImporter`. Wywołanie w `AddRecipeViewModel`:

```csharp
// W AddRecipeViewModel.ImportRecipeAsync():
var importResult = await _recipeImporter.ImportFromUrlAsync(ImportUrl, ct);
var availableIngredients = await _ingredientService.GetIngredientsAsync();

var recipe = _recipeImporter.ToRecipe(importResult, availableIngredients);

// Zapis do bazy przez serwis:
await _recipeService.AddRecipeAsync(recipe);

// Opcjonalnie zapisz nowe składniki (te z matched_db_name == null):
foreach (var unmatched in importResult.UnmatchedIngredients)
{
    // Możesz pokazać użytkownikowi listę nierozpoznanych składników
    // lub zapisać je jako nowe Ingredient z wartościami 0
}
```

---

## Checklist dla Copilota

- [ ] Zaktualizować `RecipeImporterOptions` — zamienić `GitHubPat` na `OpenAiApiKey`
- [ ] Rozszerzyć `LlmParsedRecipe` o `Title`, `Calories`, `Protein`, `Fat`, `Carbs`
- [ ] Dodać `ImportStrategy.AiFallback`
- [ ] Napisać `TryEnhanceWithAiAsync` z powyższą sygnaturą i promptem systemowym
- [ ] Zaktualizować `ImportFromUrlAsync` — scalanie lokalnych + AI wyników
- [ ] Zaktualizować `MauiProgram.cs` — rejestracja z `OpenAiApiKey`
- [ ] W `AddRecipeViewModel` wywołać `ToRecipe()` i zapisać przez `IRecipeService`
- [ ] Usunąć stare referencje do GitHub Models / `GitHubPat` (lub zostawić jako alternatywę)
- [ ] Testy jednostkowe dla `TryEnhanceWithAiAsync` (mock HttpClient)

---

## Przykładowy user prompt (runtime)

```
PRZEPIS: Spaghetti Carbonara

SKŁADNIKI DO DOPASOWANIA:
1. guanciale
2. pecorino romano
3. żółtka jaj

DOSTĘPNE NAZWY W BAZIE:
- Boczek
- Parmezan
- Jajka
- Makaron
- Oliwa z oliwek
- Czosnek
[...pełna lista z bazy...]
```

**Oczekiwana odpowiedź AI:**

```json
{
  "title": "Spaghetti Carbonara",
  "calories": 0.0,
  "protein": 0.0,
  "fat": 0.0,
  "carbs": 0.0,
  "ingredients": [
    { "raw_name": "guanciale",        "quantity": 200.0, "unit": "gram",  "matched_db_name": "Boczek"   },
    { "raw_name": "pecorino romano",  "quantity": 100.0, "unit": "gram",  "matched_db_name": "Parmezan" },
    { "raw_name": "żółtka jaj",       "quantity": 4.0,   "unit": "piece", "matched_db_name": "Jajka"    }
  ]
}
```