# ARCHITECTURE.md

<!-- This file will contain detailed information about the FoodBook App architecture, design patterns, and technical decisions -->

## 1. Cel dokumentu
Dokument opisuje architekturê aplikacji FoodBook App: warstwy, wzorce, zale¿noœci, decyzje techniczne oraz wytyczne rozwoju i rozszerzalnoœci. Ma byæ punktem odniesienia dla deweloperów i agentów AI generuj¹cych kod.

---
## 2. Kontekst biznesowy
FoodBook App to wieloplatformowa (Android, iOS, Windows, macOS) aplikacja mobilna do:
- Zarz¹dzania baz¹ przepisów (dodawanie, edycja, import z sieci, obliczenia makro)
- Planowania posi³ków w przedzia³ach dat z konfiguracj¹ liczby posi³ków dziennie
- Generowania list zakupów na podstawie planów
- Archiwizacji i przywracania planów/list zakupów
- Zbierania i wyœwietlania statystyk ¿ywieniowych (kalorie, bia³ko, t³uszcze, wêglowodany)

---
## 3. Warstwy i podzia³ odpowiedzialnoœci
| Warstwa | Zakres | Technologie / Artefakty |
|---------|--------|-------------------------|
| Prezentacji | UI, XAML Pages, Shell | .NET MAUI XAML, Shell, ResourceDictionary, Style, Converters |
| ViewModel (MVVM) | Logika prezentacji, komendy, stan UI | Klasy w `ViewModels/`, INotifyPropertyChanged |
| Serwisy Aplikacyjne | Operacje domenowe / agregacja danych | Klasy w `Services/` (RecipeService, PlannerService, PlanService, ShoppingListService, IngredientService) |
| Dostêp do danych | ORM, persystencja | EF Core (AppDbContext), SQLite |
| Modele Domenowe | Encje i obiekty transferu | Klasy w `Models/` (Recipe, Ingredient, PlannedMeal, PlannerDay, Plan) |
| Infrastruktura | DI, lokalizacja, import, narzêdzia | MauiProgram.cs, Localization*, RecipeImporter, konwertery |

---
## 4. Wzorce projektowe
- MVVM (oddzielenie UI od logiki – View ? ViewModel przez binding)
- Dependency Injection (constructor injection, rejestracje w `MauiProgram.cs`)
- Repository-like podejœcie uproszczone (serwisy aplikacyjne wykorzystuj¹ce EF Core – bez osobnych repozytoriów dla prostoty)
- Observer (INotifyPropertyChanged w modelach dynamicznych: Ingredient, PlannedMeal)
- Command Pattern (ICommand w ViewModelach do akcji UI)
- Caching in-memory (PlannerViewModel – w³asny cache zakresu dat)

