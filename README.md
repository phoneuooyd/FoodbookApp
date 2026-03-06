# FoodBook App

**Aplikacja mobilna do zarządzania przepisami, planowania posiłków i tworzenia list zakupów**

[![.NET MAUI](https://img.shields.io/badge/.NET-MAUI-512BD4?style=flat-square)](https://dotnet.microsoft.com/apps/maui)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=flat-square)](https://www.sqlite.org/)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-Core-blue?style=flat-square)](https://docs.microsoft.com/en-us/ef/core/)
[![Supabase](https://img.shields.io/badge/Cloud-Supabase-3ECF8E?style=flat-square)](https://supabase.com/)

---

## Opis projektu

FoodBook App to kompleksowa aplikacja mobilna stworzona w technologii .NET MAUI, która pomaga użytkownikom w:

- Zarządzaniu bazą przepisów kulinarnych
- Organizowaniu składników z informacjami odżywczymi
- Planowaniu posiłków na wybrane dni
- Automatycznym generowaniu list zakupów
- Importowaniu przepisów z internetu
- Synchronizacji danych z chmurą Supabase

---

## Główne funkcjonalności

### **Strona główna**
- Przegląd najważniejszych informacji
- Szybki dostęp do wszystkich funkcji aplikacji

### **Zarządzanie przepisami**
- Dodawanie nowych przepisów (ręcznie lub import z URL)
- Edytowanie istniejących przepisów
- Usuwanie i archiwizowanie przepisów
- Automatyczny import przepisów ze stron internetowych (scraping HTML)
- Automatyczne obliczanie wartości odżywczych
- Organizowanie przepisów w **foldery**
- Oznaczanie przepisów **etykietami** (labels)
- Filtrowanie i sortowanie listy przepisów

### **Baza składników**
- Rozbudowana baza składników z wartościami odżywczymi
- Dodawanie własnych składników
- Edytowanie parametrów składników
- Wyszukiwanie składników
- Wyświetlanie kalorii, białka, tłuszczów i węglowodanów

### **Planer posiłków**
- Planowanie posiłków na wybrane dni
- Konfiguracja liczby posiłków dziennie
- Wybór przepisów z bazy danych
- Ustalanie liczby porcji dla każdego posiłku
- Elastyczny zakres dat (od-do)
- Zarządzanie wieloma planami żywieniowymi

### **Listy zakupów**
- Automatyczne generowanie list zakupów na podstawie planera
- Zaznaczanie zakupionych produktów
- Edycja ilości i jednostek w locie
- Usuwanie niepotrzebnych pozycji
- Intuicyjny interfejs do zarządzania zakupami

### **Archiwizacja przepisów**
- Przenoszenie przepisów do archiwum zamiast trwałego usuwania
- Przeglądanie i przywracanie zarchiwizowanych przepisów

### **Archiwizacja danych (kopie zapasowe)**
- Tworzenie lokalnych kopii zapasowych bazy danych (eksport do archiwum)
- Przywracanie danych z wybranego archiwum
- Przeglądanie i zarządzanie listą kopii zapasowych
- Nadawanie własnych nazw plikom archiwum

### **Profil użytkownika i chmura (Supabase)**
- Rejestracja i logowanie przez e-mail i hasło (Supabase Auth)
- Automatyczne przywracanie sesji po ponownym uruchomieniu aplikacji
- Włączanie/wyłączanie synchronizacji danych z chmurą
- Ręczne wymuszanie synchronizacji (Force Sync)
- Synchronizacja w tle z powiadomieniem o postępie
- Pobieranie preferencji użytkownika z chmury

### **Ustawienia aplikacji**
- Wybór języka interfejsu (Polski, English, Deutsch, Français, Español, 한국어)
- Wybór motywu kolorystycznego (12 wariantów: Default, Nature, Forest, Autumn, Warm, Sunset, Vibrant, Monochrome, Navy, Mint, Sky, Bubblegum)
- Przełączanie trybu jasny / ciemny
- Wybór czcionki spośród 18 dostępnych krojów pisma
- Regulacja rozmiaru czcionki
- Kreator pierwszego uruchomienia (Setup Wizard)

---

## Integracja z Supabase

Aplikacja integruje się z platformą [Supabase](https://supabase.com/) jako backendem chmurowym.

### **Uwierzytelnianie**
- `SupabaseAuthService` – zarządzanie rejestracją, logowaniem i wylogowywaniem
- `SecureStorageAuthTokenStore` – bezpieczne przechowywanie tokenów JWT w pamięci urządzenia
- `BearerTokenHandler` – automatyczne dołączanie tokenów do żądań HTTP
- `JwtValidator` – lokalna walidacja tokenów bez zależności od ASP.NET Core
- Automatyczne odświeżanie tokenów (`AutoRefreshToken`)

### **Synchronizacja danych (Cloud Sync)**
- `SupabaseSyncService` – zarządzanie kolejką synchronizacji i przetwarzaniem przyrostowych zmian
- `SupabaseCrudService` – pełny CRUD dla wszystkich encji (przepisy, składniki, plany, etykiety, foldery, listy zakupów)
- `SupabaseRestClient` – bezpośredni klient REST do Supabase PostgREST API z obsługą nagłówków autoryzacyjnych
- Synchronizacja partii danych (batch upsert) dla szybkiego pierwszego importu
- Obsługa błędów fatalnych (RLS/auth) z automatycznym zatrzymaniem synchronizacji
- Synchronizacja preferencji użytkownika z chmurą

### **Deduplikacja danych**
- `DeduplicationService` – wykrywanie i usuwanie duplikatów podczas synchronizacji
- Tryb **Cloud-First**: dane z chmury nadpisują lokalne (ID z chmury wygrywa)
- Tryb **Local-First**: dane lokalne nadpisują chmurę (duplikaty w chmurze są usuwane przed wysyłką)
- Dopasowanie składników: nazwa + kalorie + białko + tłuszcze + węglowodany (tolerancja 0.1)
- Dopasowanie przepisów: nazwa + makro + liczba składników + nazwy składników

### **Synchronizowane encje**
- Przepisy (`Recipe`) i ich składniki
- Składniki (`Ingredient`)
- Foldery (`Folder`)
- Etykiety (`RecipeLabel`)
- Plany posiłków (`Plan`, `PlannedMeal`)
- Pozycje listy zakupów (`ShoppingListItem`)
- Preferencje użytkownika (`UserPreferences`)

---

## Architektura aplikacji

### **Technologie**
- **Framework**: .NET MAUI (Multi-platform App UI)
- **Wersja .NET**: 9.0
- **Lokalna baza danych**: SQLite z Entity Framework Core
- **Backend chmurowy**: Supabase (Auth + PostgREST)
- **Wzorce**: MVVM (Model-View-ViewModel)
- **DI**: Wbudowany Dependency Injection
- **UI**: XAML z Material Design

### **Wzorzec MVVM**
Aplikacja wykorzystuje wzorzec MVVM z:
- **Models**: Klasy reprezentujące dane (`Recipe`, `Ingredient`, `Plan`, `Folder`, `RecipeLabel`, …)
- **Views**: Widoki XAML definiujące interfejs użytkownika
- **ViewModels**: Logika prezentacji i wiązanie danych

### **Baza danych**
- **SQLite**: Lokalna baza danych na urządzeniu
- **Entity Framework Core**: ORM do zarządzania danymi
- **Migracje**: Automatyczne tworzenie i aktualizacja schematu
- **Seed Data**: Automatyczne wypełnianie przykładowymi danymi przy pierwszym uruchomieniu

---

## Rozpoczęcie pracy

### **Wymagania**
- Visual Studio 2022 (17.8+) lub Visual Studio Code
- .NET 9.0 SDK
- Workloads dla .NET MAUI:
  - Android
  - iOS (opcjonalnie)
  - Windows (opcjonalnie)
  - macOS (opcjonalnie)

### **Instalacja**

1. **Sklonuj repozytorium**
   ```bash
   git clone https://github.com/[twoja-nazwa]/FoodBookApp.git
   cd FoodBookApp
   ```

2. **Przywróć pakiety NuGet**
   ```bash
   dotnet restore
   ```

3. **Zbuduj projekt**
   ```bash
   dotnet build
   ```

4. **Uruchom aplikację**
   ```bash
   # Android
   dotnet run --framework net9.0-android

   # Windows
   dotnet run --framework net9.0-windows10.0.19041.0
   ```

### **Pierwsze uruchomienie**
1. Przy pierwszym uruchomieniu baza danych zostanie automatycznie utworzona
2. Aplikacja załaduje przykładowe składniki z pliku `ingredients.json`
3. Zostanie uruchomiony kreator konfiguracji (Setup Wizard)
4. Opcjonalnie: zaloguj się lub zarejestruj konto Supabase, aby włączyć synchronizację w chmurze

---

## Instrukcja użytkowania

### **Dodawanie składników**
1. Przejdź do zakładki "Składniki"
2. Naciśnij "Dodaj składnik"
3. Wypełnij formularz z wartościami odżywczymi
4. Zapisz składnik

### **Tworzenie przepisów**
1. Przejdź do zakładki "Przepisy"
2. Naciśnij "Dodaj przepis"
3. Wybierz tryb:
   - **Ręczny**: Wprowadź dane samodzielnie
   - **Import**: Podaj URL strony z przepisem
4. Dodaj składniki i ich ilości
5. Opcjonalnie: przypisz folder lub etykiety
6. Zapisz przepis

### **Planowanie posiłków**
1. Przejdź do zakładki "Planer"
2. Wybierz zakres dat (od-do)
3. Ustaw liczbę posiłków dziennie
4. Dla każdego dnia:
   - Wybierz przepisy z listy
   - Ustaw liczbę porcji przyciskami +/-
   - Dodaj lub usuń posiłki
5. Zapisz plan

### **Generowanie listy zakupów**
1. Utwórz plan posiłków w Planerze
2. Przejdź do "Listy zakupów"
3. Otwórz wygenerowaną listę
4. Podczas zakupów:
   - Zaznaczaj kupione produkty
   - Edytuj ilości jeśli potrzeba
   - Usuwaj niepotrzebne pozycje

### **Logowanie i synchronizacja z chmurą**
1. Przejdź do zakładki "Profil"
2. Wprowadź e-mail i hasło, a następnie naciśnij "Zaloguj" (lub "Zarejestruj" dla nowego konta)
3. Po zalogowaniu włącz przełącznik "Włącz synchronizację z chmurą"
4. Naciśnij "Wymuś synchronizację", aby natychmiast zsynchronizować dane
5. Możesz wybrać synchronizację w tle, aby kontynuować korzystanie z aplikacji podczas syncu

### **Kopie zapasowe danych**
1. Przejdź do ustawień → "Archiwizacja danych"
2. Podaj opcjonalną nazwę dla archiwum
3. Naciśnij "Utwórz archiwum"
4. Aby przywrócić dane: wybierz archiwum z listy i naciśnij ikonę przywracania

---

## Personalizacja

### **Motywy kolorystyczne**
Dostępne 12 motywów kolorystycznych: **Default**, **Nature**, **Forest**, **Autumn**, **Warm**, **Sunset**, **Vibrant**, **Monochrome**, **Navy**, **Mint**, **Sky**, **Bubblegum**.

### **Tryb jasny / ciemny**
Aplikacja obsługuje jasny i ciemny motyw, automatycznie dostosowując się do ustawień systemu lub według preferencji użytkownika.

### **Czcionki**
Dostępnych 18 krojów pisma, m.in.: OpenSans, Barlow Condensed, DynaPuff, Cherry Bomb One, Kalam, Yellowtail, Poiret One i inne.

### **Rozmiar czcionki**
Możliwość regulacji rozmiaru tekstu w całej aplikacji.

### **Język interfejsu**
Obsługiwane języki: **Polski**, **English**, **Deutsch**, **Français**, **Español**, **한국어**.

### **Układy responsywne**
Interfejs automatycznie dostosowuje się do różnych rozmiarów ekranów i orientacji urządzenia.

---

## Konfiguracja rozwoju

### **Główne pakiety NuGet**

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="HtmlAgilityPack" />
<PackageReference Include="Newtonsoft.Json" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" />
<PackageReference Include="CommunityToolkit.Maui" />
<PackageReference Include="Supabase" />
<PackageReference Include="Microsoft.IdentityModel.Tokens" />
<PackageReference Include="Sharpnado.CollectionView" />
```

### **Dodawanie nowych funkcji**

1. **Nowy model**: Dodaj klasę w folderze `Models/`
2. **Nowy serwis**: Utwórz interfejs i implementację w `Services/`
3. **Nowy widok**: Dodaj XAML i code-behind w `Views/`
4. **Nowy ViewModel**: Utwórz klasę w `ViewModels/`
5. **Rejestracja**: Dodaj do DI w `MauiProgram.cs`

### **Migracje bazy danych**

```bash
# Dodanie nowej migracji
dotnet ef migrations add NazwaMigracji

# Aktualizacja bazy danych
dotnet ef database update
```

---

## Wkład w projekt

