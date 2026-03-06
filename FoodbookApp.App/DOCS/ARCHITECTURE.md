# ARCHITECTURE.md

## 1. Cel dokumentu
Dokument opisuje architekturę aplikacji FoodBook App: warstwy, wzorce, zależności, decyzje techniczne oraz wytyczne rozwoju i rozszerzalności.

---
## 2. Kontekst biznesowy
FoodBook App to wieloplatformowa (Android, iOS, Windows, macOS) aplikacja mobilna do:
- zarządzania bazą przepisów,
- planowania posiłków,
- generowania list zakupów,
- archiwizacji planów,
- analizy makro i kalorii.

---
## 3. Warstwy i podział odpowiedzialności
| Warstwa | Zakres | Technologie / Artefakty |
|---------|--------|-------------------------|
| Prezentacji | UI, XAML Pages, Shell | .NET MAUI XAML, Shell, ResourceDictionary, Style, Converters |
| ViewModel (MVVM) | Logika prezentacji, komendy, stan UI | `ViewModels/`, INotifyPropertyChanged |
| Serwisy aplikacyjne | Operacje domenowe / agregacja danych | `Services/` |
| Dostęp do danych | ORM i persystencja local-first | EF Core, SQLite, EF Migrations |
| Modele domenowe i DTO | Encje i kontrakty transportowe | `Models/`, `Models/DTOs/` |
| Infrastruktura | DI, lokalizacja, import, sync cloud | `MauiProgram.cs`, Supabase config/client, konwertery |

---
## 4. Wzorce projektowe
- MVVM
- Dependency Injection
- Command Pattern
- Observer (`INotifyPropertyChanged`)
- Local-first + kolejka synchronizacji

---
## 5. Struktura projektu
Główne katalogi: `Models/`, `Data/`, `Services/`, `ViewModels/`, `Views/`, `Localization/`, `Resources/`, `Converters/`, `Platforms/`.

---
## 6. Modele domenowe (skrót)
- `Recipe`, `Ingredient`, `PlannedMeal`, `PlannerDay`, `Plan`
- Relacje: Recipe (1) -> (N) Ingredient; planowanie opiera się na zakresie dat.

---
## 7. Data layer
- ORM: Entity Framework Core 9 + SQLite.
- Kontekst: `AppDbContext` (m.in. `Recipes`, `Ingredients`, `PlannedMeals`, `Plans`, `AuthAccounts`, `SyncQueue`, `SyncStates`).
- Migracje są aktywnie używane i wersjonowane w `Data/Migrations` (np. `20260126193304_InitialCreate`, `AppDbContextModelSnapshot`).
- Lokalna persystencja mechanizmów synchronizacji:
  - tabela `SyncQueue` (`SyncQueueEntry`) przechowuje operacje do wysłania,
  - tabela `SyncStates` (`SyncState`) przechowuje checkpoint i status synchronizacji per konto,
  - tabela `AuthAccounts` (`AuthAccount`) mapuje lokalny kontekst użytkownika na `SupabaseUserId`.
- Seed: `SeedData.InitializeAsync()` i `SeedIngredientsAsync()`.

---
## 8. Serwisy aplikacyjne
| Serwis | Cel |
|--------|-----|
| `IRecipeService` / `RecipeService` | CRUD przepisów |
| `IIngredientService` / `IngredientService` | Operacje na składnikach |
| `IPlannerService` / `PlannerService` | Zarządzanie planned meals |
| `IPlanService` / `PlanService` | Operacje na planach |
| `IShoppingListService` / `ShoppingListService` | Generowanie list zakupów |
| `LocalizationService` | Lokalizacja |
| `RecipeImporter` | Import przepisu z URL |

---
## 9. Cloud Sync / Supabase
### 9.1 Komponenty i modele
- `AuthAccount`: konto lokalne + mapowanie do użytkownika Supabase.
- `SyncState`: stan synchronizacji (checkpoint, timestamp, status, błąd).
- `SyncQueueEntry`: pojedyncza operacja kolejki (encja, akcja, payload, retry, status).

### 9.2 DTO (`FoodbookApp.App/Models/DTOs`)
Synchronizacja korzysta z DTO jako kontraktów transferowych i mapowania encji:
- `PlanDto`
- `RecipeDto`
- `IngredientDto`
- `ShoppingListItemDto`
- uzupełniająco: `PlannedMealDto`, `FolderDto`, `UserPreferencesDto`

### 9.3 Konfiguracja (`FoodbookApp.App/Properties/appsettings.json`)
- Sekcja `Supabase`: `Url`, `Key`
- Ustawienia JWT w `Authentication`: `ValidIssuer`, `ValidAudience`, `JwtSecret`

---
## 10. Przepływ danych synchronizacji (local-first)
1. Zmiana zapisywana jest lokalnie w SQLite.
2. Tworzony jest wpis `SyncQueueEntry` w `SyncQueue`.
3. Worker synchronizacji wysyła zmiany do Supabase.
4. Po sukcesie aktualizowany jest `SyncState`, a wpis kolejki oznaczany jako `Completed`.
5. Przy błędzie wpis przechodzi na `RetryPending` / `Failed`, licznik retry rośnie, stosowany jest backoff.
6. Przy błędach trwałych (np. auth/JWT) sync dla konta jest zatrzymany do odświeżenia sesji.

