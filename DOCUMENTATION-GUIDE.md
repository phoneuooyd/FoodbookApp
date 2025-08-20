# DOCUMENTATION-GUIDE.md

Przewodnik standaryzujący dokumentowanie w projekcie FoodBook App (.NET MAUI / .NET 9).

---
## 1. Cel dokumentu
Ujednolicenie sposobu tworzenia, aktualizacji i utrzymywania dokumentacji (kod, architektura, procesy, zmiany). Dokument stanowi punkt odniesienia dla deweloperów i agentów AI.

---
## 2. Moduł Planera (Meal Planner) – Dokumentacja Funkcjonalna
Ta sekcja dokumentuje aktualne działanie planera na podstawie analizy: `PlannerViewModel`, `PlannerPage.xaml.cs`, `IPlannerService`, `PlannerDay`, `PlannedMeal`, `Plan`.

### 2.1 Cel modułu
Pozwala użytkownikowi:
- Wybrać zakres dat (StartDate–EndDate)
- Ustawić liczbę posiłków dziennie (MealsPerDay)
- Dodać / usunąć posiłki (PlannedMeal) oraz wskazać przepis (Recipe)
- Skonfigurować porcje dla każdego posiłku
- Zapisać plan (tworzy encję `Plan` + odpowiadające `PlannedMeal` w bazie) – baza wyjściowa do list zakupów.

### 2.2 Kluczowe klasy
| Klasa | Rola |
|-------|------|
| PlannerViewModel | Logika prezentacji planera, ładowanie danych, cache, komendy |
| PlannerPage | Strona UI powiązana z VM (shell tab / route) |
| PlannerDay | Struktura pomocnicza zawierająca datę i kolekcję posiłków |
| PlannedMeal | Pojedynczy zaplanowany posiłek (RecipeId, Portions, Date) |
| Plan | Agregat dat (StartDate, EndDate) + IsArchived dla list zakupów |
| IPlannerService | Interfejs operacji CRUD dla PlannedMeal |
| IPlanService | Operacje na Plan (kolizje, zapis) |
| IRecipeService | Dostarczanie listy przepisów |

### 2.3 Stany i właściwości (VM)
| Właściwość | Opis |
|------------|------|
| StartDate / EndDate | Zakres planowania; zmiana auto wywołuje `LoadAsync()` |
| MealsPerDay | Liczba pozycji (slotów) dziennie; dostosowywana dynamicznie |
| Recipes | Załadowane przepisy (batchowo dla responsywności) |
| Days | Kolekcja `PlannerDay` od StartDate do EndDate |
| IsLoading / LoadingStatus / LoadingProgress | Stan i progres ładowania wieloetapowego |
| Cache (_cached*) | Lokalny deep copy stanu dla ponownych wejść na stronę |

### 2.4 Komendy (VM)
| Komenda | Akcja |
|---------|------|
| AddMealCommand(PlannerDay) | Dodaje nowy slot posiłku dla dnia (PlannedMeal placeholder) |
| RemoveMealCommand(PlannedMeal) | Usuwa posiłek (odsubskrybowuje eventy) |
| IncreasePortionsCommand / DecreasePortionsCommand | Zmiana liczby porcji w zakresie 1–20 |
| SaveCommand | Walidacja zakresu (kolizje), utworzenie `Plan`, zapis posiłków, reset + cache clear |
| CancelCommand | Nawigacja wstecz (Shell) |

### 2.5 Przepływ ładowania (`LoadAsync`)
Etapy (ze wskaźnikiem progresu):
1. Przygotowanie i czyszczenie kolekcji (0.1)
2. Ładowanie przepisów (batch: 20 szt., progres do 0.5) – minimalizacja blokowania UI / krótkie `Task.Delay`
3. Pobranie istniejących zaplanowanych posiłków z serwisu (0.5)
4. Budowa dni kalendarza (0.7→0.9) – UWAGA: aktualnie dodawanie istniejących posiłków jest ZAKOMENTOWANE (`day.Meals.Add(meal)`), więc nie są renderowane; to przewiduje przyszłą integrację (np. AI planner)
5. Finalizacja + wywołanie `AdjustMealsPerDay()` oraz cache (1.0)

