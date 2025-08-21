#    FoodBook App

**Aplikacja mobilna do zarządzania przepisami, planowania posiłków i tworzenia list zakupów**

[![.NET MAUI](https://img.shields.io/badge/.NET-MAUI-512BD4?style=flat-square)](https://dotnet.microsoft.com/apps/maui)

[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat-square)](https://www.sqlite.org/)

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4 style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)

[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Core-blue style=flat-square)](https://docs.microsoft.com/en-us/ef/core/)

[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57 style=flat-square)](https://www.sqlite.org/)

##    Opis projektu

FoodBook App to kompleksowa aplikacja mobilna stworzona w technologii .NET MAUI, która pomaga użytkownikom w:
-    Zarządzaniu bazą przepisów kulinarnych
-    Organizowaniu składników z informacjami odżywczymi
-    Planowaniu posiłków na wybrane dni
-    Automatycznym generowaniu list zakupów
-    Importowaniu przepisów z internetu

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
- **Models**: Klasy reprezentujące dane (Recipe, Ingredient, Plan)
- **Views**: Widoki XAML definiujące interfejs użytkownika
- **ViewModels**: Logika prezentacji i wiązanie danych

###    **Baza danych**
- **SQLite**: Lokalna baza danych na urządzeniu
- **Entity Framework Core**: ORM do zarządzania danymi
- **Migracje**: Automatyczne tworzenie i aktualizacja schematu
- **Seed Data**: Automatyczne wypełnianie przykładowymi danymi

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
Aplikacja obsługuje jasny i ciemny motyw, automatycznie dostosowując się do ustawień systemu.

###    **Układy responsywne**
Interfejs automatycznie dostosowuje się do różnych rozmiarów ekranów i orientacji urządzenia.

##    **Konfiguracja rozwoju**

###    **Główne pakiety NuGet**<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" />
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