Model jest eventual-consistent i nie blokuje pracy offline.

---
## 11. Nawigacja i routing
- .NET MAUI Shell (`AppShell.xaml`)
- Rejestracja tras w `MauiProgram.cs`
- Nawigacja przez `Shell.Current.GoToAsync()`

---
## 12. Dependency Injection (DI)
- `DbContext` przez `AddDbContext`
- Serwisy domenowe i sync jako scoped/transient zgodnie z cyklem życia
- Lokalizacja i ustawienia jako singleton tam, gdzie uzasadnione

---
## 13. Bezpieczeństwo i prywatność (sync online)
- Tokeny i klucze sesji przechowywane w `SecureStorage` (bez logowania plaintext).
- Walidacja JWT (`ValidIssuer`, `ValidAudience`, `JwtSecret`, TTL).
- Minimalizacja danych: synchronizowane są wyłącznie pola wymagane biznesowo.
- Rekomendowane szyfrowanie lokalnej bazy na urządzeniu.
- Ryzyka: przejęcie tokenu, replay, konflikty multi-device, nadmiarowy payload.
- Mitigacje: rotacja tokenów, idempotencja, correlation/batch id, retry z backoff, redakcja logów.

---
## 14. Operacyjne scenariusze synchronizacji
### 14.1 Pierwsza synchronizacja
- Po logowaniu tworzony/aktualizowany jest `AuthAccount`.
- Następuje inicjalny merge local <-> cloud.
- Ustawiany jest checkpoint w `SyncState`.

### 14.2 Wymuszenie synchronizacji
- Ręczny trigger natychmiast przetwarza `SyncQueue`.
- Operacja jest idempotentna (nie powiela `Completed`).

### 14.3 Konflikt danych i strategia rozstrzygania
- Domyślnie: last-write-wins (`UpdatedAt` + wersjonowanie).
- Dla krytycznych encji możliwy merge pól lub decyzja użytkownika.

### 14.4 Tryb offline/online
- Offline: pełna praca na SQLite + narastająca kolejka.
- Online: flush kolejki, aktualizacja stanu, czyszczenie historii wg retencji.

---
## 15. Decyzje architektoniczne (ADR skrót)
| ID | Decyzja | Status | Uzasadnienie |
|----|---------|--------|--------------|
| ADR-01 | EF Core + SQLite | Zaakceptowane | Lokalna baza multi-platform |
| ADR-02 | Brak osobnych repozytoriów | Tymczasowe | Mniejszy narzut kodu |
| ADR-03 | Shell Navigation | Zaakceptowane | Spójny routing |
| ADR-04 | Manual caching w PlannerViewModel | Zaakceptowane | Lepsza responsywność |
| ADR-05 | Lokalizacja `.resx` | Zaakceptowane | Standard .NET |
| ADR-06 | Batch UI loading | Zaakceptowane | Płynność UI |
| ADR-07 | EF Migrations jako standard ewolucji schematu | Wdrożone | Kontrola zmian DB, także dla sync |
| ADR-08 | Local-first + Supabase sync queue | Wdrożone / aktywnie rozwijane | Offline-first + eventual consistency |

---
## 16. Ryzyka i mitigacje
| Ryzyko | Skutek | Mitigacja |
|--------|--------|----------|
| Niespójność migracji | Błędy uruchomienia / sync | Testy migracji + update DB w pipeline |
| Konflikty multi-device | Utrata spójności | Wersjonowanie, LWW, merge i UI konfliktów |
| Utrata/wyciek tokenu | Ryzyko bezpieczeństwa | SecureStorage, rotacja tokenów, redakcja logów |
| Kolejka retry rośnie bez końca | Koszt i opóźnienia sync | Backoff, limity retry, dead-letter policy |

---
## 17. Roadmap techniczny (skrót)
1. Synchronizacja Supabase: wdrożona, aktywnie rozwijana (monitoring, UX konfliktów, optymalizacje batch).
2. Rozszerzenie testów sync (unit, integration, scenariusze konfliktów).
3. Optymalizacja `SyncQueue`/`SyncStates` (indeksy, cleanup, telemetry operacyjna).
4. Moduł AI planowania posiłków.
5. Dalsze odchudzanie modeli UI przez DTO i mapowanie.
6. Audyt lokalizacji i pełne pokrycie zasobów.

---
## 18. Konwencje kodu (skrót)
- C# 13, nullable enabled.
- Metody async z sufiksem `Async`.
- Brak logiki biznesowej w code-behind.

---
## 19. Aktualizacja dokumentu
Dokument aktualizować przy każdej istotnej zmianie architektonicznej (warstwy, modele sync, migracje, bezpieczeństwo, ADR, roadmapa).

---
**Ostatnia aktualizacja:** 2026-03-06.  
**Właściciel dokumentu:** Zespół FoodBook App.
