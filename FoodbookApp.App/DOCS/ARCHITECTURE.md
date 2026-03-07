# ARCHITECTURE.md

<!-- This file will contain detailed information about the FoodBook App architecture, design patterns, and technical decisions -->

## 1. Cel dokumentu
Dokument opisuje architektur aplikacji FoodBook App: warstwy, wzorce, zalenoci, decyzje techniczne oraz wytyczne rozwoju i rozszerzalnoci. Ma by punktem odniesienia dla deweloperw i agentw AI generujcych kod.

---
## 2. Kontekst biznesowy
FoodBook App to wieloplatformowa (Android, iOS, Windows, macOS) aplikacja mobilna do:
- Zarzdzania baz przepisw (dodawanie, edycja, import z sieci, obliczenia makro)
- Planowania posikw w przedziaach dat z konfiguracj liczby posikw dziennie
- Generowania list zakupw na podstawie planw
- Archiwizacji i przywracania planw/list zakupw
- Zbierania i wywietlania statystyk ywieniowych (kalorie, biako, tuszcze, wglowodany)

---
## 3. Warstwy i podzia odpowiedzialnoci
| Warstwa | Zakres | Technologie / Artefakty |
|---------|--------|-------------------------|
| Prezentacji | UI, XAML Pages, Shell | .NET MAUI XAML, Shell, ResourceDictionary, Style, Converters |
| ViewModel (MVVM) | Logika prezentacji, komendy, stan UI | Klasy w `ViewModels/`, INotifyPropertyChanged |
| Serwisy Aplikacyjne | Operacje domenowe / agregacja danych | Klasy w `Services/` (RecipeService, PlannerService, PlanService, ShoppingListService, IngredientService) |
| Dostp do danych | ORM, persystencja | EF Core (AppDbContext), SQLite |
| Modele Domenowe | Encje i obiekty transferu | Klasy w `Models/` (Recipe, Ingredient, PlannedMeal, PlannerDay, Plan) |
| Infrastruktura | DI, lokalizacja, import, narzdzia | MauiProgram.cs, Localization*, RecipeImporter, konwertery |

---
## 4. Wzorce projektowe
- MVVM (oddzielenie UI od logiki  View ? ViewModel przez binding)
- Dependency Injection (constructor injection, rejestracje w `MauiProgram.cs`)
- Repository-like podejcie uproszczone (serwisy aplikacyjne wykorzystujce EF Core  bez osobnych repozytoriw dla prostoty)
- Observer (INotifyPropertyChanged w modelach dynamicznych: Ingredient, PlannedMeal)
- Command Pattern (ICommand w ViewModelach do akcji UI)
- Caching in-memory (PlannerViewModel  wasny cache zakresu dat)

