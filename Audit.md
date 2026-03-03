Znalezione problemy: -MauiProgram

        CreateMauiApp
            dbService.InitializeAsync().GetAwaiter().GetResult()

Uzasadnienie: -Blokowanie asynchronicznej inicjalizacji bazy na wątku startowym (`GetResult`) łamie paradygmat async/await i zwiększa ryzyko deadlocków oraz długiego "cold start" aplikacji.

Propozycja poprawy
- Przenieść inicjalizację bazy do w pełni asynchronicznego flow uruchamiania (np. etap bootstrap po starcie UI) i unikać synchronicznego oczekiwania.

---

Znalezione problemy: -MauiProgram

        CreateMauiApp
            Hardcoded Supabase URL/Key (linie z `new Supabase.Client(...)` oraz `new SupabaseRestClient(...)`)

Uzasadnienie: -Konfiguracja Supabase jest powielona i zaszyta na stałe w kodzie. To błąd architektoniczny (łamie SRP/DIP), utrudnia rotację kluczy i bezpieczne zarządzanie konfiguracją między środowiskami.

Propozycja poprawy
- Wstrzykiwać ustawienia przez `IOptions`/konfigurację i używać jednego źródła prawdy dla URL/Key.

---

Znalezione problemy: -DatabaseService

        InitializeAsync
            Preferences flag `DbInitialized` jako twardy warunek pomijania migracji

Uzasadnienie: -Jeżeli flaga zostanie ustawiona, kolejne uruchomienia pomijają migracje. To grozi niespójnością schematu po aktualizacjach aplikacji i błędami runtime.

Propozycja poprawy
- Zawsze sprawdzać `GetPendingMigrationsAsync`, a flagę traktować jedynie pomocniczo (np. telemetrycznie), nie jako warunek blokujący migrację.

---

Znalezione problemy: -SupabaseCrudService

        GetCurrentUserIdAsync
            ręczne parsowanie JWT przez `Split('.')` + `Convert.FromBase64String`

Uzasadnienie: -Ręczne odczytywanie `sub` bez walidacji tokenu tworzy ryzyko akceptacji niepoprawnych danych tożsamości i narusza zasady bezpieczeństwa.

Propozycja poprawy
- Korzystać z istniejącego, zwalidowanego źródła tożsamości (`_client.Auth.CurrentUser`) albo walidatora JWT z pełną walidacją podpisu/issuer/audience.

---

Znalezione problemy: -SupabaseCrudService

        klasa (całość)
            odpowiedzialność: inicjalizacja klienta + autoryzacja + mapowanie DTO + CRUD dla wielu agregatów

Uzasadnienie: -Klasa ma 1000+ linii i łączy wiele ról. To silne naruszenie SRP i utrudniona testowalność oraz utrzymanie.

Propozycja poprawy
- Rozdzielić na mniejsze serwisy/repozytoria per obszar (np. Recipes/Folders/Plans) i osobny komponent do autoryzacji sesji.

---

Znalezione problemy: -SupabaseSyncService

        klasa (całość)
            rozmiar ~2252 linii + wiele odpowiedzialności (kolejka, retry, timery, import cloud, deduplikacja, statusy)

Uzasadnienie: -Monolit serwisowy narusza SRP/OCP, utrudnia zmianę logiki synchronizacji oraz podnosi ryzyko regresji przy każdej modyfikacji.

Propozycja poprawy
- Podzielić na komponenty: `SyncQueueProcessor`, `CloudImportService`, `SyncScheduler`, `SyncStateService`.

---

Znalezione problemy: -SupabaseSyncService

        StartSyncTimer / StartCloudPollingTimer
            `new Timer(async _ => await ...)` (async callback w Timer)

Uzasadnienie: -`Timer` nie jest natywnie asynchroniczny; callback może się nawarstwiać przy wolnej sieci i uruchamiać nakładające się przebiegi. To ryzyko wydajnościowe i wyścigów.

Propozycja poprawy
- Użyć `PeriodicTimer` + pojedynczej pętli async z kontrolą anulowania i gwarancją pojedynczego przebiegu.

---

Znalezione problemy: -SupabaseSyncService

        EnableCloudSyncAsync / ProcessQueueAsync / RestartSyncTimerAsync
            fire-and-forget (`_ = StartInitialSyncAsync(ct)`, `_ = ProcessQueueAsync(ct)`, `_ = Task.Run(...)`)

Uzasadnienie: -Nieobserwowane zadania utrudniają kontrolę błędów i cyklu życia synchronizacji. Przy zamknięciu/wylogowaniu mogą pozostać „osierocone” operacje.

Propozycja poprawy
- Wprowadzić centralny orchestrator z tokenem anulowania i jawnie śledzonymi Taskami.

---

Znalezione problemy: -SupabaseSyncService

        PollCloudForChangesAsync
            brak propagacji `CancellationToken` (`FetchAllCloudDataAsync()` i `ImportCloudDataToLocalAsync(..., default)`)

Uzasadnienie: -Ignorowanie anulowania utrudnia bezpieczne zatrzymanie procesu (logout/dispose) i może podtrzymywać kosztowne operacje I/O.

Propozycja poprawy
- Przekazywać `CancellationToken` przez cały łańcuch wywołań i respektować go w punktach zapisu/odczytu.

---