### 2.6 Cache
Mechanizm: Po udanym załadowaniu wykonywany jest deep copy (`Recipes`, `Days` + `Meals`) do list `_cachedRecipes`, `_cachedDays` oraz zapis parametrów `_cachedStartDate`, `_cachedEndDate`, `_cachedMealsPerDay`.
Użycie: Przy kolejnych wejściach jeśli parametry nie uległy zmianie – odtwarzany jest stan zamiast ponownego pobierania.
Inwalidacja: `Reset()` / zmiana zakresu / zmiana MealsPerDay.

### 2.7 Operacja zapisu (`SaveAsync`)
1. Walidacja kolizji dat: `IPlanService.HasOverlapAsync(StartDate, EndDate)` – jeśli istnieje aktywny plan w zakresie -> alert i przerwanie.
2. Tworzenie nowego `Plan` (StartDate, EndDate)
3. Usunięcie istniejących posiłków w zakresie (czyszczenie) – uniknięcie duplikatów
4. Iteracja po dniach i posiłkach: dla posiłków z wybranym `Recipe` ustawiany `RecipeId` i zapisywany `PlannedMeal`
5. Reset stanu + czyszczenie cache
6. Prezentacja alertu z potwierdzeniem (lista zakupów gotowa)

Artefakty utworzone:
- Encja `Plan` (wykorzystywana w module Listy Zakupów)
- Kolekcja `PlannedMeal` powiązana datą i `RecipeId`

### 2.8 Ograniczenia i reguły biznesowe
| Reguła | Opis |
|-------|-----|
| Porcje | 1 ≤ Portions ≤ 20 |
| Kolizja planów | Brak nakładających się aktywnych planów (archiwizacja poza zakresem) |
| MealsPerDay | Dynamicznie wymusza liczbę slotów – brak mechanizmu różnej liczby posiłków między dniami |
| Cache | Aktualny tylko jeśli nie zmieniono kluczowych parametrów (daty, MealsPerDay) |
| Istniejące posiłki | Obecnie nie są renderowane (komentarz w kodzie) – docelowo przywrócić logikę / AI uzupełniania |

### 2.9 Interakcje / UI (wnioskowane)
| Element UI | Powiązanie |
|------------|-----------|
| DatePicker Start | `StartDate` (PropertyChanged => Load) |
| DatePicker End | `EndDate` (PropertyChanged => Load) |
| Stepper / Picker MealsPerDay | `MealsPerDay` (AdjustMealsPerDay) |
| Lista dni (CollectionView/ListView) | ItemsSource = `Days` |
| Wiersz posiłku (Meal slot) | Binding do `PlannedMeal` (Recipe picker, plus/minus, remove) |
| Przycisk "+" (Add Meal) | `AddMealCommand` |
| Przycisk "Usuń" | `RemoveMealCommand` |
| Przycisk "+ porcje" | `IncreasePortionsCommand` |
| Przycisk "- porcje" | `DecreasePortionsCommand` |
| Zapisz | `SaveCommand` |
| Anuluj / Back | `CancelCommand` |
| ProgressBar | `LoadingProgress` |
| Status Label | `LoadingStatus` |

### 2.10 Przyszłe rozszerzenia (sugestie)
| Obszar | Propozycja |
|--------|-----------|
| Render istniejących posiłków | Od-komentować `day.Meals.Add(meal)` + mechanizm mapowania na UI |
| AI Planner | Generowanie posiłków automatycznie wg makro / preferencji |
| Różne posiłki na różne dni | Per-day override liczby slotów |
| Walidacja braków | Highlight slotów bez Recipe przy zapisie (opcjonalne) |
| Undo / History | Bufor zmian przed zapisem (dirty tracking) |
| Optymalizacja | Wspólny cache przepisu między modułami (global Recipe cache) |
| Partial Save | Dodawanie nowych posiłków bez pełnego resetu po zapisie |

### 2.11 Wzorce jakości zastosowane
- Batch loading + micro-delays dla płynności UI (responsywność na słabszych urządzeniach)
- Deep copy cache – zmniejszenie kosztu ponownego wejścia w stronę
- Defensive coding (sprawdzanie null w AddMeal/RemoveMeal)
- Event unsubscription (Reset usuwa handlery przed czyszczeniem kolekcji)

