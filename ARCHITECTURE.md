# ARCHITECTURE.md

<!-- This file will contain detailed information about the FoodBook App architecture, design patterns, and technical decisions -->

## 1. Cel dokumentu
Dokument opisuje architektur� aplikacji FoodBook App: warstwy, wzorce, zale�no�ci, decyzje techniczne oraz wytyczne rozwoju i rozszerzalno�ci. Ma by� punktem odniesienia dla deweloper�w i agent�w AI generuj�cych kod.

---
## 2. Kontekst biznesowy
FoodBook App to wieloplatformowa (Android, iOS, Windows, macOS) aplikacja mobilna do:
- Zarz�dzania baz� przepis�w (dodawanie, edycja, import z sieci, obliczenia makro)
- Planowania posi�k�w w przedzia�ach dat z konfiguracj� liczby posi�k�w dziennie
- Generowania list zakup�w na podstawie plan�w
- Archiwizacji i przywracania plan�w/list zakup�w
- Zbierania i wy�wietlania statystyk �ywieniowych (kalorie, bia�ko, t�uszcze, w�glowodany)

---
## 3. Warstwy i podzia� odpowiedzialno�ci
| Warstwa | Zakres | Technologie / Artefakty |
|---------|--------|-------------------------|
| Prezentacji | UI, XAML Pages, Shell | .NET MAUI XAML, Shell, ResourceDictionary, Style, Converters |
| ViewModel (MVVM) | Logika prezentacji, komendy, stan UI | Klasy w `ViewModels/`, INotifyPropertyChanged |
| Serwisy Aplikacyjne | Operacje domenowe / agregacja danych | Klasy w `Services/` (RecipeService, PlannerService, PlanService, ShoppingListService, IngredientService) |
| Dost�p do danych | ORM, persystencja | EF Core (AppDbContext), SQLite |
| Modele Domenowe | Encje i obiekty transferu | Klasy w `Models/` (Recipe, Ingredient, PlannedMeal, PlannerDay, Plan) |
| Infrastruktura | DI, lokalizacja, import, narz�dzia | MauiProgram.cs, Localization*, RecipeImporter, konwertery |

---
## 4. Wzorce projektowe
- MVVM (oddzielenie UI od logiki � View ? ViewModel przez binding)
- Dependency Injection (constructor injection, rejestracje w `MauiProgram.cs`)
- Repository-like podej�cie uproszczone (serwisy aplikacyjne wykorzystuj�ce EF Core � bez osobnych repozytori�w dla prostoty)
- Observer (INotifyPropertyChanged w modelach dynamicznych: Ingredient, PlannedMeal)
- Command Pattern (ICommand w ViewModelach do akcji UI)
- Caching in-memory (PlannerViewModel � w�asny cache zakresu dat)

