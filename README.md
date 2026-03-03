#    FoodBook App

**Aplikacja mobilna do zarządzania przepisami, planowania posiłków i tworzenia list zakupów**

[![.NET MAUI](https://img.shields.io/badge/.NET-MAUI-512BD4?style=flat-square)](https://dotnet.microsoft.com/apps/maui)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat-square)](https://www.sqlite.org/)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Core-blue?style=flat-square)](https://docs.microsoft.com/en-us/ef/core/)
[![Supabase](https://img.shields.io/badge/Backend-Supabase-3ECF8E?style=flat-square)](https://supabase.com/)
[![xUnit](https://img.shields.io/badge/Tests-xUnit-5B2D8B?style=flat-square)](https://xunit.net/)



##    Opis projektu

FoodBook App to kompleksowa aplikacja mobilna stworzona w technologii .NET MAUI, która pomaga użytkownikom w:
-    Zarządzaniu bazą przepisów kulinarnych
-    Organizowaniu składników z informacjami odżywczymi
-    Planowaniu posiłków na wybrane dni
-    Automatycznym generowaniu list zakupów
-    Importowaniu przepisów z internetu
-    Synchronizacji danych z chmurą (Supabase)
-    Personalizacji wyglądu (motyw, czcionka, język interfejsu)

##   Główne funkcjonalności

###    **Strona główna**
- Przegląd najważniejszych informacji
- Szybki dostęp do wszystkich funkcji aplikacji

###    **Zarządzanie przepisami**
-   Dodawanie nowych przepisów (ręcznie lub import z URL)
-    Edytowanie istniejących przepisów
-     Usuwanie przepisów
-    Automatyczny import przepisów z stron internetowych
-    Automatyczne obliczanie wartości odżywczych
-    Etykiety/kategorie przepisów z obsługą kolorów
-    Organizacja przepisów w hierarchiczne foldery (z obsługą przeciągania i upuszczania)

###    **Baza składników**
-    Rozbudowana baza składników z wartościami odżywczymi
-   Dodawanie własnych składników
-    Edytowanie parametrów składników
-    Wyszukiwanie składników
-    Wyświetlanie kalorii, białka, tłuszczów i węglowodanów

###    **Planer posiłków**
-    Planowanie posiłków na wybrane dni
-    Konfiguracja liczby posiłków dziennie
-     Wybór przepisów z bazy danych
-    Ustalanie liczby porcji dla każdego posiłku
-     Elastyczny zakres dat (od-do)

###    **Listy zakupów**
-    Automatyczne generowanie list zakupów na podstawie planera
-   Zaznaczanie zakupionych produktów
-    Edycja ilości i jednostek w locie
-     Usuwanie niepotrzebnych pozycji
-    Intuicyjny interfejs do zarządzania zakupami

###    **Profil użytkownika i konto**
-    Rejestracja i logowanie za pomocą e-maila i hasła (Supabase Auth)
-    Bezpieczne przechowywanie tokenów JWT (SecureStorage)
-    Zarządzanie kontem z poziomu ekranu Profil

###    **Synchronizacja z chmurą**
-    Dwukierunkowa synchronizacja danych z backendem Supabase
-    Włączanie/wyłączanie synchronizacji chmurowej per konto
-    Wymuszanie natychmiastowej synchronizacji (Force Sync)
-    Kolejka synchronizacji z obsługą konfliktów (priorytet lokalny lub chmurowy)
-    Deduplikacja danych podczas synchronizacji

###    **Ustawienia i personalizacja**
-    Wybór motywu: jasny / ciemny / systemowy
-    Wiele palet kolorystycznych do wyboru
-    Wybór czcionki (font family) i rozmiaru czcionki
-    Wybór języka interfejsu (PL, EN, DE, ES, FR, KO)
-    Migracja i archiwizacja danych

###    **Archiwizacja danych**
-    Eksport i import danych aplikacji
-    Przeglądanie archiwów
-    Kompatybilność archiwów między wersjami aplikacji

##     Architektura aplikacji

###     **Technologie**
- **Framework**: .NET MAUI (Multi-platform App UI)
- **Wersja .NET**: 9.0
- **Baza danych**: SQLite z Entity Framework Core
- **Backend / BaaS**: Supabase (REST API, Auth)
- **Uwierzytelnianie**: JWT (walidacja po stronie klienta, SecureStorage)
- **Wzorce**: MVVM (Model-View-ViewModel)
- **DI**: Wbudowany Dependency Injection
- **UI**: XAML z Material Design
- **Lokalizacja**: .resx resource files (PL, EN, DE, ES, FR, KO)
- **Testy**: xUnit

###    **Wzorzec MVVM**
Aplikacja wykorzystuje wzorzec MVVM z:
- **Models**: Klasy reprezentujące dane (Recipe, Ingredient, Plan, Folder, RecipeLabel, SyncState, AuthAccount)
- **Views**: Widoki XAML definiujące interfejs użytkownika
- **ViewModels**: Logika prezentacji i wiązanie danych

###    **Baza danych**
- **SQLite**: Lokalna baza danych na urządzeniu
- **Entity Framework Core**: ORM do zarządzania danymi
- **Migracje**: Automatyczne tworzenie i aktualizacja schematu
- **Seed Data**: Automatyczne wypełnianie przykładowymi danymi

###    **Uwierzytelnianie i synchronizacja**
- **Supabase Auth**: Rejestracja i logowanie przez REST API
- **JWT**: Walidacja tokenów po stronie klienta (`IJwtValidator`)
- **SecureStorage**: Bezpieczne przechowywanie tokenów na urządzeniu (`IAuthTokenStore`)
- **SupabaseSyncService**: Dwukierunkowa synchronizacja z Supabase
- **DeduplicationService**: Wykrywanie i eliminacja duplikatów podczas synchronizacji
- **SyncQueue**: Kolejka zmian lokalnych oczekujących na synchronizację

###    **Lokalizacja**
Interfejs aplikacji jest w pełni zlokalizowany przy użyciu plików zasobów `.resx`:
- 🇵🇱 Polski (pl-PL)
- 🇬🇧 Angielski (en)
- 🇩🇪 Niemiecki (de-DE)
- 🇪🇸 Hiszpański (es-ES)
- 🇫🇷 Francuski (fr-FR)
- 🇰🇷 Koreański (ko-KR)

##    Rozpoczęcie pracy

###    **Wymagania**
- Visual Studio 2022 (17.8+) lub Visual Studio Code
- .NET 9.0 SDK
- Workloads dla .NET MAUI:
  - Android
  - iOS (opcjonalnie)
  - Windows (opcjonalnie)
  - macOS (opcjonalnie)

###    **Instalacja**

1. **Sklonuj repozytorium**git clone https://github.com/[twoja-nazwa]/FoodBookApp.git
   cd FoodBookApp
2. **Przywróć pakiety NuGet**dotnet restore
3. **Zbuduj projekt**dotnet build
4. **Uruchom aplikację**# Android
dotnet run --framework net9.0-android

# Windows
dotnet run --framework net9.0-windows10.0.19041.0
###    **Pierwsze uruchomienie**
1. Przy pierwszym uruchomieniu baza danych zostanie automatycznie utworzona
2. Aplikacja załaduje przykładowe składniki z pliku `ingredients.json`
3. Zostanie utworzony przykładowy przepis do demonstracji funkcjonalności

##    **Instrukcja użytkowania**

###    **Dodawanie składników**
1. Przejdź do zakładki "Składniki"
2. Naciśnij "Dodaj składnik"
3. Wypełnij formularz z wartościami odżywczymi
4. Zapisz składnik

###    **Tworzenie przepisów**
1. Przejdź do zakładki "Przepisy"
2. Naciśnij "Dodaj przepis"
3. Wybierz tryb:
   - **Ręczny**: Wprowadź dane samodzielnie
   - **Import**: Podaj URL strony z przepisem
4. Dodaj składniki i ich ilości
5. Zapisz przepis

###    **Planowanie posiłków**
1. Przejdź do zakładki "Planer"
2. Wybierz zakres dat (od-do)
3. Ustaw liczbę posiłków dziennie
4. Dla każdego dnia:
   - Wybierz przepisy z listy
   - Ustaw liczbę porcji przyciskami +/-
   - Dodaj lub usuń posiłki
5. Zapisz plan

###    **Generowanie listy zakupów**
1. Utwórz plan posiłków w Planerze
2. Przejdź do "Listy zakupów"
3. Otwórz wygenerowaną listę
4. Podczas zakupów:
   - Zaznaczaj kupione produkty  
   - Edytuj ilości jeśli potrzeba
   - Usuwaj niepotrzebne pozycje

##    **Personalizacja**

###    **Motywy kolorystyczne**
Aplikacja obsługuje jasny i ciemny motyw, automatycznie dostosowując się do ustawień systemu. Dostępne są również dodatkowe palety kolorów do wyboru w ustawieniach.

###    **Czcionki**
Użytkownik może wybrać rodzinę czcionek (font family) oraz rozmiar tekstu (font size) w ustawieniach aplikacji.

###    **Język interfejsu**
Język interfejsu można zmienić niezależnie od języka systemu. Obsługiwane języki: Polski, English, Deutsch, Español, Français, 한국어.

###    **Układy responsywne**
Interfejs automatycznie dostosowuje się do różnych rozmiarów ekranów i orientacji urządzenia.

##    **Testy automatyczne**

Projekt zawiera zestaw testów jednostkowych w folderze `FoodbookApp.Tests` (xUnit):
- Testy modeli (Ingredient, Recipe, Plan, RecipeLabel)
- Testy serwisów (DatabaseService, ThemeService, IngredientService, PlannerService)
- Testy autentykacji (SupabaseAuthServiceTests)
- Testy stron (HomePage, AddRecipePage, IngredientsPage, ArchivePage, DataArchivizationPage)

Uruchomienie testów:
```bash
dotnet test
```

##    **Roadmapa projektu**

### ✅ Zrealizowane
- [x] Zarządzanie przepisami (CRUD, import z URL)
- [x] Baza składników z wartościami odżywczymi
- [x] Planer posiłków (zakres dat, liczba porcji)
- [x] Automatyczne listy zakupów
- [x] Etykiety/kategorie przepisów (z kolorami)
- [x] Foldery do organizacji przepisów (hierarchia, drag & drop)
- [x] Konto użytkownika (rejestracja, logowanie – Supabase Auth, JWT)
- [x] Synchronizacja danych z chmurą (Supabase REST API)
- [x] Archiwizacja i eksport/import danych
- [x] Wielojęzyczny interfejs (PL, EN, DE, ES, FR, KO)
- [x] Personalizacja motywu, palety kolorów, czcionki i rozmiaru tekstu
- [x] Wizard konfiguracyjny przy pierwszym uruchomieniu
- [x] Testy jednostkowe (xUnit)

### 🔜 Planowane
- [ ] Powiadomienia przypominające o posiłkach
- [ ] Udostępnianie przepisów między użytkownikami
- [ ] Skaner kodów kreskowych produktów
- [ ] Integracja z zewnętrznymi bazami danych żywności (np. Open Food Facts)
- [ ] Widżety na ekran główny (Android / iOS)
- [ ] Wersja na macOS / Windows z pełnym wsparciem myszy i klawiatury

##    **Konfiguracja rozwoju**

###    **Główne pakiety NuGet**<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" />
###     **Dodawanie nowych funkcji**

1. **Nowy model**: Dodaj klasę w folderze `Models/`
2. **Nowy serwis**: Utwórz interfejs i implementację w `Services/`
3. **Nowy widok**: Dodaj XAML i code-behind w `Views/`
4. **Nowy ViewModel**: Utwórz klasę w `ViewModels/`
5. **Rejestracja**: Dodaj do DI w `MauiProgram.cs`

###     **Migracje bazy danych**# Dodanie nowej migracji
dotnet ef migrations add NazwaMigracji

# Aktualizacja bazy danych
dotnet ef database update
##    **Wkład w projekt**