### 2.12 Ryzyka
| Ryzyko | Uwagi |
|--------|-------|
| Komentarz blokujący odtwarzanie istniejących posiłków | Użytkownik nie widzi zapisanych posiłków – może uznać, że plan się nie zapisał |
| Brak walidacji nieuzupełnionych slotów | Tworzy Plan bez żadnych PlannedMeal (możliwie pusta lista zakupów) |
| Brak limitu dat (duży zakres) | Potencjalnie duże kolekcje Days → obciążenie UI |

### 2.13 Szablon dokumentacyjny dla nowych modułów (do reużycia)## <Nazwa modułu>
Cel: ...
Główne klasy: ...
Stany/Properties: (tabela)
Komendy/Akcje: (tabela)
Przepływ główny: Kroki + pseudo sequence
Reguły biznesowe: (tabela)
Elementy UI + binding: (tabela)
Artefakty zapisane: ...
Cache/Optymalizacje: ...
Ryzyka i mitigacje: ...
Przyszłe rozszerzenia: ...

---
## 3. Moduł Składników (Ingredients) – Dokumentacja Funkcjonalna
Analiza na podstawie: `IngredientsViewModel`, `IngredientsPage`, `IngredientFormViewModel`, `IngredientFormPage`, `Ingredient`, `IIngredientService`, integracja `SeedData.UpdateIngredientWithOpenFoodFactsAsync`.

### 3.1 Cel modułu
Umożliwić zarządzanie katalogiem składników z wartościami odżywczymi wykorzystywanymi w przepisach i planowaniu posiłków:
- Przeglądaj i filtruj listę składników
- Dodawaj / edytuj / usuwaj składniki
- Utrzymuj wartości odżywcze bazowe (kalorie, białko, tłuszcz, węglowodany)
- Weryfikuj i aktualizuj dane pojedynczego składnika lub wielu składników z OpenFoodFacts
- Dostarczaj dane referencyjne do kalkulacji wartości przepisów (AddRecipeViewModel)

### 3.2 Kluczowe klasy
| Klasa | Rola |
|-------|------|
| Ingredient | Model domenowy (Id, Name, Quantity, Unit, Calories, Protein, Fat, Carbs, RecipeId) |
| IngredientsViewModel | Logika listy składników (ładowanie, filtracja, komendy CRUD, masowa weryfikacja) |
| IngredientsPage | Strona listy + inicjacja seeda przy pustej bazie |
| IngredientFormViewModel | Logika formularza dodawania / edycji pojedynczego składnika + weryfikacja OpenFoodFacts |
| IngredientFormPage | UI formularza (binding do właściwości i komend VM) |
| IIngredientService | Abstrakcja warstwy danych (CRUD dla Ingredient) |
| SeedData (metody OpenFoodFacts) | Aktualizacja wartości odżywczych na podstawie zewnętrznego API |

### 3.3 Stany i właściwości
| Kontekst | Właściwość | Opis |
|----------|-----------|------|
| Lista | Ingredients (ObservableCollection) | Bieżąca (filtrowana) lista wyświetlana w UI |
| Lista | _allIngredients (prywatne) | Pełna lista do filtracji |
| Lista | SearchText | Tekst filtrowania (case-insensitive Contains) |
| Lista | IsLoading / IsRefreshing | Flagi stanu ładowania / odświeżania |
| Lista | IsBulkVerifying / BulkVerificationStatus | Stan i status masowej weryfikacji |
| Formularz | Name, Quantity, SelectedUnit | Dane podstawowe składnika (Quantity jako string dla walidacji) |
| Formularz | Calories, Protein, Fat, Carbs | Wartości odżywcze (string dla walidacji) |
| Formularz | ValidationMessage / HasValidationError | Walidacja pól formularza |
| Formularz | VerificationStatus / IsVerifying | Status pojedynczej weryfikacji OpenFoodFacts |
| Formularz | Title / SaveButtonText | Dynamiczny tytuł i etykieta przycisku (nowy/edycja) |
| Formularz | IsPartOfRecipe / RecipeInfo | Informacja kontekstowa jeśli składnik należy do przepisu |

