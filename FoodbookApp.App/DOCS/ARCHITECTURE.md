# ARCHITECTURE.md

<!-- This file will contain detailed information about the FoodBook App architecture, design patterns, and technical decisions -->

## 1. Cel dokumentu
Dokument opisuje architekturę aplikacji FoodBook App: warstwy, wzorce, zależności, decyzje techniczne oraz wytyczne rozwoju i rozszerzalności. Ma być punktem odniesienia dla deweloperów i agentów AI generujących kod.

---
## 2. Kontekst biznesowy
FoodBook App to wieloplatformowa (Android, iOS, Windows, macOS) aplikacja mobilna do:
- Zarządzania bazą przepisów (dodawanie, edycja, import z sieci, obliczenia makro)
- Planowania posiłków w przedziałach dat z konfiguracją liczby posiłków dziennie
- Generowania list zakupów na podstawie planów
- Archiwizacji i przywracania planów/list zakupów
- Zbierania i wyświetlania statystyk żywieniowych (kalorie, białko, tłuszcze, węglowodany)

---
## 3. Warstwy i podział odpowiedzialności
| Warstwa | Zakres | Technologie / Artefakty |
|---------|--------|-------------------------|
| Prezentacji | UI, XAML Pages, Shell | .NET MAUI XAML, Shell, ResourceDictionary, Style, Converters |
| ViewModel (MVVM) | Logika prezentacji, komendy, stan UI | Klasy w `ViewModels/`, INotifyPropertyChanged |
| Serwisy Aplikacyjne | Operacje domenowe / agregacja danych | Klasy w `Services/` (RecipeService, PlannerService, PlanService, ShoppingListService, IngredientService) |
| Dostęp do danych | ORM, persystencja | EF Core (AppDbContext), SQLite |
| Modele Domenowe | Encje i obiekty transferu | Klasy w `Models/` (Recipe, Ingredient, PlannedMeal, PlannerDay, Plan) |
| Infrastruktura | DI, lokalizacja, import, narzędzia | MauiProgram.cs, Localization*, RecipeImporter, konwertery |

---
## 4. Wzorce projektowe
- MVVM (oddzielenie UI od logiki – View ? ViewModel przez binding)
- Dependency Injection (constructor injection, rejestracje w `MauiProgram.cs`)
- Repository-like podejście uproszczone (serwisy aplikacyjne wykorzystujące EF Core – bez osobnych repozytoriów dla prostoty)
- Observer (INotifyPropertyChanged w modelach dynamicznych: Ingredient, PlannedMeal)
- Command Pattern (ICommand w ViewModelach do akcji UI)
- Caching in-memory (PlannerViewModel – własny cache zakresu dat)