---
## 5. Struktura projektu
Patrz `PROJECT-FILES.md` – g³ówne katalogi: `Models/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, `Localization/`, `Resources/`, `Converters/`, `Platforms/`.

---
## 6. Modele domenowe (skrót)
- `Recipe`: podstawowe makro wartoœci (Calories, Protein, Fat, Carbs), lista Ingredient, IloscPorcji
- `Ingredient`: wartoœci od¿ywcze przypisane do jednostki/iloœci, powi¹zanie opcjonalne z Recipe
- `PlannedMeal`: referencja do Recipe (RecipeId), Date, Portions
- `PlannerDay`: agreguje kolekcjê PlannedMeal dla konkretnej daty (UI helper)
- `Plan`: przedzia³ dat (StartDate, EndDate), IsArchived – logiczna archiwizacja

Relacje kluczowe: Recipe (1) — (N) Ingredient; Plan (N) — (N) PlannedMeal (powi¹zanie via zakres dat i RecipeId logicznie, faktycznie PlannedMeal nie ma PlanId – identyfikacja poprzez daty w przedziale planu).

---
## 7. Dostêp do danych
- ORM: Entity Framework Core 9 + SQLite
- Kontekst: `AppDbContext` (DbSet: Recipes, Ingredients, PlannedMeals, Plans)
- Migrations: mog¹ byæ dodane (w README – instrukcja), obecnie inicjalizacja przez `EnsureCreated()` + seed
- Seed: `SeedData.InitializeAsync()` + `SeedIngredientsAsync()` – fallback mechanizmy (embedded resource ? app package ? filesystem ? fallback statyczny)
- Udoskonalenia przysz³e: wprowadzenie migracji kontrolowanych; osobne repozytoria (opcjonalnie); indeksy (np. na PlannedMeals.Date)

---
## 8. Serwisy aplikacyjne
| Serwis | Cel |
|--------|-----|
| IRecipeService / RecipeService | CRUD przepisów, pobieranie pojedynczego i listy |
| IIngredientService / IngredientService | Operacje na sk³adnikach globalnych i przypisanych do przepisu |
| IPlannerService / PlannerService | Zarz¹dzanie PlannedMeal (dodawanie/aktualizacja/usuwanie/pobieranie zakresu) |
| IPlanService / PlanService | Operacje na Plan (archiwizacja, kolizje dat) |
| IShoppingListService / ShoppingListService | Generowanie list zakupów na podstawie planu (agregacje sk³adników) |
| LocalizationService / LocalizationResourceManager | Lokalizacja tekstów (resx) i dynamiczne odœwie¿anie |
| RecipeImporter | Import przepisu z URL (scraping i heurystyka) |

---
## 9. ViewModel (MVVM) – kluczowe aspekty
- Ka¿dy ViewModel izoluje logikê UI: komendy, validacja, ³adowanie danych async
- `PlannerViewModel`: zarz¹dzanie datami, batch loading, progress reporting, caching
- `HomeViewModel`: agregacja statystyk (nutritional) + elastyczne zakresy dat
- `AddRecipeViewModel`: dwa tryby (manual / import), dynamiczne przeliczanie makro
- `ShoppingListViewModel` i `ArchiveViewModel`: filtrowanie aktywnych/archiwalnych planów

Konwencje: minimalizacja logiki w code-behind (XAML.cs) – ograniczona do delegowania zdarzeñ do VM.

---
## 10. Nawigacja i routing
- `.NET MAUI Shell` (`AppShell.xaml`) – TabBar (Ingredients, Recipes, Home, Planner, ShoppingList)
- Dodatkowe widoki rejestrowane przez `Routing.RegisterRoute` w `MauiProgram.cs`
- Nawigacja wywo³ywana poprzez `Shell.Current.GoToAsync()` z parametrami Query (np. `?id=`)

---
## 11. Dependency Injection (DI)
Rejestracje w `MauiProgram.cs`:
- DbContext (AddDbContext) – scope per request (dla MAUI de facto per scope tworzony z providerem)
- Serwisy domenowe: `AddScoped` (operacje na danych), `RecipeImporter` + `HttpClient`
- ViewModels: mieszanka `AddScoped` / `AddTransient` (AddRecipePageViewModel jako transient – unikanie re-u¿ycia stanu)
- Niektóre VM jako Singleton (SettingsViewModel) – stan globalny aplikacji
- Lokalizacja: Singleton (`LocalizationService`, `LocalizationResourceManager`)

Uzasadnienie: transiency dla formularzy edycji (œwie¿y stan), scope dla serwisów które korzystaj¹ z DbContext.

---
## 12. Lokalizacja
- Folder `Localization/` – pary plików resx (neutral + pl-PL) + wygenerowane Designer.cs
- Binding do zasobów poprzez niestandardowe rozszerzenie `TranslateExtension`
- Strings w UI nie powinny byæ hardcodowane (przysz³e refaktory: przenieœæ jeszcze pozosta³e literalne teksty do zasobów)

---
## 13. Caching i optymalizacja ³adowania
- `PlannerViewModel` implementuje mechanizm cache (StartDate, EndDate, MealsPerDay) aby unikn¹æ ponownego pobierania
- Batch loading (paczki po 20 przepisów) + krótkie `Task.Delay` dla responsywnoœci UI
- Potencjalne ulepszenia: MemoryCache dla Recipes globalnie, prefetching, kompresja/serializacja offline

---
## 14. Wzorce asynchroniczne
- Wszystkie operacje I/O (EF, HTTP, import) = async/await
- Progress UI przez w³aœciwoœci `LoadingStatus` / `LoadingProgress`
- Brak blokowania w¹tku UI – w krytycznych miejscach u¿yte krótkie opóŸnienia dla p³ynnoœci

---
## 15. Obs³uga b³êdów i odpornoœæ
- Try/catch w punktach granicznych (LoadAsync, SeedData, Import, Nutrition calc)
- Logowanie przez `System.Diagnostics.Debug.WriteLine`
- User-friendly komunikaty przez `DisplayAlert`
- Mo¿liwe przysz³e rozszerzenia: centralny logger (ILogger), telemetry, retry policy (Polly) dla HTTP

---
## 16. Walidacja
- `AddRecipeViewModel` – walidacja pól (konwersja liczb, makra, iloœæ porcji)
- Brak centralnego systemu walidacji – potencja³ na wprowadzenie FluentValidation / dedykowanego adaptera do MVVM

---
## 17. Bezpieczeñstwo i prywatnoœæ
- Lokalne dane w SQLite (brak jeszcze szyfrowania – mo¿liwoœæ u¿ycia Microsoft.Data.Sqlite z has³em / rozwi¹zania typu SQLCipher)
- Potencjalne klucze/API – przechowywaæ w SecureStorage
- Brak zewnêtrznego logowania / kont u¿ytkowników (aplikacja lokalna)
- Anonimizacja – brak danych osobowych (N/A na obecnym etapie)

---
## 18. Wydajnoœæ
Aktualne praktyki:
- Batch update listy recept
- Minimalizacja UI lock przez krótkie Task.Delay i ObservableCollection
- Unikanie zbêdnych Include() – ³adowanie sk³adników przepisu dopiero gdy potrzebne (miejsce do poprawy – lazy load / selektywne zapytania)
Propozycje optymalizacji:
- Indeks na PlannedMeals.Date + RecipeId
- Prekomputacja makr / denormalizacja (cache sum sk³adników w Recipe)
- Virtualization / CollectionView optymalizacje
- Profilowanie zu¿ycia pamiêci dla d³ugich sesji (szczególnie event handler unsubscription)

---
## 19. Testy (plan)
- Unit: serwisy (mock DbContext via InMemory), ViewModel (mock services, FluentAssertions)
- Integration: real EF Core SQLite in-memory / plik testowy
- UI (opcjonalnie): .NET MAUI UITest / AppCenter
- Coverage target: 80% logicznych ga³êzi w Services + krytyczne VM

---
## 20. Rozszerzalnoœæ
| Scenariusz | Zalecenie |
|------------|-----------|
| Dodanie API synchronizacji | Wydziel warstwê Infrastructure/ApiClient + DTO Mappery |
| Wprowadzenie u¿ytkowników | Dodaæ warstwê auth + kontener SecureStorage, modele User/Profile |
| Analiza makro trendów | Wprowadziæ modu³ AnalyticsService z cachingiem statystyk |
| AI Planner | Nowy serwis `IAiMealPlanningService` ? generuje listê PlannedMeal; integracja w PlannerViewModel |
| Eksport / PDF | Adapter eksportuj¹cy Plan + Ingredients do PDF/CSV (oddzielny modu³) |

---
## 21. Decyzje architektoniczne (ADR skrót)
| ID | Decyzja | Status | Uzasadnienie |
|----|---------|--------|--------------|
| ADR-01 | EF Core + SQLite | Zaakceptowane | Prosta lokalna baza, wsparcie multi-platform |
| ADR-02 | Brak osobnych repozytoriów | Tymczasowe | Redukcja boilerplate – ma³y zespó³ / MVP |
| ADR-03 | Shell Navigation | Zaakceptowane | Standaryzowany routing + TabBar multi-platform |
| ADR-04 | Manual caching w PlannerViewModel | Zaakceptowane | Prosty wzrost responsywnoœci bez zewnêtrznych bibliotek |
| ADR-05 | Resource .resx lokalizacja | Zaakceptowane | Standard .NET, póŸniejsza ³atwa rozbudowa jêzyków |
| ADR-06 | Batch UI loading | Zaakceptowane | P³ynnoœæ UI na s³abszych urz¹dzeniach |
| ADR-07 | Brak migracji przy starcie (EnsureCreated) | Do rewizji | Skrócenie czasu startu – docelowo wprowadziæ migracje |

---
## 22. Ryzyka i mitigacje
| Ryzyko | Skutek | Mitigacja |
|--------|--------|----------|
| Brak migracji | Trudna ewolucja schematu | Wprowadziæ EF Migrations + CI krok aktualizacji |
| Memory leak (event handlers) | Degradacja wydajnoœci | Audyty, wzorzec WeakEvent / odsubskrybowanie w Reset |
| Brak szyfrowania DB | Mo¿liwy dostêp do danych | Szyfrowany provider / encja tylko z danymi niesensytywnymi |
| Z³o¿one importy z sieci | Wra¿liwoœæ na layout strony | Parser plug-in + testy kontraktowe |

---
## 23. Roadmap techniczny (skrót)
1. Wprowadzenie migracji + testy migracyjne
2. Dodanie testów jednostkowych (min. Services + PlannerViewModel)
3. Warstwa caching globalny (IMemoryCache lub LiteDB dla offline sync)
4. Modu³ AI planowania posi³ków (heurystyki + preferencje u¿ytkownika)
5. Odchudzenie modeli UI (DTO zamiast bezpoœrednich encji w bindingach – ograniczenie ryzyka side effects)
6. Audyt lokalizacji – pe³ne pokrycie stringów

---
## 24. Konwencje kodu (skrót)
- C# 13, nullable enabled, `var` gdy typ oczywisty, jawne typy gdy zwiêksza czytelnoœæ
- Nazwy async metod: sufiks `Async`
- Publiczne cz³ony dokumentowane XML
- Brak logiki biznesowej w code-behind – tylko routing/UI glue

---
## 25. Dalsze rekomendacje
- Rozwa¿yæ wprowadzenie CommunityToolkit.Mvvm (atrybuty `[ObservableProperty]`, `[RelayCommand]`) dla redukcji boilerplate
- Centralny serwis logowania (ILogger<T>) zamiast Debug.WriteLine
- Mechanizm diff / change tracking dla szybszych zapisów (tylko zmienione encje)
- Utrzymywanie PURE metod dla obliczeñ makro (³atwiejsze unit testy)

---
## 26. S³owniczek
| Pojêcie | Definicja |
|---------|-----------|
| Plan | Zakres dat + meta (archiwizacja) determinuj¹cy listê zakupów |
| PlannedMeal | Element planu – posi³ek odwo³uj¹cy siê do przepisu w dacie |
| Lista zakupów | Agregacja sk³adników z posi³ków w ramach planu |
| Makro | Kalorie, bia³ko, t³uszcz, wêglowodany |

---
## 27. Aktualizacja dokumentu
Dokument aktualizowaæ przy ka¿dej istotnej zmianie: dodanie nowej warstwy, refaktoryzacja modeli, zmiana strategii seedowania, dodanie migracji lub modu³u AI.

---
**Ostatnia aktualizacja:** (auto) – dopasuj przy commicie.  
**W³aœciciel dokumentu:** Zespó³ FoodBook App.