### 3.4 Komendy / Akcje
| Kontekst | Komenda | Działanie |
|----------|---------|----------|
| Lista | AddCommand | Nawigacja do pustego formularza składnika |
| Lista | EditCommand(Ingredient) | Nawigacja do formularza z parametrem id |
| Lista | DeleteCommand(Ingredient) | Usunięcie z bazy + kolekcji |
| Lista | RefreshCommand | Ponowne ładowanie listy (ReloadAsync) |
| Lista | BulkVerifyCommand | Masowa weryfikacja wszystkich aktualnie wyświetlanych składników |
| Formularz | SaveCommand | Walidacja i zapis (Add lub Update) |
| Formularz | CancelCommand | Powrót do poprzedniej strony |
| Formularz | VerifyNutritionCommand | Pojedyncza weryfikacja / aktualizacja wartości z OpenFoodFacts |

### 3.5 Główne przepływy
1. Ładowanie listy (LoadAsync):
   - Pobranie pełnej listy z serwisu
   - Batch insert do `Ingredients` (domyślnie paczki 50) z krótkimi `Task.Delay(1)` dla płynności UI
   - Ustawienie `_allIngredients` i zastosowanie filtra (SearchText)
2. Filtrowanie: Ustawienie `SearchText` => FilterIngredients() podmienia kolekcję na dopasowania
3. Dodanie / Edycja:
   - Formularz waliduje każde pole on-change (`ValidateInput`) i steruje `CanSave`
   - `SaveAsync` tworzy lub aktualizuje encję przez `IIngredientService`
4. Weryfikacja pojedyncza (VerifyNutritionAsync):
   - Tworzenie kopii tymczasowej Ingredient (tylko nazwa + obecne wartości)
   - Wywołanie `SeedData.UpdateIngredientWithOpenFoodFactsAsync`
   - W razie aktualizacji: nadpisanie wartości w VM i komunikat porównawczy (oryginał → nowe)
5. Masowa weryfikacja (BulkVerifyIngredientsAsync):
   - Potwierdzenie użytkownika
   - Iteracja po kolekcji (progress w `BulkVerificationStatus`)
   - Dla każdego: kopia, próba update, zapis zmian do bazy gdy różnice
   - Minimalne opóźnienie (200 ms) aby nie przeciążyć API
   - Wynik końcowy (zaktualizowane / bez zmian / błędy)
6. Seedowanie (IngredientsPage):
   - Po pierwszym ładowaniu jeśli lista pusta → dialog i opcjonalne wywołanie `SeedData.SeedIngredientsAsync`

### 3.6 Reguły biznesowe / Walidacje
| Obiekt | Reguła |
|--------|-------|
| Ingredient (formularz) | Name wymagane (niepuste) |
| Ingredient (formularz) | Quantity > 0 (parsowalne double) |
| Ingredient (formularz) | Kalorie / Białko / Tłuszcze / Węglowodany ≥ 0 (parsowalne) |
| Ingredient (model) | Wartości odżywcze interpretowane względem: 100 g / 100 ml / 1 sztuka (Piece) |
| Weryfikacja OF | Wykonywana tylko gdy Name niepusty, blokada IsVerifying zapobiega równoczesnym wywołaniom |
| Masowa weryfikacja | Wymaga potwierdzenia i nie uruchamia się równolegle (IsBulkVerifying) |

### 3.7 Elementy UI + Binding (wnioskowane)
| Strona | Element | Powiązanie |
|--------|--------|-----------|
| IngredientsPage | Lista (CollectionView/ListView) | ItemsSource = `Ingredients` |
| IngredientsPage | Pole wyszukiwania | Text = `SearchText` (on-change filtr) |
| IngredientsPage | Pull-to-refresh | Command = `RefreshCommand`, IsRefreshing = `IsRefreshing` |
| IngredientsPage | Dodaj przycisk FAB / toolbar | `AddCommand` |
| IngredientsPage | Akcje elementu (edit/delete) | `EditCommand` / `DeleteCommand` |
| IngredientsPage | Masowa weryfikacja (przycisk) | `BulkVerifyCommand`, IsEnabled !`IsBulkVerifying` |
| IngredientFormPage | Entry Name | `Name` |
| IngredientFormPage | Entry Quantity | `Quantity` |
| IngredientFormPage | Picker Unit | `SelectedUnit`, ItemsSource = `Units` |
| IngredientFormPage | Pola makro (Entries) | `Calories`, `Protein`, `Fat`, `Carbs` |
| IngredientFormPage | Przycisk Weryfikuj | `VerifyNutritionCommand`, IsEnabled = !`IsVerifying` |
| IngredientFormPage | Status weryfikacji | `VerificationStatus` (IsVisible konwerter string→bool) |
| IngredientFormPage | Zapisz | `SaveCommand`, Text = `SaveButtonText` |
| IngredientFormPage | Walidacja | `ValidationMessage`, IsVisible=`HasValidationError` |

