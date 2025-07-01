#    FoodBook App


**Aplikacja mobilna do zarz�dzania przepisami, planowania posi�k�w i tworzenia list zakup�w**


[![.NET MAUI](https://img.shields.io/badge/.NET-MAUI-512BD4 style=flat-square)](https://dotnet.microsoft.com/apps/maui)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4 style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Core-blue style=flat-square)](https://docs.microsoft.com/en-us/ef/core/)
[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57 style=flat-square)](https://www.sqlite.org/)

##    Opis projektu

FoodBook App to kompleksowa aplikacja mobilna stworzona w technologii .NET MAUI, kt�ra pomaga u�ytkownikom w:
-    Zarz�dzaniu baz� przepis�w kulinarnych
-    Organizowaniu sk�adnik�w z informacjami od�ywczymi
-    Planowaniu posi�k�w na wybrane dni
-    Automatycznym generowaniu list zakup�w
-    Importowaniu przepis�w z internetu

##   G��wne funkcjonalno�ci

###    **Strona g��wna**
- Przegl�d najwa�niejszych informacji
- Szybki dost�p do wszystkich funkcji aplikacji

###    **Zarz�dzanie przepisami**
-   Dodawanie nowych przepis�w (r�cznie lub import z URL)
-    Edytowanie istniej�cych przepis�w
-     Usuwanie przepis�w
-    Automatyczny import przepis�w z stron internetowych
-    Automatyczne obliczanie warto�ci od�ywczych

###    **Baza sk�adnik�w**
-    Rozbudowana baza sk�adnik�w z warto�ciami od�ywczymi
-   Dodawanie w�asnych sk�adnik�w
-    Edytowanie parametr�w sk�adnik�w
-    Wyszukiwanie sk�adnik�w
-    Wy�wietlanie kalorii, bia�ka, t�uszcz�w i w�glowodan�w

###    **Planer posi�k�w**
-    Planowanie posi�k�w na wybrane dni
-    Konfiguracja liczby posi�k�w dziennie
-     Wyb�r przepis�w z bazy danych
-    Ustalanie liczby porcji dla ka�dego posi�ku
-     Elastyczny zakres dat (od-do)

###    **Listy zakup�w**
-    Automatyczne generowanie list zakup�w na podstawie planera
-   Zaznaczanie zakupionych produkt�w
-    Edycja ilo�ci i jednostek w locie
-     Usuwanie niepotrzebnych pozycji
-    Intuicyjny interfejs do zarz�dzania zakupami


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
- **Models**: Klasy reprezentuj�ce dane (Recipe, Ingredient, Plan)
- **Views**: Widoki XAML definiuj�ce interfejs u�ytkownika
- **ViewModels**: Logika prezentacji i wi�zanie danych

###    **Baza danych**
- **SQLite**: Lokalna baza danych na urz�dzeniu
- **Entity Framework Core**: ORM do zarz�dzania danymi
- **Migracje**: Automatyczne tworzenie i aktualizacja schematu
- **Seed Data**: Automatyczne wype�nianie przyk�adowymi danymi

##    Rozpocz�cie pracy

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

2. **Przywr�� pakiety NuGet**dotnet restore
3. **Zbuduj projekt**dotnet build
4. **Uruchom aplikacj�**# Android

dotnet run --framework net9.0-android

# Windows
dotnet run --framework net9.0-windows10.0.19041.0

###    **Pierwsze uruchomienie**
1. Przy pierwszym uruchomieniu baza danych zostanie automatycznie utworzona
2. Aplikacja za�aduje przyk�adowe sk�adniki z pliku `ingredients.json`
3. Zostanie utworzony przyk�adowy przepis do demonstracji funkcjonalno�ci

##    **Instrukcja u�ytkowania**

###    **Dodawanie sk�adnik�w**
1. Przejd� do zak�adki "Sk�adniki"
2. Naci�nij "Dodaj sk�adnik"
3. Wype�nij formularz z warto�ciami od�ywczymi
4. Zapisz sk�adnik

###    **Tworzenie przepis�w**
1. Przejd� do zak�adki "Przepisy"
2. Naci�nij "Dodaj przepis"
3. Wybierz tryb:
   - **R�czny**: Wprowad� dane samodzielnie
   - **Import**: Podaj URL strony z przepisem
4. Dodaj sk�adniki i ich ilo�ci
5. Zapisz przepis

###    **Planowanie posi�k�w**
1. Przejd� do zak�adki "Planer"
2. Wybierz zakres dat (od-do)
3. Ustaw liczb� posi�k�w dziennie
4. Dla ka�dego dnia:
   - Wybierz przepisy z listy
   - Ustaw liczb� porcji przyciskami +/-
   - Dodaj lub usu� posi�ki
5. Zapisz plan

###    **Generowanie listy zakup�w**
1. Utw�rz plan posi�k�w w Planerze
2. Przejd� do "Listy zakup�w"
3. Otw�rz wygenerowan� list�
4. Podczas zakup�w:
   - Zaznaczaj kupione produkty  
   - Edytuj ilo�ci je�li potrzeba
   - Usuwaj niepotrzebne pozycje

##    **Personalizacja**

###    **Motywy kolorystyczne**
Aplikacja obs�uguje jasny i ciemny motyw, automatycznie dostosowuj�c si� do ustawie� systemu.

###    **Uk�ady responsywne**
Interfejs automatycznie dostosowuje si� do r�nych rozmiar�w ekran�w i orientacji urz�dzenia.

##    **Konfiguracja rozwoju**

###    **G��wne pakiety NuGet**<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />

<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" />
###     **Dodawanie nowych funkcji**


1. **Nowy model**: Dodaj klas� w folderze `Models/`
2. **Nowy serwis**: Utw�rz interfejs i implementacj� w `Services/`
3. **Nowy widok**: Dodaj XAML i code-behind w `Views/`
4. **Nowy ViewModel**: Utw�rz klas� w `ViewModels/`

5. **Rejestracja**: Dodaj do DI w `MauiProgram.cs`

###     **Migracje bazy danych**# Dodanie nowej migracji
dotnet ef migrations add NazwaMigracji

# Aktualizacja bazy danych
dotnet ef database update