---
## 5. Struktura projektu
Patrz `PROJECT-FILES.md`  gwne katalogi: `Models/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, `Localization/`, `Resources/`, `Converters/`, `Platforms/`.

---
## 6. Modele domenowe (skrt)
- `Recipe`: podstawowe makro wartoci (Calories, Protein, Fat, Carbs), lista Ingredient, IloscPorcji
- `Ingredient`: wartoci odywcze przypisane do jednostki/iloci, powizanie opcjonalne z Recipe
- `PlannedMeal`: referencja do Recipe (RecipeId), Date, Portions
- `PlannerDay`: agreguje kolekcj PlannedMeal dla konkretnej daty (UI helper)
- `Plan`: przedzia dat (StartDate, EndDate), IsArchived  logiczna archiwizacja

Relacje kluczowe: Recipe (1)  (N) Ingredient; Plan (N)  (N) PlannedMeal (powizanie via zakres dat i RecipeId logicznie, faktycznie PlannedMeal nie ma PlanId  identyfikacja poprzez daty w przedziale planu).

---
## 7. Dostp do danych
- ORM: Entity Framework Core 9 + SQLite
- Kontekst: `AppDbContext` (DbSet: Recipes, Ingredients, PlannedMeals, Plans)
- Migrations: mog by dodane (w README  instrukcja), obecnie inicjalizacja przez `EnsureCreated()` + seed
- Seed: `SeedData.InitializeAsync()` + `SeedIngredientsAsync()`  fallback mechanizmy (embedded resource ? app package ? filesystem ? fallback statyczny)
- Udoskonalenia przysze: wprowadzenie migracji kontrolowanych; osobne repozytoria (opcjonalnie); indeksy (np. na PlannedMeals.Date)

---
## 8. Serwisy aplikacyjne
| Serwis | Cel |
|--------|-----|
| IRecipeService / RecipeService | CRUD przepisw, pobieranie pojedynczego i listy |
| IIngredientService / IngredientService | Operacje na skadnikach globalnych i przypisanych do przepisu |
| IPlannerService / PlannerService | Zarzdzanie PlannedMeal (dodawanie/aktualizacja/usuwanie/pobieranie zakresu) |
| IPlanService / PlanService | Operacje na Plan (archiwizacja, kolizje dat) |
| IShoppingListService / ShoppingListService | Generowanie list zakupw na podstawie planu (agregacje skadnikw) |
| LocalizationService / LocalizationResourceManager | Lokalizacja tekstw (resx) i dynamiczne odwieanie |
| RecipeImporter | Import przepisu z URL (scraping i heurystyka) |

---
## 9. ViewModel (MVVM)  kluczowe aspekty
- Kady ViewModel izoluje logik UI: komendy, validacja, adowanie danych async
- `PlannerViewModel`: zarzdzanie datami, batch loading, progress reporting, caching
- `HomeViewModel`: agregacja statystyk (nutritional) + elastyczne zakresy dat
- `AddRecipeViewModel`: dwa tryby (manual / import), dynamiczne przeliczanie makro
- `ShoppingListViewModel` i `ArchiveViewModel`: filtrowanie aktywnych/archiwalnych planw

Konwencje: minimalizacja logiki w code-behind (XAML.cs)  ograniczona do delegowania zdarze do VM.

---
## 10. Nawigacja i routing
- `.NET MAUI Shell` (`AppShell.xaml`)  TabBar (Ingredients, Recipes, Home, Planner, ShoppingList)
- Dodatkowe widoki rejestrowane przez `Routing.RegisterRoute` w `MauiProgram.cs`
- Nawigacja wywoywana poprzez `Shell.Current.GoToAsync()` z parametrami Query (np. `?id=`)

---
## 11. Dependency Injection (DI)
Rejestracje w `MauiProgram.cs`:
- DbContext (AddDbContext)  scope per request (dla MAUI de facto per scope tworzony z providerem)
- Serwisy domenowe: `AddScoped` (operacje na danych), `RecipeImporter` + `HttpClient`
- ViewModels: mieszanka `AddScoped` / `AddTransient` (AddRecipePageViewModel jako transient  unikanie re-uycia stanu)
- Niektre VM jako Singleton (SettingsViewModel)  stan globalny aplikacji
- Lokalizacja: Singleton (`LocalizationService`, `LocalizationResourceManager`)

Uzasadnienie: transiency dla formularzy edycji (wiey stan), scope dla serwisw ktre korzystaj z DbContext.

---
## 12. Lokalizacja
- Folder `Localization/`  pary plikw resx (neutral + pl-PL) + wygenerowane Designer.cs
- Binding do zasobw poprzez niestandardowe rozszerzenie `TranslateExtension`
- Strings w UI nie powinny by hardcodowane (przysze refaktory: przenie jeszcze pozostae literalne teksty do zasobw)

---
## 13. Caching i optymalizacja adowania
- `PlannerViewModel` implementuje mechanizm cache (StartDate, EndDate, MealsPerDay) aby unikn ponownego pobierania
- Batch loading (paczki po 20 przepisw) + krtkie `Task.Delay` dla responsywnoci UI
- Potencjalne ulepszenia: MemoryCache dla Recipes globalnie, prefetching, kompresja/serializacja offline

---
## 14. Wzorce asynchroniczne
- Wszystkie operacje I/O (EF, HTTP, import) = async/await
- Progress UI przez waciwoci `LoadingStatus` / `LoadingProgress`
- Brak blokowania wtku UI  w krytycznych miejscach uyte krtkie opnienia dla pynnoci

---
## 15. Obsuga bdw i odporno
- Try/catch w punktach granicznych (LoadAsync, SeedData, Import, Nutrition calc)
- Logowanie przez `System.Diagnostics.Debug.WriteLine`
- User-friendly komunikaty przez `DisplayAlert`
- Moliwe przysze rozszerzenia: centralny logger (ILogger), telemetry, retry policy (Polly) dla HTTP

---
## 16. Walidacja
- `AddRecipeViewModel`  walidacja pl (konwersja liczb, makra, ilo porcji)
- Brak centralnego systemu walidacji  potencja na wprowadzenie FluentValidation / dedykowanego adaptera do MVVM

---
## 17. Bezpieczestwo i prywatno
- Lokalne dane w SQLite (brak jeszcze szyfrowania  moliwo uycia Microsoft.Data.Sqlite z hasem / rozwizania typu SQLCipher)
- Potencjalne klucze/API  przechowywa w SecureStorage
- Brak zewntrznego logowania / kont uytkownikw (aplikacja lokalna)
- Anonimizacja  brak danych osobowych (N/A na obecnym etapie)

---
## 18. Wydajno
Aktualne praktyki:
- Batch update listy recept
- Minimalizacja UI lock przez krtkie Task.Delay i ObservableCollection
- Unikanie zbdnych Include()  adowanie skadnikw przepisu dopiero gdy potrzebne (miejsce do poprawy  lazy load / selektywne zapytania)
Propozycje optymalizacji:
- Indeks na PlannedMeals.Date + RecipeId
- Prekomputacja makr / denormalizacja (cache sum skadnikw w Recipe)
- Virtualization / CollectionView optymalizacje
- Profilowanie zuycia pamici dla dugich sesji (szczeglnie event handler unsubscription)

---
## 19. Testy (plan)
- Unit: serwisy (mock DbContext via InMemory), ViewModel (mock services, FluentAssertions)
- Integration: real EF Core SQLite in-memory / plik testowy
- UI (opcjonalnie): .NET MAUI UITest / AppCenter
- Coverage target: 80% logicznych gazi w Services + krytyczne VM

---
## 20. Rozszerzalno
| Scenariusz | Zalecenie |
|------------|-----------|
| Dodanie API synchronizacji | Wydziel warstw Infrastructure/ApiClient + DTO Mappery |
| Wprowadzenie uytkownikw | Doda warstw auth + kontener SecureStorage, modele User/Profile |
| Analiza makro trendw | Wprowadzi modu AnalyticsService z cachingiem statystyk |
| AI Planner | Nowy serwis `IAiMealPlanningService` ? generuje list PlannedMeal; integracja w PlannerViewModel |
| Eksport / PDF | Adapter eksportujcy Plan + Ingredients do PDF/CSV (oddzielny modu) |

---
## 21. Decyzje architektoniczne (ADR skrt)
| ID | Decyzja | Status | Uzasadnienie |
|----|---------|--------|--------------|
| ADR-01 | EF Core + SQLite | Zaakceptowane | Prosta lokalna baza, wsparcie multi-platform |
| ADR-02 | Brak osobnych repozytoriw | Tymczasowe | Redukcja boilerplate  may zesp / MVP |
| ADR-03 | Shell Navigation | Zaakceptowane | Standaryzowany routing + TabBar multi-platform |
| ADR-04 | Manual caching w PlannerViewModel | Zaakceptowane | Prosty wzrost responsywnoci bez zewntrznych bibliotek |
| ADR-05 | Resource .resx lokalizacja | Zaakceptowane | Standard .NET, pniejsza atwa rozbudowa jzykw |
| ADR-06 | Batch UI loading | Zaakceptowane | Pynno UI na sabszych urzdzeniach |
| ADR-07 | Brak migracji przy starcie (EnsureCreated) | Do rewizji | Skrcenie czasu startu  docelowo wprowadzi migracje |

---
## 22. Ryzyka i mitigacje
| Ryzyko | Skutek | Mitigacja |
|--------|--------|----------|
| Brak migracji | Trudna ewolucja schematu | Wprowadzi EF Migrations + CI krok aktualizacji |
| Memory leak (event handlers) | Degradacja wydajnoci | Audyty, wzorzec WeakEvent / odsubskrybowanie w Reset |
| Brak szyfrowania DB | Moliwy dostp do danych | Szyfrowany provider / encja tylko z danymi niesensytywnymi |
| Zoone importy z sieci | Wraliwo na layout strony | Parser plug-in + testy kontraktowe |

---
## 23. Roadmap techniczny (skrt)
1. Wprowadzenie migracji + testy migracyjne
2. Dodanie testw jednostkowych (min. Services + PlannerViewModel)
3. Warstwa caching globalny (IMemoryCache lub LiteDB dla offline sync)
4. Modu AI planowania posikw (heurystyki + preferencje uytkownika)
5. Odchudzenie modeli UI (DTO zamiast bezporednich encji w bindingach  ograniczenie ryzyka side effects)
6. Audyt lokalizacji  pene pokrycie stringw

---
## 24. Konwencje kodu (skrt)
- C# 13, nullable enabled, `var` gdy typ oczywisty, jawne typy gdy zwiksza czytelno
- Nazwy async metod: sufiks `Async`
- Publiczne czony dokumentowane XML
- Brak logiki biznesowej w code-behind  tylko routing/UI glue

---
## 25. Dalsze rekomendacje
- Rozway wprowadzenie CommunityToolkit.Mvvm (atrybuty `[ObservableProperty]`, `[RelayCommand]`) dla redukcji boilerplate
- Centralny serwis logowania (ILogger<T>) zamiast Debug.WriteLine
- Mechanizm diff / change tracking dla szybszych zapisw (tylko zmienione encje)
- Utrzymywanie PURE metod dla oblicze makro (atwiejsze unit testy)

---
## 26. Sowniczek
| Pojcie | Definicja |
|---------|-----------|
| Plan | Zakres dat + meta (archiwizacja) determinujcy list zakupw |
| PlannedMeal | Element planu  posiek odwoujcy si do przepisu w dacie |
| Lista zakupw | Agregacja skadnikw z posikw w ramach planu |
| Makro | Kalorie, biako, tuszcz, wglowodany |

---
## 27. Aktualizacja dokumentu
Dokument aktualizowa przy kadej istotnej zmianie: dodanie nowej warstwy, refaktoryzacja modeli, zmiana strategii seedowania, dodanie migracji lub moduu AI.

---
**Ostatnia aktualizacja:** (auto)  dopasuj przy commicie.
**Waciciel dokumentu:** Zesp FoodBook App.

## Foodbook Templates
- Added `FoodbookTemplate` + `TemplateMeal` domain model for reusable meal-plan templates.
- Added `IFoodbookTemplateService`/`FoodbookTemplateService` for save/apply/update/delete flows.
- Planner now exposes menu actions to save current planner as template and apply template to a new plan.