### 3.8 Artefakty / Produkty
| Artefakt | Opis |
|----------|------|
| Encja Ingredient | Trwałe dane (EF / baza lokalna) |
| Zaktualizowane wartości odżywcze | Źródło dla kalkulacji w przepisach (agregacja makro) |
| Logi debug | Informacyjne (dodanie/aktualizacja, weryfikacje) |

### 3.9 Cechy i wzorce jakości
- Batch loading dla responsywności (analogicznie do modułu Planera)
- Rozdzielenie logiki formularza i listy (dwa ViewModel-e)
- Walidacja przy każdej zmianie pola (wczesne sprzężenie zwrotne)
- Komendy z dynamicznym CanExecute (Save, Verify, BulkVerify)
- Ochrona przed równoległymi operacjami (IsVerifying / IsBulkVerifying)
- Integracja z zewnętrznym źródłem (OpenFoodFacts) przez kontrolowany wrapper SeedData
- Reużywalność wartości (Ingredients jako dane referencyjne do AddRecipeViewModel)

### 3.10 Przypadki użycia (Use Cases)
| Id | Nazwa | Aktor | Scenariusz sukcesu |
|----|-------|-------|--------------------|
| UC-ING-01 | Przegląd listy składników | Użytkownik | Otwiera listę → widzi wszystkie składniki (batch load) |
| UC-ING-02 | Filtrowanie składników | Użytkownik | Wpisuje frazę → lista zawęża się dynamicznie |
| UC-ING-03 | Dodanie składnika | Użytkownik | Przycisk Dodaj → wypełnia formularz → Zapisz → powrót do listy |
| UC-ING-04 | Edycja składnika | Użytkownik | Wybiera istniejący → modyfikuje dane → Zapisz |
| UC-ING-05 | Usunięcie składnika | Użytkownik | Wybiera Usuń → składnik znika z listy |
| UC-ING-06 | Weryfikacja pojedyncza | Użytkownik | Formularz → Weryfikuj → aktualizacja makro + komunikat |
| UC-ING-07 | Masowa weryfikacja | Użytkownik | Lista → Masowa weryfikacja → postęp → raport wynikowy |
| UC-ING-08 | Seed przykładowych danych | Użytkownik | Pusta baza → dialog → Potwierdź → automatyczne wypełnienie |

### 3.11 Ryzyka i ograniczenia
| Ryzyko/Ograniczenie | Uwagi / Potencjalna mitigacja |
|---------------------|-------------------------------|
| Brak caching-u listy | Każde przejście może ponownie ładować dane – rozważyć cache + invalidacja |
| Zależność od API OpenFoodFacts | Limity / opóźnienia → rozważyć throttling / lokalny cache odpowiedzi |
| Brak undo dla masowej weryfikacji | Zmiany wartości natychmiast zapisywane – opcjonalny snapshot / transakcja |
| Założenie bazowe 100g/100ml | Wymaga wyraźnej komunikacji w UI / labeli |
| Możliwe duplikaty nazw | Brak wymuszenia unikalności – rozważyć walidację w serwisie |
| Brak paginacji przy bardzo dużej liście | W przyszłości wprowadzić wirtualizację / infinite scroll |
| Brak zabezpieczenia przed częstym BulkVerify | Dodać minimalny interwał / log throttle |

### 3.12 Przyszłe rozszerzenia
| Obszar | Propozycja |
|--------|-----------|
| Caching | Cache warstwy `IIngredientService` + ETag / timestamp |
| Historia zmian | Audyt wartości odżywczych po weryfikacjach |
| Wersjonowanie składników | Soft-delete + datowane rewizje |
| Import CSV/Excel | Masowe dodawanie / aktualizacja lokalna |
| Eksport | Udostępnianie listy do analizy (CSV) |
| Obsługa alergenów | Pola dodatkowe (gluten, laktoza, itp.) |
| Kategoryzacja | Tagowanie składników (warzywa, białka, tłuszcze) |
| Wydajność | Wirtualizacja UI / incremental loading |
| Walidacja unikalności | Ostrzeżenie przy dodaniu istniejącej nazwy |
| Offline sync | Kolejka zmian i synchronizacja z chmurą |