---
## 5. Struktura projektu
Patrz `PROJECT-FILES.md` � g��wne katalogi: `Models/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, `Localization/`, `Resources/`, `Converters/`, `Platforms/`.

---
## 6. Modele domenowe (skr�t)
- `Recipe`: podstawowe makro warto�ci (Calories, Protein, Fat, Carbs), lista Ingredient, IloscPorcji
- `Ingredient`: warto�ci od�ywcze przypisane do jednostki/ilo�ci, powi�zanie opcjonalne z Recipe
- `PlannedMeal`: referencja do Recipe (RecipeId), Date, Portions
- `PlannerDay`: agreguje kolekcj� PlannedMeal dla konkretnej daty (UI helper)
- `Plan`: przedzia� dat (StartDate, EndDate), IsArchived � logiczna archiwizacja

Relacje kluczowe: Recipe (1) � (N) Ingredient; Plan (N) � (N) PlannedMeal (powi�zanie via zakres dat i RecipeId logicznie, faktycznie PlannedMeal nie ma PlanId � identyfikacja poprzez daty w przedziale planu).

---
## 7. Dost�p do danych
- ORM: Entity Framework Core 9 + SQLite
- Kontekst: `AppDbContext` (DbSet: Recipes, Ingredients, PlannedMeals, Plans)
- Migrations: mog� by� dodane (w README � instrukcja), obecnie inicjalizacja przez `EnsureCreated()` + seed
- Seed: `SeedData.InitializeAsync()` + `SeedIngredientsAsync()` � fallback mechanizmy (embedded resource ? app package ? filesystem ? fallback statyczny)
- Udoskonalenia przysz�e: wprowadzenie migracji kontrolowanych; osobne repozytoria (opcjonalnie); indeksy (np. na PlannedMeals.Date)

---
## 8. Serwisy aplikacyjne
| Serwis | Cel |
|--------|-----|
| IRecipeService / RecipeService | CRUD przepis�w, pobieranie pojedynczego i listy |
| IIngredientService / IngredientService | Operacje na sk�adnikach globalnych i przypisanych do przepisu |
| IPlannerService / PlannerService | Zarz�dzanie PlannedMeal (dodawanie/aktualizacja/usuwanie/pobieranie zakresu) |
| IPlanService / PlanService | Operacje na Plan (archiwizacja, kolizje dat) |
| IShoppingListService / ShoppingListService | Generowanie list zakup�w na podstawie planu (agregacje sk�adnik�w) |
| LocalizationService / LocalizationResourceManager | Lokalizacja tekst�w (resx) i dynamiczne od�wie�anie |
| RecipeImporter | Import przepisu z URL (scraping i heurystyka) |

---
## 9. ViewModel (MVVM) � kluczowe aspekty
- Ka�dy ViewModel izoluje logik� UI: komendy, validacja, �adowanie danych async
- `PlannerViewModel`: zarz�dzanie datami, batch loading, progress reporting, caching
- `HomeViewModel`: agregacja statystyk (nutritional) + elastyczne zakresy dat
- `AddRecipeViewModel`: dwa tryby (manual / import), dynamiczne przeliczanie makro
- `ShoppingListViewModel` i `ArchiveViewModel`: filtrowanie aktywnych/archiwalnych plan�w

Konwencje: minimalizacja logiki w code-behind (XAML.cs) � ograniczona do delegowania zdarze� do VM.

---
## 10. Nawigacja i routing
- `.NET MAUI Shell` (`AppShell.xaml`) � TabBar (Ingredients, Recipes, Home, Planner, ShoppingList)
- Dodatkowe widoki rejestrowane przez `Routing.RegisterRoute` w `MauiProgram.cs`
- Nawigacja wywo�ywana poprzez `Shell.Current.GoToAsync()` z parametrami Query (np. `?id=`)

---
## 11. Dependency Injection (DI)
Rejestracje w `MauiProgram.cs`:
- DbContext (AddDbContext) � scope per request (dla MAUI de facto per scope tworzony z providerem)
- Serwisy domenowe: `AddScoped` (operacje na danych), `RecipeImporter` + `HttpClient`
- ViewModels: mieszanka `AddScoped` / `AddTransient` (AddRecipePageViewModel jako transient � unikanie re-u�ycia stanu)
- Niekt�re VM jako Singleton (SettingsViewModel) � stan globalny aplikacji
- Lokalizacja: Singleton (`LocalizationService`, `LocalizationResourceManager`)

Uzasadnienie: transiency dla formularzy edycji (�wie�y stan), scope dla serwis�w kt�re korzystaj� z DbContext.

---
## 12. Lokalizacja
- Folder `Localization/` � pary plik�w resx (neutral + pl-PL) + wygenerowane Designer.cs
- Binding do zasob�w poprzez niestandardowe rozszerzenie `TranslateExtension`
- Strings w UI nie powinny by� hardcodowane (przysz�e refaktory: przenie�� jeszcze pozosta�e literalne teksty do zasob�w)

---
## 13. Caching i optymalizacja �adowania
- `PlannerViewModel` implementuje mechanizm cache (StartDate, EndDate, MealsPerDay) aby unikn�� ponownego pobierania
- Batch loading (paczki po 20 przepis�w) + kr�tkie `Task.Delay` dla responsywno�ci UI
- Potencjalne ulepszenia: MemoryCache dla Recipes globalnie, prefetching, kompresja/serializacja offline

---
## 14. Wzorce asynchroniczne
- Wszystkie operacje I/O (EF, HTTP, import) = async/await
- Progress UI przez w�a�ciwo�ci `LoadingStatus` / `LoadingProgress`
- Brak blokowania w�tku UI � w krytycznych miejscach u�yte kr�tkie op�nienia dla p�ynno�ci

---
## 15. Obs�uga b��d�w i odporno��
- Try/catch w punktach granicznych (LoadAsync, SeedData, Import, Nutrition calc)
- Logowanie przez `System.Diagnostics.Debug.WriteLine`
- User-friendly komunikaty przez `DisplayAlert`
- Mo�liwe przysz�e rozszerzenia: centralny logger (ILogger), telemetry, retry policy (Polly) dla HTTP

---
## 16. Walidacja
- `AddRecipeViewModel` � walidacja p�l (konwersja liczb, makra, ilo�� porcji)
- Brak centralnego systemu walidacji � potencja� na wprowadzenie FluentValidation / dedykowanego adaptera do MVVM

---
## 17. Bezpiecze�stwo i prywatno��
- Lokalne dane w SQLite (brak jeszcze szyfrowania � mo�liwo�� u�ycia Microsoft.Data.Sqlite z has�em / rozwi�zania typu SQLCipher)
- Potencjalne klucze/API � przechowywa� w SecureStorage
- Brak zewn�trznego logowania / kont u�ytkownik�w (aplikacja lokalna)
- Anonimizacja � brak danych osobowych (N/A na obecnym etapie)

---
## 18. Wydajno��
Aktualne praktyki:
- Batch update listy recept
- Minimalizacja UI lock przez kr�tkie Task.Delay i ObservableCollection
- Unikanie zb�dnych Include() � �adowanie sk�adnik�w przepisu dopiero gdy potrzebne (miejsce do poprawy � lazy load / selektywne zapytania)
Propozycje optymalizacji:
- Indeks na PlannedMeals.Date + RecipeId
- Prekomputacja makr / denormalizacja (cache sum sk�adnik�w w Recipe)
- Virtualization / CollectionView optymalizacje
- Profilowanie zu�ycia pami�ci dla d�ugich sesji (szczeg�lnie event handler unsubscription)

---
## 19. Testy (plan)
- Unit: serwisy (mock DbContext via InMemory), ViewModel (mock services, FluentAssertions)
- Integration: real EF Core SQLite in-memory / plik testowy
- UI (opcjonalnie): .NET MAUI UITest / AppCenter
- Coverage target: 80% logicznych ga��zi w Services + krytyczne VM

---
## 20. Rozszerzalno��
| Scenariusz | Zalecenie |
|------------|-----------|
| Dodanie API synchronizacji | Wydziel warstw� Infrastructure/ApiClient + DTO Mappery |
| Wprowadzenie u�ytkownik�w | Doda� warstw� auth + kontener SecureStorage, modele User/Profile |
| Analiza makro trend�w | Wprowadzi� modu� AnalyticsService z cachingiem statystyk |
| AI Planner | Nowy serwis `IAiMealPlanningService` ? generuje list� PlannedMeal; integracja w PlannerViewModel |
| Eksport / PDF | Adapter eksportuj�cy Plan + Ingredients do PDF/CSV (oddzielny modu�) |

---
## 21. Decyzje architektoniczne (ADR skr�t)
| ID | Decyzja | Status | Uzasadnienie |
|----|---------|--------|--------------|
| ADR-01 | EF Core + SQLite | Zaakceptowane | Prosta lokalna baza, wsparcie multi-platform |
| ADR-02 | Brak osobnych repozytori�w | Tymczasowe | Redukcja boilerplate � ma�y zesp� / MVP |
| ADR-03 | Shell Navigation | Zaakceptowane | Standaryzowany routing + TabBar multi-platform |
| ADR-04 | Manual caching w PlannerViewModel | Zaakceptowane | Prosty wzrost responsywno�ci bez zewn�trznych bibliotek |
| ADR-05 | Resource .resx lokalizacja | Zaakceptowane | Standard .NET, p�niejsza �atwa rozbudowa j�zyk�w |
| ADR-06 | Batch UI loading | Zaakceptowane | P�ynno�� UI na s�abszych urz�dzeniach |
| ADR-07 | Brak migracji przy starcie (EnsureCreated) | Do rewizji | Skr�cenie czasu startu � docelowo wprowadzi� migracje |

---
## 22. Ryzyka i mitigacje
| Ryzyko | Skutek | Mitigacja |
|--------|--------|----------|
| Brak migracji | Trudna ewolucja schematu | Wprowadzi� EF Migrations + CI krok aktualizacji |
| Memory leak (event handlers) | Degradacja wydajno�ci | Audyty, wzorzec WeakEvent / odsubskrybowanie w Reset |
| Brak szyfrowania DB | Mo�liwy dost�p do danych | Szyfrowany provider / encja tylko z danymi niesensytywnymi |
| Z�o�one importy z sieci | Wra�liwo�� na layout strony | Parser plug-in + testy kontraktowe |

---
## 23. Roadmap techniczny (skr�t)
1. Wprowadzenie migracji + testy migracyjne
2. Dodanie test�w jednostkowych (min. Services + PlannerViewModel)
3. Warstwa caching globalny (IMemoryCache lub LiteDB dla offline sync)
4. Modu� AI planowania posi�k�w (heurystyki + preferencje u�ytkownika)
5. Odchudzenie modeli UI (DTO zamiast bezpo�rednich encji w bindingach � ograniczenie ryzyka side effects)
6. Audyt lokalizacji � pe�ne pokrycie string�w

---
## 24. Konwencje kodu (skr�t)
- C# 13, nullable enabled, `var` gdy typ oczywisty, jawne typy gdy zwi�ksza czytelno��
- Nazwy async metod: sufiks `Async`
- Publiczne cz�ony dokumentowane XML
- Brak logiki biznesowej w code-behind � tylko routing/UI glue

---
## 25. Dalsze rekomendacje
- Rozwa�y� wprowadzenie CommunityToolkit.Mvvm (atrybuty `[ObservableProperty]`, `[RelayCommand]`) dla redukcji boilerplate
- Centralny serwis logowania (ILogger<T>) zamiast Debug.WriteLine
- Mechanizm diff / change tracking dla szybszych zapis�w (tylko zmienione encje)
- Utrzymywanie PURE metod dla oblicze� makro (�atwiejsze unit testy)

---
## 26. S�owniczek
| Poj�cie | Definicja |
|---------|-----------|
| Plan | Zakres dat + meta (archiwizacja) determinuj�cy list� zakup�w |
| PlannedMeal | Element planu � posi�ek odwo�uj�cy si� do przepisu w dacie |
| Lista zakup�w | Agregacja sk�adnik�w z posi�k�w w ramach planu |
| Makro | Kalorie, bia�ko, t�uszcz, w�glowodany |

---
## 27. Aktualizacja dokumentu
Dokument aktualizowa� przy ka�dej istotnej zmianie: dodanie nowej warstwy, refaktoryzacja modeli, zmiana strategii seedowania, dodanie migracji lub modu�u AI.

---
**Ostatnia aktualizacja:** (auto) � dopasuj przy commicie.  
**W�a�ciciel dokumentu:** Zesp� FoodBook App.