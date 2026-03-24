# PROJECT-FILES.md

Aktualny, skrócony opis struktury repozytorium FoodbookApp.
Dokument ma charakter „high-level + kluczowe punkty orientacyjne” i nie zawiera pełnego, ręcznie utrzymywanego spisu wszystkich plików.

## 1) Struktura repo (high-level)

```text
FoodbookApp/
├── FoodbookApp.App/      # Główna aplikacja .NET MAUI
└── FoodbookApp.Tests/    # Testy (xUnit)
```

## 2) Moduły aplikacji (`FoodbookApp.App`)

```text
FoodbookApp.App/
├── Data/                 # EF Core DbContext + migracje
│   └── Migrations/
├── Models/               # Modele domenowe i kontrakty danych
│   └── DTOs/
├── Services/             # Logika aplikacyjna i integracje
│   ├── Auth/             # Uwierzytelnianie
│   ├── Supabase/         # Synchronizacja / usługi zewnętrzne
│   └── Archive/          # Archiwizacja danych
├── ViewModels/           # MVVM ViewModels
├── Views/                # Widoki XAML + code-behind
│   └── Components/       # Komponenty UI
├── Converters/           # Konwertery bindingów
├── Localization/         # Pliki .resx i designer
├── Resources/            # Obrazy, style, fonty, raw assets
├── Platforms/            # Kod specyficzny dla Android/iOS/Windows/macOS/Tizen
├── Properties/           # Konfiguracja uruchamiania
├── DOCS/                 # Dokumentacja projektu
├── Interfaces/           # Interfejsy współdzielone
├── Messages/             # Typy komunikatów
├── Utils/                # Narzędzia pomocnicze
├── Scripts/              # Skrypty pomocnicze
└── Trimming/             # Konfiguracja trimowania/AOT
```

### Uwaga o modułach wymaganych przez standard projektu
- `Components`: obecnie utrzymywane jako `Views/Components/`.
- `Helpers`: odpowiedniki narzędzi pomocniczych znajdują się w `Utils/`.
- `Behaviors`: obecnie brak wydzielonego katalogu top-level `Behaviors/` (dodać przy pierwszym behaviorze współdzielonym).

## 3) Kluczowe pliki startowe (`FoodbookApp.App`)

- `FoodbookApp.App/FoodbookApp.App.csproj`
- `FoodbookApp.App/MauiProgram.cs`
- `FoodbookApp.App/App.xaml`, `FoodbookApp.App/App.xaml.cs`
- `FoodbookApp.App/AppShell.xaml`, `FoodbookApp.App/AppShell.xaml.cs`

## 4) Testy (`FoodbookApp.Tests`)

```text
FoodbookApp.Tests/
├── FoodbookApp.Tests.csproj
├── *Tests.cs             # Testy jednostkowe i komponentowe
```

Przykładowe obszary pokryte testami: baza danych, składniki, przepisy, planner, archiwizacja, motywy oraz auth (Supabase).

## 5) Nowe obszary funkcjonalne (istotne dla orientacji)

- DTO: `FoodbookApp.App/Models/DTOs/`
- Auth: `FoodbookApp.App/Services/Auth/`
- Sync / integracje zewnętrzne: `FoodbookApp.App/Services/Supabase/`
- Migracje EF Core: `FoodbookApp.App/Data/Migrations/`

## 6) Zasada utrzymania tego dokumentu

Zamiast pełnego wyliczania wszystkich plików utrzymujemy tylko:
1. strukturę high-level,
2. kluczowe katalogi modułów,
3. najważniejsze pliki startowe,
4. katalog testów i główne obszary funkcjonalne.

Przy każdej zmianie struktury katalogów (dodanie, usunięcie, przeniesienie modułu) zaktualizuj sekcje 1–5.

---
**Ostatnio zweryfikowano strukturę:** 2026-03-06

## 7) Subskrypcje (dodane 2026-03-24)

Nowe elementy struktury:
- `Interfaces/ISubscriptionManagementService.cs` — kontrakt zarządzania planem.
- `Models/SubscriptionPlan.cs` i `Models/SubscriptionActionResult.cs` — model planu i wynik akcji UI.
- `Services/Subscription/MockSubscriptionManagementService.cs` — domyślny mock provider.
- `Services/Subscription/SupabaseEdgeSubscriptionManagementService.cs` — seam pod edge function.
- `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` — seam pod provider płatności.