---
## 4. Moduł Przepisów (Recipes) – Dokumentacja Funkcjonalna
Analiza na podstawie: `Recipe`, `RecipeViewModel`, `RecipesPage`, `AddRecipeViewModel`, `AddRecipePage`, `IRecipeService`, integracja z `IIngredientService` i `RecipeImporter`.

### 4.1 Cel modułu
Zapewnić pełny cykl życia przepisów kulinarnych wraz z automatycznym lub ręcznym wyliczaniem wartości odżywczych:
- Tworzenie / edycja / usuwanie przepisów
- Dodawanie / usuwanie składników w ramach przepisu
- Automatyczne sumowanie wartości odżywczych na podstawie składników (tryb kalkulowany)
- Ręczna korekta wartości (tryb manualny) lub kopiowanie obliczonych do edytowalnych pól
- Import przepisu z zewnętrznego źródła (URL) przez `RecipeImporter`
- Filtrowanie i przeglądanie listy przepisów

### 4.2 Kluczowe klasy
| Klasa | Rola |
|-------|------|
| Recipe | Model domenowy (Id, Name, Description, Calories, Protein, Fat, Carbs, IloscPorcji, Ingredients) |
| RecipeViewModel | Lista przepisów: ładowanie, filtracja, CRUD komendy, batch loading |
| RecipesPage | Strona listy; inicjalne ładowanie / odświeżanie |
| AddRecipeViewModel | Formularz dodawania/edycji, kalkulacje makro, import, walidacja |
| AddRecipePage | UI formularza + eventy (reset, opóźnione przeliczenia, obsługa zmian nazwy składnika) |
| IRecipeService | Warstwa dostępu do danych przepisów (CRUD) |
| IIngredientService | Dostarcza listę składników referencyjnych do kalkulacji i pickerów |
| RecipeImporter | Pobiera strukturę przepisu z URL (parser / integracja zewnętrzna) |

### 4.3 Stany i właściwości (AddRecipeViewModel)
| Właściwość | Opis |
|-----------|------|
| Name / Description | Dane podstawowe przepisu |
| IloscPorcji | Liczba porcji (string do walidacji, domyślnie "2") |
| Calories / Protein / Fat / Carbs | Bieżące (manualne lub skopiowane) wartości przepisu |
| CalculatedCalories / *Protein / *Fat / *Carbs | Automatycznie wyliczone sumy z listy składników |
| UseCalculatedValues / UseManualValues | Przełącznik trybu aktualizacji pól głównych makro |
| Ingredients (ObservableCollection) | Składniki przepisu (lokalne kopie) |
| ImportUrl / ImportStatus | Dane procesu importu |
| ValidationMessage / HasValidationError | Walidacja formularza |
| Title / SaveButtonText | Dynamiczne etykiety (dodawanie vs edycja) |
| AvailableIngredientNames | Lista nazw istniejących składników do wyboru w UI |

### 4.4 Stany i właściwości (RecipeViewModel)
| Właściwość | Opis |
|-----------|------|
| Recipes (ObservableCollection) | Aktualnie wyświetlane przepisy |
| _allRecipes (prywatne) | Pełny zbiór do filtracji |
| SearchText | Tekst filtrowania (nazwa / opis) |
| IsLoading / IsRefreshing | Flagi operacji asynchronicznych |

### 4.5 Komendy / Akcje
| Kontekst | Komenda | Działanie |
|----------|---------|----------|
| Lista | AddRecipeCommand | Nawigacja do pustego formularza przepisu |
| Lista | EditRecipeCommand(Recipe) | Nawigacja do formularza z id |
| Lista | DeleteRecipeCommand(Recipe) | Usunięcie przepisu |
| Lista | RefreshCommand | Odświeżenie listy |
| Formularz | AddIngredientCommand | Dodanie nowego składnika (początkowo 1 g/ szt.) |
| Formularz | RemoveIngredientCommand(Ingredient) | Usunięcie składnika z przepisu |
| Formularz | SaveRecipeCommand | Walidacja + zapis (Add/Update) |
| Formularz | CancelCommand | Nawigacja wstecz |
| Formularz | ImportRecipeCommand | Import z URL (RecipeImporter) |
| Formularz | SetManualModeCommand / SetImportModeCommand | Przełączenie trybu UI (manual vs import) |
| Formularz | CopyCalculatedValuesCommand | Kopiowanie Calculated* do edytowalnych pól |