---
## 5. Struktura projektu
Patrz `PROJECT-FILES.md` – główne katalogi: `Models/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, `Localization/`, `Resources/`, `Converters/`, `Platforms/`.

---
## 6. Modele domenowe (skrót)
- `Recipe`: podstawowe makro wartości (Calories, Protein, Fat, Carbs), lista Ingredient, IloscPorcji
- `Ingredient`: wartości odżywcze przypisane do jednostki/ilości, powiązanie opcjonalne z Recipe
- `PlannedMeal`: referencja do Recipe (RecipeId), Date, Portions
- `PlannerDay`: agreguje kolekcję PlannedMeal dla konkretnej daty (UI helper)
- `Plan`: przedział dat (StartDate, EndDate), IsArchived – logiczna archiwizacja

Relacje kluczowe: Recipe (1) — (N) Ingredient; Plan (N) — (N) PlannedMeal (powiązanie via zakres dat i RecipeId logicznie, faktycznie PlannedMeal nie ma PlanId – identyfikacja poprzez daty w przedziale planu).

---
## 7. Dostęp do danych
- ORM: Entity Framework Core 9 + SQLite
- Kontekst: `AppDbContext` (DbSet: Recipes, Ingredients, PlannedMeals, Plans)
- Migrations: mogą być dodane (w README – instrukcja), obecnie inicjalizacja przez `EnsureCreated()` + seed
- Seed: `SeedData.InitializeAsync()` + `SeedIngredientsAsync()` – fallback mechanizmy (embedded resource ? app package ? filesystem ? fallback statyczny)
- Udoskonalenia przyszłe: wprowadzenie migracji kontrolowanych; osobne repozytoria (opcjonalnie); indeksy (np. na PlannedMeals.Date)

---
## 8. Serwisy aplikacyjne
| Serwis | Cel |
|--------|-----|
| IRecipeService / RecipeService | CRUD przepisów, pobieranie pojedynczego i listy |
| IIngredientService / IngredientService | Operacje na składnikach globalnych i przypisanych do przepisu |
| IPlannerService / PlannerService | Zarządzanie PlannedMeal (dodawanie/aktualizacja/usuwanie/pobieranie zakresu) |
| IPlanService / PlanService | Operacje na Plan (archiwizacja, kolizje dat) |
| IShoppingListService / ShoppingListService | Generowanie list zakupów na podstawie planu (agregacje składników) |
| LocalizationService / LocalizationResourceManager | Lokalizacja tekstów (resx) i dynamiczne odświeżanie |
| RecipeImporter | Import przepisu z URL (scraping i heurystyka) |

---
## 9. ViewModel (MVVM) – kluczowe aspekty
- Każdy ViewModel izoluje logikę UI: komendy, validacja, ładowanie danych async
- `PlannerViewModel`: zarządzanie datami, batch loading, progress reporting, caching
- `HomeViewModel`: agregacja statystyk (nutritional) + elastyczne zakresy dat
- `AddRecipeViewModel`: dwa tryby (manual / import), dynamiczne przeliczanie makro
- `ShoppingListViewModel` i `ArchiveViewModel`: filtrowanie aktywnych/archiwalnych planów

Konwencje: minimalizacja logiki w code-behind (XAML.cs) – ograniczona do delegowania zdarzeń do VM.

---
## 10. Nawigacja i routing
- `.NET MAUI Shell` (`AppShell.xaml`) – TabBar (Ingredients, Recipes, Home, Planner, ShoppingList)
- Dodatkowe widoki rejestrowane przez `Routing.RegisterRoute` w `MauiProgram.cs`
- Nawigacja wywoływana poprzez `Shell.Current.GoToAsync()` z parametrami Query (np. `?id=`)

---
## 11. Dependency Injection (DI)
Rejestracje w `MauiProgram.cs`:
- DbContext (AddDbContext) – scope per request (dla MAUI de facto per scope tworzony z providerem)
- Serwisy domenowe: `AddScoped` (operacje na danych), `RecipeImporter` + `HttpClient`
- ViewModels: mieszanka `AddScoped` / `AddTransient` (AddRecipePageViewModel jako transient – unikanie re-użycia stanu)
- Niektóre VM jako Singleton (SettingsViewModel) – stan globalny aplikacji
- Lokalizacja: Singleton (`LocalizationService`, `LocalizationResourceManager`)

Uzasadnienie: transiency dla formularzy edycji (świeży stan), scope dla serwisów które korzystają z DbContext.

---
## 12. Lokalizacja
- Folder `Localization/` – pary plików resx (neutral + pl-PL) + wygenerowane Designer.cs
- Binding do zasobów poprzez niestandardowe rozszerzenie `TranslateExtension`
- Strings w UI nie powinny być hardcodowane (przyszłe refaktory: przenieść jeszcze pozostałe literalne teksty do zasobów)

---
## 13. Caching i optymalizacja ładowania
- `PlannerViewModel` implementuje mechanizm cache (StartDate, EndDate, MealsPerDay) aby uniknąć ponownego pobierania
- Batch loading (paczki po 20 przepisów) + krótkie `Task.Delay` dla responsywności UI
- Potencjalne ulepszenia: MemoryCache dla Recipes globalnie, prefetching, kompresja/serializacja offline

---
## 14. Wzorce asynchroniczne
- Wszystkie operacje I/O (EF, HTTP, import) = async/await
- Progress UI przez właściwości `LoadingStatus` / `LoadingProgress`
- Brak blokowania wątku UI – w krytycznych miejscach użyte krótkie opóźnienia dla płynności

---
## 15. Obsługa błędów i odporność
- Try/catch w punktach granicznych (LoadAsync, SeedData, Import, Nutrition calc)
- Logowanie przez `System.Diagnostics.Debug.WriteLine`
- User-friendly komunikaty przez `DisplayAlert`
- Możliwe przyszłe rozszerzenia: centralny logger (ILogger), telemetry, retry policy (Polly) dla HTTP

---
## 16. Walidacja
- `AddRecipeViewModel` – walidacja pól (konwersja liczb, makra, ilość porcji)
- Brak centralnego systemu walidacji – potencjał na wprowadzenie FluentValidation / dedykowanego adaptera do MVVM

---
## 17. Bezpieczeństwo i prywatność
- Lokalne dane w SQLite (brak jeszcze szyfrowania – możliwość użycia Microsoft.Data.Sqlite z hasłem / rozwiązania typu SQLCipher)
- Potencjalne klucze/API – przechowywać w SecureStorage
- Brak zewnętrznego logowania / kont użytkowników (aplikacja lokalna)
- Anonimizacja – brak danych osobowych (N/A na obecnym etapie)

---
## 18. Wydajność
Aktualne praktyki:
- Batch update listy recept
- Minimalizacja UI lock przez krótkie Task.Delay i ObservableCollection
- Unikanie zbędnych Include() – ładowanie składników przepisu dopiero gdy potrzebne (miejsce do poprawy – lazy load / selektywne zapytania)
Propozycje optymalizacji:
- Indeks na PlannedMeals.Date + RecipeId
- Prekomputacja makr / denormalizacja (cache sum składników w Recipe)
- Virtualization / CollectionView optymalizacje
- Profilowanie zużycia pamięci dla długich sesji (szczególnie event handler unsubscription)

---
## 19. Testy (plan)
- Unit: serwisy (mock DbContext via InMemory), ViewModel (mock services, FluentAssertions)
- Integration: real EF Core SQLite in-memory / plik testowy
- UI (opcjonalnie): .NET MAUI UITest / AppCenter
- Coverage target: 80% logicznych gałęzi w Services + krytyczne VM

---
## 20. Rozszerzalność
| Scenariusz | Zalecenie |
|------------|-----------|
| Dodanie API synchronizacji | Wydziel warstwę Infrastructure/ApiClient + DTO Mappery |
| Wprowadzenie użytkowników | Dodać warstwę auth + kontener SecureStorage, modele User/Profile |
| Analiza makro trendów | Wprowadzić moduł AnalyticsService z cachingiem statystyk |
| AI Planner | Nowy serwis `IAiMealPlanningService` ? generuje listę PlannedMeal; integracja w PlannerViewModel |
| Eksport / PDF | Adapter eksportujący Plan + Ingredients do PDF/CSV (oddzielny moduł) |

---
## 21. Decyzje architektoniczne (ADR skrót)
| ID | Decyzja | Status | Uzasadnienie |
|----|---------|--------|--------------|
| ADR-01 | EF Core + SQLite | Zaakceptowane | Prosta lokalna baza, wsparcie multi-platform |
| ADR-02 | Brak osobnych repozytoriów | Tymczasowe | Redukcja boilerplate – mały zespół / MVP |
| ADR-03 | Shell Navigation | Zaakceptowane | Standaryzowany routing + TabBar multi-platform |
| ADR-04 | Manual caching w PlannerViewModel | Zaakceptowane | Prosty wzrost responsywności bez zewnętrznych bibliotek |
| ADR-05 | Resource .resx lokalizacja | Zaakceptowane | Standard .NET, późniejsza łatwa rozbudowa języków |
| ADR-06 | Batch UI loading | Zaakceptowane | Płynność UI na słabszych urządzeniach |
| ADR-07 | Brak migracji przy starcie (EnsureCreated) | Do rewizji | Skrócenie czasu startu – docelowo wprowadzić migracje |

---
## 22. Ryzyka i mitigacje
| Ryzyko | Skutek | Mitigacja |
|--------|--------|----------|
| Brak migracji | Trudna ewolucja schematu | Wprowadzić EF Migrations + CI krok aktualizacji |
| Memory leak (event handlers) | Degradacja wydajności | Audyty, wzorzec WeakEvent / odsubskrybowanie w Reset |
| Brak szyfrowania DB | Możliwy dostęp do danych | Szyfrowany provider / encja tylko z danymi niesensytywnymi |
| Złożone importy z sieci | Wrażliwość na layout strony | Parser plug-in + testy kontraktowe |

---
## 23. Roadmap techniczny (skrót)
1. Wprowadzenie migracji + testy migracyjne
2. Dodanie testów jednostkowych (min. Services + PlannerViewModel)
3. Warstwa caching globalny (IMemoryCache lub LiteDB dla offline sync)
4. Moduł AI planowania posiłków (heurystyki + preferencje użytkownika)
5. Odchudzenie modeli UI (DTO zamiast bezpośrednich encji w bindingach – ograniczenie ryzyka side effects)
6. Audyt lokalizacji – pełne pokrycie stringów

---
## 24. Konwencje kodu (skrót)
- C# 13, nullable enabled, `var` gdy typ oczywisty, jawne typy gdy zwiększa czytelność
- Nazwy async metod: sufiks `Async`
**Waciciel dokumentu:** Zesp FoodBook App.
## Foodbook templates module (2026-03)
- Added `FoodbookTemplate` aggregate with child `TemplateMeal` for reusable planner blueprints.
- Planner now supports premium-gated save-as-template and applying templates to generate `Plan` + `PlannedMeal` entries.
- Planner lists screen now exposes tabs for planners and templates management.
- Brak logiki biznesowej w code-behind – tylko routing/UI glue

---
## 25. Dalsze rekomendacje
- Rozważyć wprowadzenie CommunityToolkit.Mvvm (atrybuty `[ObservableProperty]`, `[RelayCommand]`) dla redukcji boilerplate
- Centralny serwis logowania (ILogger<T>) zamiast Debug.WriteLine
- Mechanizm diff / change tracking dla szybszych zapisów (tylko zmienione encje)
- Utrzymywanie PURE metod dla obliczeń makro (łatwiejsze unit testy)

---
## 26. Słowniczek
| Pojęcie | Definicja |
|---------|-----------|
| Plan | Zakres dat + meta (archiwizacja) determinujący listę zakupów |
| PlannedMeal | Element planu – posiłek odwołujący się do przepisu w dacie |
| Lista zakupów | Agregacja składników z posiłków w ramach planu |
| Makro | Kalorie, białko, tłuszcz, węglowodany |

---
## 27. Aktualizacja dokumentu
Dokument aktualizować przy każdej istotnej zmianie: dodanie nowej warstwy, refaktoryzacja modeli, zmiana strategii seedowania, dodanie migracji lub modułu AI.

---
**Ostatnia aktualizacja:** (auto) – dopasuj przy commicie.  
**Właściciel dokumentu:** Zespół FoodBook App.