Znalezione problemy: -SupabaseSyncService

        ProcessSingleEntryAsync + Process{Entity}Async
            switch po stringach (`EntityType`) i duplikacja logiki operacji

Uzasadnienie: -Łamie OCP: dodanie nowego typu encji wymaga modyfikacji wielu sekcji `switch`. Rosnący koszt utrzymania i większa podatność na błędy.

Propozycja poprawy
- Zastosować strategię/handler per encja (`ISyncEntryHandler`) rejestrowane w DI.

---

Znalezione problemy: -SupabaseSyncService

        ProcessQueueAsync / ForceSyncAllAsync
            `SaveChangesAsync` wykonywane wielokrotnie wewnątrz pętli

Uzasadnienie: -Duża liczba zapisów transakcyjnych dla każdej pozycji obniża wydajność (I/O + locki DB), szczególnie dla większych kolejek.

Propozycja poprawy
- Grupować zmiany i wykonywać zapis partiami lub po zakończeniu logicznego batcha.

---

Znalezione problemy: -DeduplicationService

        cache pól `_cloudIngredients`, `_cloudRecipes`, `_localBaseIngredients`
            brak TTL/strategii wygaszania i utrzymanie danych przez cały lifecycle singletona

Uzasadnienie: -Cache bez wygaszania może się starzeć i zwiększać zużycie pamięci; dane deduplikacji mogą odbiegać od stanu chmury.

Propozycja poprawy
- Wprowadzić TTL + odświeżanie wersjonowane (np. `IMemoryCache` i timestamp ostatniego udanego fetchu).

---

Znalezione problemy: -PreferencesService

        SyncToCloudAsync
            użycie globalnego `MauiProgram.ServiceProvider` (Service Locator)

Uzasadnienie: -Silne sprzężenie z globalnym kontenerem łamie DIP, utrudnia testowanie i rozszerzanie oraz zaciera granice odpowiedzialności.

Propozycja poprawy
- Wstrzykiwać wymagane zależności przez konstruktor i usunąć bezpośrednie odwołania do statycznego ServiceProvidera.

---

Znalezione problemy: -PreferencesService

        SaveLanguage/SaveTheme/SaveColorTheme/... -> `_ = SyncToCloudAsync()`
            fire-and-forget synchronizacji po każdej zmianie preferencji

Uzasadnienie: -Wiele szybkich zmian ustawień może wywołać lawinę równoległych synchronizacji, zwiększając koszt sieci i ryzyko wyścigów stanu.

Propozycja poprawy
- Dodać debouncing/coalescing oraz pojedynczą kolejkę aktualizacji preferencji.

---

Znalezione problemy: -AppEvents

        RaiseIngredientsChangedSync
            `Task.WhenAll(tasks).Wait()`

Uzasadnienie: -Blokada synchroniczna na asynchronicznych handlerach zwiększa ryzyko deadlocka i przycięć UI.

Propozycja poprawy
- Usunąć wariant synchroniczny albo zastąpić go pełnym `await` w łańcuchu wywołań.

---

Znalezione problemy: -AppEvents

        RaiseRecipeSaved / DrainPendingRecipeSavedEvents
            nieograniczona kolejka `_pendingRecipeSavedEvents`

Uzasadnienie: -Brak limitu rozmiaru kolejki może prowadzić do niekontrolowanego wzrostu pamięci przy długim braku subskrybentów.

Propozycja poprawy
- Wprowadzić limit kolejki, mechanizm drop/merge lub trwały bufor z kontrolą rozmiaru.

---

Sugestie dotyczące jakości: -ISupabaseCrudService

        interfejs (całość)
            30+ metod dla wielu domen

Uzasadnienie Propozycja poprawy
- Interfejs jest zbyt szeroki (naruszenie ISP), co utrudnia mockowanie i izolowane testy.
- Podzielić interfejs na mniejsze kontrakty domenowe (np. `IRecipeCloudRepository`, `IPlanCloudRepository`, `IUserPreferencesCloudRepository`).

---

Sugestie dotyczące jakości: -SupabaseAuthService

        SignInAsync / SetSessionAsync
            bardzo szczegółowe logowanie przebiegu uwierzytelnienia

Uzasadnienie Propozycja poprawy
- Nadmiar logów diagnostycznych zwiększa ryzyko wycieku informacji operacyjnych i utrudnia analizę realnych błędów.
- Wprowadzić poziomy logowania oraz maskowanie danych, a w produkcji ograniczyć logi do zdarzeń istotnych.

---

Sugestie dotyczące jakości: -App (App.xaml.cs)

        konstruktor + auto-login
            uruchamianie logiki przez `Task.Run` i korzystanie z globalnego ServiceProvidera

Uzasadnienie Propozycja poprawy
- Start aplikacji staje się trudny do deterministycznego testowania i monitorowania.
- Przenieść bootstrap do dedykowanego `IStartupOrchestrator` z jawnym cyklem życia i anulowaniem.

---

Sugestie dotyczące jakości: -SupabaseSyncService

        ImportCloudDataToLocalAsync / ForceImportCloudDataToLocalAsync
            duplikacja bardzo podobnej logiki importu

Uzasadnienie Propozycja poprawy
- Powielony kod zwiększa koszt utrzymania i ryzyko niespójnych poprawek.
- Wydzielić współdzieloną warstwę mapowania/merge i użyć jednej ścieżki importu z parametryzacją trybu.