### 4.6 Główne przepływy
1. Ładowanie listy przepisów (LoadRecipesAsync): Batch loading (paczkami 50) + filtracja
2. Filtrowanie: SearchText => FilterRecipes() (nazwa lub opis Contains Insensitive)
3. Dodawanie nowego przepisu: Reset() → uzupełnienie danych → dynamiczna walidacja → SaveRecipeAsync
4. Edycja przepisu: LoadRecipeAsync(id) ładuje entity + kopie składników → przeliczenie makro → zapis aktualizuje
5. Dodanie składnika: AddIngredientCommand → domyślne wartości 0 → recalculacja + walidacja
6. Aktualizacja wartości odżywczych: Ingredients.CollectionChanged oraz zmiany nazw (OnIngredientNameChanged) wyzwalają recalculację (async) + odczyt bazy
7. Kalkulacje makro: Suma (wartośćSkładnika * współczynnik jednostki) gdzie Gram/Milliliter = qty/100, Piece = qty
8. Tryb automatyczny vs manualny: UseCalculatedValues=true nadpisuje główne pola obliczonymi; wyłączenie pozwala ręcznie edytować
9. Import: ImportRecipeAsync → pobranie danych → ustawienie Name/Description/Ingredients → przeliczenie makro → decyzja o trybie (auto jeśli brak makro w imporcie)
10. Zapis: Walidacja; w trybie dodawania reset formularza po sukcesie; w edycji powrót bez resetu Name itd.

### 4.7 Reguły biznesowe / Walidacje
| Reguła | Opis |
|-------|------|
| Name wymagane | Nie może być puste |
| IloscPorcji > 0 | Musi być dodatnią liczbą całkowitą |
| Makro (Calories/Protein/Fat/Carbs) ≥ 0 | Wartości liczbowe nieujemne |
| Ingredient.Name (jeśli istnieje) | Niepusty przy zapisie |
| Ingredient.Quantity > 0 | Każdy składnik musi mieć dodatnią ilość |
| Dozwolone zapisanie bez składników | Brak twardego wymagania listy składników |
| Konwersja jednostek | Gram/Milliliter w oparciu o wartości dla 100 jednostek; Piece jako 1:1 |

### 4.8 Elementy UI + Binding (wnioskowane)
| Strona | Element | Powiązanie |
|--------|--------|-----------|
| RecipesPage | Lista przepisów | ItemsSource = `Recipes` |
| RecipesPage | Wyszukiwanie | Text = `SearchText` |
| RecipesPage | Pull-to-refresh | Command = `RefreshCommand`, IsRefreshing = `IsRefreshing` |
| AddRecipePage | Pola tekstowe | `Name`, `Description`, `IloscPorcji` |
| AddRecipePage | Pola makro | `Calories`, `Protein`, `Fat`, `Carbs` (edytowalne zależnie od UseCalculatedValues) |
| AddRecipePage | Lista składników | ItemsSource = `Ingredients` |
| AddRecipePage | Przycisk Dodaj składnik | `AddIngredientCommand` |
| AddRecipePage | Usuń składnik | `RemoveIngredientCommand` |
| AddRecipePage | Import (URL) | `ImportUrl`, przycisk = `ImportRecipeCommand`, status = `ImportStatus` |
| AddRecipePage | Tryb automatyczny/manualny | `UseCalculatedValues` / przyciski SetManualModeCommand itd. |
| AddRecipePage | Kopiuj obliczone | `CopyCalculatedValuesCommand` |
| AddRecipePage | Zapisz | `SaveRecipeCommand`, Text=`SaveButtonText` |
| AddRecipePage | Walidacja | `ValidationMessage`, IsVisible=`HasValidationError` |

### 4.9 Artefakty / Produkty
| Artefakt | Opis |
|---------|------|
| Encja Recipe | Utrwalony przepis z makro i listą składników |
| Zsumowane wartości makro | Wynik kalkulacji dynamicznej (Calculated*) |
| Dane importu | Struktura przepisu z zewnętrznego źródła (URL) |
| Logi debug | Informacje o zapisach / błędach / importach |

