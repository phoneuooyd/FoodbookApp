#    FoodBook App

**Aplikacja mobilna do zarządzania przepisami, planowania posiłków i tworzenia list zakupów**

[![.NET MAUI](https://img.shields.io/badge/.NET-MAUI-512BD4 style=flat-square)](https://dotnet.microsoft.com/apps/maui)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4 style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Core-blue style=flat-square)](https://docs.microsoft.com/en-us/ef/core/)
[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57 style=flat-square)](https://www.sqlite.org/)

##    Opis projektu

FoodBook App to kompleksowa aplikacja mobilna stworzona w technologii .NET MAUI, która pomaga u¿ytkownikom w:
-    Zarz¹dzaniu baz¹ przepisów kulinarnych
-    Organizowaniu sk³adników z informacjami od¿ywczymi
-    Planowaniu posi³ków na wybrane dni
-    Automatycznym generowaniu list zakupów
-    Importowaniu przepisów z internetu

##   G³ówne funkcjonalnoci

###    **Strona g³ówna**
- Przegl¹d najwa¿niejszych informacji
- Szybki dostêp do wszystkich funkcji aplikacji

###    **Zarz¹dzanie przepisami**
-   Dodawanie nowych przepisów (rêcznie lub import z URL)
-    Edytowanie istniej¹cych przepisów
-     Usuwanie przepisów
-    Automatyczny import przepisów z stron internetowych
-    Automatyczne obliczanie wartoci od¿ywczych

###    **Baza sk³adników**
-    Rozbudowana baza sk³adników z wartociami od¿ywczymi
-   Dodawanie w³asnych sk³adników
-    Edytowanie parametrów sk³adników
-    Wyszukiwanie sk³adników
-    Wywietlanie kalorii, bia³ka, t³uszczów i wêglowodanów

###    **Planer posi³ków**
-    Planowanie posi³ków na wybrane dni
-    Konfiguracja liczby posi³ków dziennie
-     Wybór przepisów z bazy danych
-    Ustalanie liczby porcji dla ka¿dego posi³ku
-     Elastyczny zakres dat (od-do)

###    **Listy zakupów**
-    Automatyczne generowanie list zakupów na podstawie planera
-   Zaznaczanie zakupionych produktów
-    Edycja iloci i jednostek w locie
-     Usuwanie niepotrzebnych pozycji
-    Intuicyjny interfejs do zarz¹dzania zakupami

##     Architektura aplikacji

###     **Technologie**
- **Framework**: .NET MAUI (Multi-platform App UI)
- **Wersja .NET**: 9.0
- **Baza danych**: SQLite z Entity Framework Core
- **Wzorce**: MVVM (Model-View-ViewModel)
- **DI**: Wbudowany Dependency Injection
- **UI**: XAML z Material Design

###    **Wzorzec MVVM**
Aplikacja wykorzystuje wzorzec MVVM z:
- **Models**: Klasy reprezentuj¹ce dane (Recipe, Ingredient, Plan)
- **Views**: Widoki XAML definiuj¹ce interfejs u¿ytkownika
- **ViewModels**: Logika prezentacji i wi¹zanie danych

###    **Baza danych**
- **SQLite**: Lokalna baza danych na urz¹dzeniu
- **Entity Framework Core**: ORM do zarz¹dzania danymi
- **Migracje**: Automatyczne tworzenie i aktualizacja schematu
- **Seed Data**: Automatyczne wype³nianie przyk³adowymi danymi

##    Rozpoczêcie pracy

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
2. **Przywróæ pakiety NuGet**dotnet restore
3. **Zbuduj projekt**dotnet build
4. **Uruchom aplikacjê**# Android
dotnet run --framework net9.0-android

# Windows
dotnet run --framework net9.0-windows10.0.19041.0
###    **Pierwsze uruchomienie**
1. Przy pierwszym uruchomieniu baza danych zostanie automatycznie utworzona
2. Aplikacja za³aduje przyk³adowe sk³adniki z pliku `ingredients.json`
3. Zostanie utworzony przyk³adowy przepis do demonstracji funkcjonalnoci

##    **Instrukcja u¿ytkowania**

###    **Dodawanie sk³adników**
1. Przejd do zak³adki "Sk³adniki"
2. Nacinij "Dodaj sk³adnik"
3. Wype³nij formularz z wartociami od¿ywczymi
4. Zapisz sk³adnik

###    **Tworzenie przepisów**
1. Przejd do zak³adki "Przepisy"
2. Nacinij "Dodaj przepis"
3. Wybierz tryb:
   - **Rêczny**: Wprowad dane samodzielnie
   - **Import**: Podaj URL strony z przepisem
4. Dodaj sk³adniki i ich iloci
5. Zapisz przepis

###    **Planowanie posi³ków**
1. Przejd do zak³adki "Planer"
2. Wybierz zakres dat (od-do)
3. Ustaw liczbê posi³ków dziennie
4. Dla ka¿dego dnia:
   - Wybierz przepisy z listy
   - Ustaw liczbê porcji przyciskami +/-
   - Dodaj lub usuñ posi³ki
5. Zapisz plan

###    **Generowanie listy zakupów**
1. Utwórz plan posi³ków w Planerze
2. Przejd do "Listy zakupów"
3. Otwórz wygenerowan¹ listê
4. Podczas zakupów:
   - Zaznaczaj kupione produkty  
   - Edytuj iloci jeli potrzeba
   - Usuwaj niepotrzebne pozycje

##    **Personalizacja**

###    **Motywy kolorystyczne**
Aplikacja obs³uguje jasny i ciemny motyw, automatycznie dostosowuj¹c siê do ustawieñ systemu.

###    **Uk³ady responsywne**
Interfejs automatycznie dostosowuje siê do ró¿nych rozmiarów ekranów i orientacji urz¹dzenia.

##    **Konfiguracja rozwoju**

###    **G³ówne pakiety NuGet**<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" />
###     **Dodawanie nowych funkcji**

1. **Nowy model**: Dodaj klasê w folderze `Models/`
2. **Nowy serwis**: Utwórz interfejs i implementacjê w `Services/`
3. **Nowy widok**: Dodaj XAML i code-behind w `Views/`
4. **Nowy ViewModel**: Utwórz klasê w `ViewModels/`
5. **Rejestracja**: Dodaj do DI w `MauiProgram.cs`

###     **Migracje bazy danych**# Dodanie nowej migracji
dotnet ef migrations add NazwaMigracji

# Aktualizacja bazy danych
dotnet ef database update
##    **Wk³ad w projekt**