### 4.10 Cechy i wzorce jakości
- Batch loading listy przepisów (wydajność UI)
- Rozdział: osobny VM listy i formularza (separacja odpowiedzialności)
- Natychmiastowa walidacja pól (early feedback)
- Reaktywne przeliczenia makro przy zmianach składników (observer pattern via CollectionChanged)
- Tryb kalkulowany vs manualny (feature toggle w UI)
- Opóźnione przeliczanie (500 ms timer) przy edycji ilości składników – redukcja kosztów
- Import z fallbackiem: brak danych makro → auto-calc, w przeciwnym razie manualny override możliwy
- Bezpieczeństwo null (sprawdzanie recipe exists przed edycją)

### 4.11 Przypadki użycia (Use Cases)
| Id | Nazwa | Aktor | Scenariusz sukcesu |
|----|-------|-------|--------------------|
| UC-REC-01 | Przegląd listy przepisów | Użytkownik | Otwiera listę → widzi przepisy (batch load) |
| UC-REC-02 | Filtrowanie przepisów | Użytkownik | Wpisuje frazę → lista zawęża się (nazwa/ opis) |
| UC-REC-03 | Dodanie przepisu ręczne | Użytkownik | Formularz → wprowadza dane → zapis → powrót |
| UC-REC-04 | Edycja przepisu | Użytkownik | Wybiera przepis → modyfikuje → zapisuje |
| UC-REC-05 | Usunięcie przepisu | Użytkownik | Akcja usuń → element znika |
| UC-REC-06 | Dodanie składnika do przepisu | Użytkownik | Dodaj składnik → wpisuje ilość → kalkulacja aktualizuje makro |
| UC-REC-07 | Usunięcie składnika | Użytkownik | Usuń składnik → recalculacja makro |
| UC-REC-08 | Przełączenie trybu makro | Użytkownik | Zmienia UseCalculatedValues → pola blokują/odblokowują edycję |
| UC-REC-09 | Kopiowanie obliczonych wartości | Użytkownik | Klik CopyCalculatedValues → makro przeniesione do pól edytowalnych |
| UC-REC-10 | Import przepisu z URL | Użytkownik | Podaje URL → import → uzupełnione dane i składniki |
| UC-REC-11 | Zapis przepisu bez składników | Użytkownik | Wypełnia tylko podstawowe dane → zapis do bazy |

### 4.12 Ryzyka i ograniczenia
| Ryzyko/Ograniczenie | Mitigacja |
|---------------------|----------|
| Brak walidacji unikalności nazw | Dodać sprawdzenie w serwisie / indeks unikalny |
| Import może być zawodny (formaty stron) | Log błędów + fallback komunikaty, testy parsera |
| Rozjazd wartości (manual vs calculated) | W UI wyświetlać znacznik źródła danych / przycisk synchronizacji |
| Częste przeliczenia przy wielu składnikach | Debounce (już 500 ms) i ewentualne batchowanie | 
| Brak undo po usunięciu składnika | Dodać cofnięcie (Snackbar/Toast) |
| Możliwe niespójności jeśli Ingredient zmieni dane globalnie | Wersjonowanie lub snapshot wartości w przepisie |

### 4.13 Przyszłe rozszerzenia
| Obszar | Propozycja |
|--------|-----------|
| Unikalność | Walidacja i sugestia alternatywnych nazw |
| Kategoryzacja przepisów | Tagging / typ posiłku / kuchnia |
| Zdjęcia | Dodanie obrazów do przepisu |
| Skalowanie porcji | Dynamiczna zmiana IloscPorcji -> przeliczenie makro proporcjonalne |
| Eksport / Udostępnianie | Generowanie PDF / linków |
| Zaawansowany import | Wsparcie wielu serwisów kulinarnych + parser pluginy |
| Analiza makro per porcja | Automatyczne wyliczenie wartości na porcję |
| Integracja z Planerem | Szybkie dodanie przepisu do planu z poziomu karty |
| Fuzzy search | Lepsze wyszukiwanie (zawiera, literówki) |
| Testy automatyczne kalkulacji | Walidacja poprawności sum przy różnych jednostkach |

---
## 5. (Pozostałe sekcje przewodnika)
Patrz dalsza część dokumentu (standardy dokumentowania kodu, lokalizacji, testów, ADR itd.).

---
**Ostatnia aktualizacja**: 20.08.2025