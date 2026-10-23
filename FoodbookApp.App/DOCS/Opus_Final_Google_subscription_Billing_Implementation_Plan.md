Tak. Poniżej masz poprawioną wersję planu — zachowuję jego strukturę i poziom szczegółowości, ale usuwam elementy niezgodne z Twoją aktualną architekturą: niepotrzebne kolumny w `users`, błędne sekrety, stare założenia dotyczące Billing Library, brak realnego uwzględnienia istniejących `subscription_purchases` i `subscription_events` oraz kilka problemów z flow zmiany planu.

# Opus Final — Google Play Subscription Billing Implementation Plan

## FoodbookApp · .NET MAUI · Supabase Backend

---

## 1. Streszczenie wykonawcze

Niniejszy plan opisuje pełną implementację subskrypcji Google Play Billing w aplikacji FoodbookApp (.NET MAUI) z weryfikacją po stronie serwera za pomocą Supabase Edge Functions.

Celem jest zastąpienie obecnego `MockSubscriptionManagementService` prawdziwą integracją z Google Play, zachowując istniejącą architekturę:

```text
ISubscriptionManagementService
        ↓
FeatureAccessService
        ↓
ProfilePage.xaml.cs
```

oraz istniejący mechanizm `SubscriptionOperationEntry`.

Architektura ma wykorzystywać:

* Google Play Billing po stronie aplikacji Android,
* Google Play Developer API po stronie backendu,
* Supabase Edge Functions jako backend weryfikujący zakupy,
* `subscription_purchases` jako tabelę przechowującą stan i dane zakupów,
* `subscription_events` jako tabelę historii zdarzeń,
* `users` jako tabelę aktualnego stanu uprawnień użytkownika,
* Google Play Real-Time Developer Notifications (RTDN) do synchronizacji lifecycle subskrypcji.

### Source of Truth

| Aspekt                                     | Obecny mock                                      | Google Play Billing                                |
| ------------------------------------------ | ------------------------------------------------ | -------------------------------------------------- |
| Kto decyduje o premium?                    | Aplikacja sama                                   | Google Play → backend weryfikuje → Supabase        |
| Skąd pochodzi PurchaseToken?               | Brak                                             | Google Play BillingClient                          |
| Kto weryfikuje zakup?                      | Brak                                             | Supabase Edge Function + Google Play Developer API |
| Kto aktualizuje `users.IsPremium`?         | `MockSubscriptionManagementService` bezpośrednio | Supabase Edge Functions po pozytywnej weryfikacji  |
| Gdzie przechowywany jest purchase token?   | Brak                                             | `subscription_purchases`                           |
| Gdzie przechowywana jest historia eventów? | Brak                                             | `subscription_events`                              |
| Acknowledge                                | Brak                                             | Backend po pozytywnej weryfikacji                  |
| Lifecycle (renew/cancel/expire)            | Brak                                             | Google Play RTDN → Pub/Sub → `handle-rtdn`         |
| Aktualny dostęp użytkownika                | Mock                                             | `users.is_premium` + `premium_to`                  |

### Główna zasada architektoniczna

Tabela `users` przechowuje **aktualny, zdenormalizowany stan dostępu użytkownika**.

Tabela `subscription_purchases` przechowuje **konkretne subskrypcje i dane zakupów Google Play**.

Tabela `subscription_events` przechowuje **historię zdarzeń i zmian stanu**.

Nie należy dodawać do `users` kolumn takich jak:

```text
google_purchase_token
google_product_id
google_order_id
subscription_state
auto_renewing
```

ponieważ dane te należą do modelu subskrypcji i są już przechowywane w dedykowanych tabelach.

---

## 2. Obecna architektura (analiza stanu)

### 2.1 Flow subskrypcji (mock)

```text
User klika "Kup Premium" w ProfilePage
    ↓
ProfilePage.xaml.cs → ExecuteSubscriptionActionAsync()
    ↓
ISubscriptionManagementService.ChangePlanAsync(targetPlan)
    ↓
MockSubscriptionManagementService:
  1. Tworzy SubscriptionOperationEntry (Pending)
  2. Bezpośrednio zapisuje do tabeli users:
     - IsPremium = true/false
     - PremiumFrom = now
     - PremiumTo = now + 1month / 1year
     - UserType = 0 / 1
  3. Zapisuje snapshot do SecureStorage
  4. Oznacza operację jako Completed
    ↓
ProfilePage → HandleSubscriptionActionResultAsync()
  - SaveSubscriptionPlanChoice() do Preferences
  - FeatureAccessService.RefreshAccessAsync()
  - RefreshSubscriptionSectionAsync()
```

### 2.2 Kluczowe pliki

| Plik                                                                    | Rola                                                                                                              |
| ----------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Interfaces/ISubscriptionManagementService.cs`                          | Kontrakt: `ChangePlanAsync`, `CancelSubscriptionAsync`, `GetPendingOperationAsync`, `ResumePendingOperationAsync` |
| `Services/Subscription/MockSubscriptionManagementService.cs`            | Obecna implementacja testowa                                                                                      |
| `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` | Implementacja Google Play Billing                                                                                 |
| `Services/Subscription/SupabaseEdgeSubscriptionManagementService.cs`    | Opcjonalny wrapper do wywoływania Edge Functions                                                                  |
| `Services/FeatureAccessService.cs`                                      | Odczytuje `users` i buduje `FeatureAccessSnapshot`                                                                |
| `Views/ProfilePage.xaml.cs`                                             | UI subskrypcji i wykonywanie operacji                                                                             |
| `Views/ProfilePage.xaml`                                                | Layout UI                                                                                                         |
| `Models/SubscriptionOperationEntry.cs`                                  | Lokalna/operacyjna reprezentacja pending operacji                                                                 |
| `Models/SubscriptionPlan.cs`                                            | Enum: `Free`, `PremiumMonthly`, `PremiumYearly`                                                                   |
| `Models/SubscriptionActionResult.cs`                                    | Wynik operacji subskrypcji                                                                                        |
| `Models/SubscriptionPendingOperation.cs`                                | DTO pendingowej operacji                                                                                          |
| `Models/SubscriptionActionState.cs`                                     | `Completed`, `Pending`, `Failed`                                                                                  |
| `MauiProgram.cs`                                                        | Rejestracja implementacji DI                                                                                      |

### 2.3 Tabela `users` w Supabase

Aktualny schemat:

```sql
create table public.users (
  id uuid not null,
  email text null,
  registration_date timestamp with time zone null default now(),
  user_type integer null default 0,
  is_premium boolean null default false,
  premium_from timestamp with time zone null,
  premium_to timestamp with time zone null,
  created_at timestamp with time zone null default now(),
  updated_at timestamp with time zone null default now(),
  ad_unlocks_json jsonb null default '[]'::jsonb,
  plan_count_this_month integer null default 0,
  active_lists_count integer null default 0,
  test_placeholder boolean null default false,

  constraint users_pkey primary key (id),
  constraint users_id_fkey
    foreign key (id)
    references auth.users (id)
    on delete cascade
);
```

### Kolumny związane z premium

```text
is_premium
premium_from
premium_to
user_type
```

Te kolumny są wystarczające do przechowywania aktualnego dostępu użytkownika.

Nie należy dodawać do `users` danych specyficznych dla Google Play, jeśli istnieją już tabele:

```text
subscription_purchases
subscription_events
```

---

## 3. Docelowa architektura (Google Play Billing)

### 3.1 Docelowy flow zakupu

```text
┌───────────────────────────────────────────────────────────┐
│  APLIKACJA (.NET MAUI / Android)                          │
│                                                           │
│  1. User klika "Kup Premium Monthly/Yearly"               │
│     ↓                                                     │
│  2. PaymentProviderSubscriptionManagementService          │
│     .ChangePlanAsync(targetPlan)                          │
│     ↓                                                     │
│  3. Google Play BillingClient                             │
│     → pobranie ProductDetails                             │
│     ↓                                                     │
│  4. BillingClient.LaunchBillingFlow()                     │
│     → Google Play purchase sheet                          │
│     ↓                                                     │
│  5. OnPurchasesUpdated callback                           │
│     → otrzymuje Purchase + PurchaseToken                  │
│     ↓                                                     │
│  6. Zapisanie SubscriptionOperationEntry jako Pending     │
│     ↓                                                     │
│  7. POST do Supabase Edge Function                        │
│     /verify-play-purchase                                  │
│     {                                                     │
│       productId,                                          │
│       purchaseToken,                                      │
│       packageName                                         │
│     }                                                     │
└───────────────────────────────────────────────────────────┘
                         ↓
┌───────────────────────────────────────────────────────────┐
│  SUPABASE EDGE FUNCTION: verify-play-purchase             │
│                                                           │
│  1. Walidacja requestu                                    │
│  2. Identyfikacja użytkownika z JWT                       │
│  3. Google Play Developer API:                            │
│     subscriptionsv2.get(purchaseToken)                    │
│                                                           │
│  4. Weryfikacja:                                          │
│     - packageName                                         │
│     - productId                                           │
│     - subscriptionState                                   │
│     - expiryTime                                          │
│                                                           │
│  5. Acknowledge jeśli wymagane                            │
│                                                           │
│  6. Upsert do subscription_purchases                      │
│                                                           │
│  7. Zapisanie subscription_events                         │
│                                                           │
│  8. Aktualizacja users:                                   │
│     - is_premium                                          │
│     - premium_from                                        │
│     - premium_to                                          │
│     - user_type                                           │
│                                                           │
│  9. Response:                                             │
│     {                                                     │
│       isValid,                                            │
│       plan,                                               │
│       premiumFrom,                                        │
│       premiumTo                                           │
│     }                                                     │
└───────────────────────────────────────────────────────────┘
                         ↓
┌───────────────────────────────────────────────────────────┐
│  APLIKACJA                                                │
│                                                           │
│  10. Edge Function → sukces                               │
│      → SubscriptionOperationEntry → Completed             │
│      → FeatureAccessService.RefreshAccessAsync()           │
│      → UI: "Premium aktywne"                              │
│                                                           │
│  11. Edge Function → timeout / błąd sieci                 │
│      → Operation → Pending                                │
│      → PurchaseToken pozostaje zapisany                    │
│      → retry po reconnect / ponownym uruchomieniu           │
└───────────────────────────────────────────────────────────┘
```

### 3.2 Flow zmiany planu

Zmiana planu z miesięcznego na roczny lub odwrotnie powinna wykorzystywać mechanizm zmiany istniejącej subskrypcji Google Play.

```text
User posiada Premium Monthly
    ↓
Kliknie "Przejdź na Premium Yearly"
    ↓
Aplikacja pobiera aktywny PurchaseToken
    ↓
Google Play BillingClient:
    SubscriptionUpdateParams
    + stary PurchaseToken
    + nowy ProductDetails / offerToken
    ↓
Google Play wykonuje zmianę subskrypcji
    ↓
Aplikacja otrzymuje nowy Purchase
    ↓
POST /verify-play-purchase
    ↓
Backend weryfikuje nowy token
    ↓
subscription_purchases zostaje zaktualizowane
subscription_events otrzymuje event
users zostaje zaktualizowane
```

Nie należy traktować zmiany planu jako zwykłego, niezależnego nowego zakupu.

### 3.3 Flow anulowania

```text
User klika "Anuluj subskrypcję"
    ↓
CancelSubscriptionAsync()
    ↓
Aplikacja otwiera Google Play Manage Subscriptions
    ↓
User anuluje auto-renewal
    ↓
Google Play:
    - subskrypcja pozostaje aktywna do expiryTime
    - auto-renew zostaje wyłączony
    ↓
Google Play wysyła RTDN
    ↓
Pub/Sub
    ↓
handle-rtdn
    ↓
Backend pobiera aktualny stan z Google Play API
    ↓
subscription_purchases zostaje zaktualizowane
subscription_events otrzymuje event
users pozostaje premium do premium_to
```

Po anulowaniu auto-renewal użytkownik nie powinien natychmiast tracić premium. Premium pozostaje aktywne do końca opłaconego okresu.

### 3.4 Flow przywracania zakupów (restore)

```text
User reinstaluje aplikację / używa nowego urządzenia
    ↓
Aplikacja uruchamia BillingClient
    ↓
QueryPurchasesAsync(SUBS)
    ↓
Google Play zwraca aktywne zakupy
    ↓
Dla każdego Purchase:
    POST /verify-play-purchase
    ↓
Backend:
    - weryfikuje token
    - sprawdza packageName
    - sprawdza productId
    - pobiera aktualny stan z Google Play
    ↓
subscription_purchases zostaje zaktualizowane
users zostaje zaktualizowane
    ↓
FeatureAccessService.RefreshAccessAsync()
```

---

## 4. Szczegółowy plan implementacji

### Krok 1: Konfiguracja Google Play Console

**Gdzie**: Google Play Console → Twoja aplikacja

### Subskrypcje

Utwórz dwa produkty subskrypcyjne:

| Product ID                 | Nazwa                       | Okres     |
| -------------------------- | --------------------------- | --------- |
| `foodbook_premium_monthly` | Foodbook Premium Miesięczny | 1 miesiąc |
| `foodbook_premium_yearly`  | Foodbook Premium Roczny     | 1 rok     |

Każda subskrypcja powinna mieć:

```text
Base plan:
    AUTO_RENEWING
```

Zalecana konfiguracja:

```text
Grace period:
    7 dni

Resubscribe:
    włączone
```

Oferta free trial lub introductory pricing jest opcjonalna i może zostać dodana później.

### License testers

Dodaj konto testowe w:

```text
Google Play Console
→ Settings
→ License testing
```

### Internal testing

Aplikacja musi być dostępna przez odpowiedni track testowy Google Play, aby testować rzeczywiste Google Play Billing.

Rekomendowana kolejność:

```text
Internal testing
    ↓
tester dodany jako license tester
    ↓
tester instaluje aplikację z Google Play
    ↓
testuje zakup
```

Nie należy zakładać, że zwykłe uruchomienie aplikacji bezpośrednio z Visual Studio będzie równoważne pełnemu środowisku Google Play Billing.

---

### Krok 2: Konfiguracja Google Cloud + Service Account

**Cel**: Supabase Edge Functions muszą mieć dostęp do Google Play Developer API.

#### 1. Włącz API

W projekcie Google Cloud:

```text
Google Play Android Developer API
```

musi być włączone.

Projekt:

```text
foodbook-497815
```

#### 2. Service Account

Utwórz Service Account w:

```text
Google Cloud Console
→ IAM & Admin
→ Service Accounts
```

Utwórz klucz JSON.

Cały JSON trafi do Supabase jako:

```text
GOOGLE_SERVICE_ACCOUNT_JSON
```

#### 3. Google Play Console

W:

```text
Google Play Console
→ Settings
→ Developer account
→ API access
```

połącz odpowiedni projekt Google Cloud i nadaj Service Account odpowiednie uprawnienia do aplikacji.

Service Account musi mieć uprawnienia umożliwiające wykonywanie operacji związanych z subskrypcjami i zakupami.

Minimalny zakres uprawnień powinien być zgodny z operacjami wykonywanymi przez Edge Functions:

```text
- odczyt subskrypcji
- acknowledge zakupów
```

Nie należy nadawać większych uprawnień niż potrzebne.

---

### Krok 3: Konfiguracja Supabase Secrets

W Supabase Edge Functions używane są następujące sekrety:

```text
GOOGLE_SERVICE_ACCOUNT_JSON
```

Cały JSON klucza Service Account.

```text
SERVICE_ROLE_KEY
```

Supabase Service Role Key.

```text
FOODBOOK_RTDN_SECRET
```

Długi, losowy sekret zabezpieczający endpoint RTDN.

Przykład endpointu RTDN:

```text
https://gscbdvezastxpyndkauh.supabase.co/functions/v1/handle-rtdn?secret=<FOODBOOK_RTDN_SECRET>
```

Wartość sekretu nie powinna być przechowywana w kodzie aplikacji.

Package name nie jest sekretem:

```text
com.ppapps.foodbookapp
```

Może być stałą w kodzie.

---

### Krok 4: Konfiguracja Google Play RTDN

Istniejący topic:

```text
projects/foodbook-497815/topics/foodbook-play-notifications
```

jest używany przez Google Play do publikowania Real-Time Developer Notifications.

Do tego topicu należy utworzyć push subscription.

Przykład:

```text
Subscription ID:
foodbook-rtdn-push
```

Delivery type:

```text
Push
```

Push endpoint:

```text
https://gscbdvezastxpyndkauh.supabase.co/functions/v1/handle-rtdn?secret=<FOODBOOK_RTDN_SECRET>
```

Architektura:

```text
Google Play
    ↓
Pub/Sub Topic
foodbook-play-notifications
    ↓
Pub/Sub Push Subscription
foodbook-rtdn-push
    ↓ HTTP POST
Supabase Edge Function
handle-rtdn
```

Nie należy tworzyć drugiego topicu, jeśli istniejący topic jest już skonfigurowany w Google Play Console.

### Odpowiedzialność RTDN

RTDN informuje backend, że coś się zmieniło.

RTDN nie powinien być traktowany jako pełne źródło danych o subskrypcji.

Po otrzymaniu RTDN:

```text
1. Decode notification
2. Odczytaj purchaseToken
3. Odczytaj notificationType
4. Pobierz aktualny stan z Google Play Developer API
5. Zaktualizuj subscription_purchases
6. Zapisz subscription_events
7. Zaktualizuj users
```

---

### Krok 5: Mapowanie Product ID → SubscriptionPlan

**Plik**:

```text
Constants/BillingConstants.cs
```

```csharp
namespace FoodbookApp.Constants;

public static class BillingConstants
{
    public const string PackageName = "com.ppapps.foodbookapp";

    public const string MonthlyProductId =
        "foodbook_premium_monthly";

    public const string YearlyProductId =
        "foodbook_premium_yearly";

    public static readonly Dictionary<string, SubscriptionPlan>
        ProductPlanMap = new()
        {
            [MonthlyProductId] =
                SubscriptionPlan.PremiumMonthly,

            [YearlyProductId] =
                SubscriptionPlan.PremiumYearly
        };

    public static readonly Dictionary<SubscriptionPlan, string>
        PlanProductMap = new()
        {
            [SubscriptionPlan.PremiumMonthly] =
                MonthlyProductId,

            [SubscriptionPlan.PremiumYearly] =
                YearlyProductId
        };

    public const string VerifyPurchaseEndpoint =
        "verify-purchase";

    public const string SyncSubscriptionEndpoint =
        "sync-play-subscription";
}
```

Nazwa endpointu musi odpowiadać rzeczywistej nazwie wdrożonej Edge Function.

---

### Krok 6: Weryfikacja istniejących tabel Supabase

Przed implementacją Edge Functions agent musi sprawdzić rzeczywisty schemat:

```text
subscription_purchases
subscription_events
```

Nie wolno zakładać nazw kolumn na podstawie planu.

Agent powinien uzyskać:

```sql
CREATE TABLE subscription_purchases ...
```

oraz:

```sql
CREATE TABLE subscription_events ...
```

i dopasować do nich kod.

Jeśli istniejące tabele są wystarczające, nie należy ich przebudowywać bez potrzeby.

### Odpowiedzialność `subscription_purchases`

Tabela powinna przechowywać dane związane z konkretną subskrypcją Google Play, np.:

```text
user_id
purchase_token
product_id
package_name
order_id
subscription_state
start_time
expiry_time
auto_renewing
acknowledgement_state
last_verified_at
```

Dokładne nazwy zależą od rzeczywistego schematu tabeli.

### Odpowiedzialność `subscription_events`

Tabela powinna przechowywać historię zdarzeń, np.:

```text
purchase_verified
subscription_renewed
subscription_canceled
subscription_expired
subscription_revoked
subscription_in_grace_period
subscription_on_hold
```

Każdy event powinien być możliwie idempotentny.

---

### Krok 7: NuGet — Google Play Billing Library

Należy użyć aktualnej stabilnej wersji bindingu kompatybilnej z aktualnym target frameworkiem projektu .NET MAUI.

Nie należy bezrefleksyjnie używać:

```text
7.1.1
```

z pierwotnego planu, ponieważ jest to starsza wersja.

Należy:

```text
1. Sprawdzić aktualną stabilną wersję NuGet bindingu
2. Sprawdzić kompatybilność z target frameworkiem
3. Dodać PackageReference tylko dla Androida
4. Zbudować projekt
5. Sprawdzić API konkretnej wersji bindingu
```

Przykładowo:

```xml
<ItemGroup Condition="$(TargetFramework.Contains('android'))">
  <PackageReference
      Include="Xamarin.Android.Google.BillingClient"
      Version="AKTUALNA_KOMPATYBILNA_WERSJA" />
</ItemGroup>
```

Agent nie powinien kopiować API z dokumentacji innej wersji Billing Library bez sprawdzenia rzeczywistego API dostępnego w zainstalowanym bindingu.

---

### Krok 8: Implementacja `PaymentProviderSubscriptionManagementService`

**Plik**:

```text
Services/Subscription/PaymentProviderSubscriptionManagementService.cs
```

Implementacja:

```csharp
public sealed class PaymentProviderSubscriptionManagementService
    : ISubscriptionManagementService
```

### Injected dependencies

W zależności od istniejącej architektury:

```text
- Supabase client / REST client
- IAccountService
- FeatureAccessService lub mechanizm refreshu
- ISecureStorageAdapter
- IClock
- ILogger
```

Nie należy dodawać nowych abstrakcji, jeśli istnieją już odpowiedniki w projekcie.

---

### `ChangePlanAsync`

```text
ChangePlanAsync(targetPlan)
```

#### Jeśli targetPlan == Free

Wywołaj:

```text
CancelSubscriptionAsync()
```

lub otwórz Google Play Manage Subscriptions.

Nie należy programmatically ustawiać `users.is_premium = false` natychmiast po kliknięciu anulowania.

---

#### Jeśli użytkownik nie ma aktywnej subskrypcji

```text
1. Map targetPlan → productId
2. Połącz BillingClient
3. Pobierz ProductDetails
4. Pobierz właściwy offerToken
5. Utwórz BillingFlowParams
6. Uruchom LaunchBillingFlow
7. Odbierz Purchase
8. Zapisz SubscriptionOperationEntry jako Pending
9. Wyślij purchaseToken do verify-purchase
10. Oczekuj na pozytywną weryfikację
11. Oznacz operację jako Completed
12. RefreshAccessAsync()
```

---

#### Jeśli użytkownik zmienia aktywny plan

Przykład:

```text
PremiumMonthly
        ↓
PremiumYearly
```

Należy:

```text
1. Pobrać aktywny purchaseToken
2. Pobrać ProductDetails nowego planu
3. Utworzyć SubscriptionUpdateParams
4. Przekazać stary purchaseToken
5. Uruchomić billing flow z parametrami zmiany subskrypcji
6. Odebrać nowy Purchase
7. Wysłać nowy token do verify-purchase
8. Backend zweryfikuje aktualny stan
9. Zaktualizować subscription_purchases
10. Zapisać event
11. Zaktualizować users
```

Nie należy traktować zmiany planu jako dwóch niezależnych subskrypcji.

---

### BillingClient lifecycle

```text
EnsureBillingClientConnectedAsync()
```

Powinna:

```text
1. Utworzyć BillingClient
2. Ustawić PurchasesUpdatedListener
3. Włączyć wymagane pending purchases zgodnie z wersją Billing Library
4. Uruchomić StartConnection
5. Oczekiwać na OnBillingSetupFinished
6. Obsłużyć BillingResponseCode
```

Wersja Billing Library może wymagać konkretnego API dla `EnablePendingPurchases`.

Agent musi używać API faktycznie dostępnego w zainstalowanej wersji bindingu.

---

### Google Play purchase flow

```text
LaunchPurchaseFlowAsync(productId)
```

Flow:

```text
1. QueryProductDetailsAsync
2. Znajdź odpowiedni ProductDetails
3. Znajdź odpowiedni offerToken
4. Utwórz ProductDetailsParams
5. Utwórz BillingFlowParams
6. LaunchBillingFlow
7. Oczekuj na OnPurchasesUpdated
```

Nie należy hardkodować ceny w aplikacji.

Ceny powinny pochodzić z:

```text
ProductDetails
```

Google Play jest źródłem aktualnej ceny i waluty.

---

### PurchasesUpdated callback

```text
OnPurchasesUpdated(
    BillingResult result,
    IList<Purchase>? purchases)
```

Obsługa:

```text
OK
    ↓
Purchase otrzymany
    ↓
PurchaseToken zapisany
    ↓
weryfikacja backendowa
```

```text
USER_CANCELED
    ↓
brak operacji
```

```text
inne błędy
    ↓
Pending lub Failed
zgodnie z retry policy
```

---

### Backend verification

```text
VerifyPurchaseWithBackendAsync(
    productId,
    purchaseToken,
    packageName)
```

Request:

```json
{
  "productId": "foodbook_premium_monthly",
  "purchaseToken": "...",
  "packageName": "com.ppapps.foodbookapp"
}
```

Użytkownik powinien być identyfikowany na backendzie na podstawie JWT Supabase, a nie na podstawie dowolnego `supabaseUserId` przesłanego przez klienta.

Jeżeli istniejąca architektura wymaga przesyłania user ID, backend musi zweryfikować, że odpowiada on użytkownikowi z JWT.

Request powinien zawierać:

```http
Authorization: Bearer <supabase_access_token>
```

Nie należy wysyłać:

```text
SERVICE_ROLE_KEY
```

do aplikacji mobilnej.

---

### Backend verification flow

```text
1. Supabase Edge Function otrzymuje request
2. Waliduje JWT użytkownika
3. Waliduje productId
4. Waliduje purchaseToken
5. Waliduje packageName
6. Tworzy OAuth access token dla Google API
7. Wywołuje subscriptionsv2.get
8. Weryfikuje packageName
9. Weryfikuje productId
10. Odczytuje aktualny subscriptionState
11. Odczytuje expiryTime
12. Acknowledge jeśli wymagane
13. Upsert subscription_purchases
14. Zapisuje subscription_events
15. Aktualizuje users
16. Zwraca wynik do aplikacji
```

---

### Aktualizacja `users`

Dla aktywnej subskrypcji:

```text
is_premium = true
premium_from = startTime
premium_to = expiryTime
user_type = 1
```

Dla wygasłej lub revoked subskrypcji:

```text
is_premium = false
premium_to = expiryTime
user_type = 0
```

Nie należy aktualizować premium wyłącznie na podstawie informacji przesłanej przez aplikację.

---

## 9. Edge Functions

### 9.1 `verify-purchase`

Cel:

```text
Weryfikacja pojedynczego zakupu.
```

Wołana przez:

```text
Aplikację po zakupie
Aplikację podczas restore
Aplikację podczas retry pending operation
```

Flow:

```text
Mobile App
    ↓
verify-purchase
    ↓
Google Play Developer API
    ↓
weryfikacja PurchaseToken
    ↓
acknowledge
    ↓
subscription_purchases
subscription_events
users
```

---

### 9.2 `handle-rtdn`

Cel:

```text
Obsługa lifecycle subskrypcji.
```

Wołana przez:

```text
Google Play
    ↓
Pub/Sub
    ↓
HTTP Push
```

Endpoint:

```text
https://gscbdvezastxpyndkauh.supabase.co/functions/v1/handle-rtdn?secret=<FOODBOOK_RTDN_SECRET>
```

Flow:

```text
1. Sprawdź secret
2. Odbierz Pub/Sub envelope
3. Odczytaj message.data
4. Zdekoduj Base64
5. Odczytaj DeveloperNotification
6. Odczytaj purchaseToken
7. Zapisz event idempotentnie
8. Pobierz aktualny stan z Google Play API
9. Zaktualizuj subscription_purchases
10. Zaktualizuj users
11. Zwróć HTTP 200
```

### Ważne

RTDN nie powinien aktualizować `users` wyłącznie na podstawie `notificationType`.

Przykład:

```text
SUBSCRIPTION_CANCELED
```

nie oznacza automatycznie:

```text
is_premium = false
```

Subskrypcja może być nadal aktywna do:

```text
expiryTime
```

Dlatego po RTDN backend powinien pobrać aktualny stan subskrypcji z Google Play Developer API.

---

### 9.3 `sync-play-subscription`

Ta funkcja jest opcjonalna.

Może zostać dodana, jeśli aplikacja wymaga osobnego endpointu do synchronizacji wielu tokenów.

MVP może działać bez niej:

```text
QueryPurchasesAsync
    ↓
verify-purchase dla każdego tokena
```

Osobna funkcja `sync-play-subscription` może zostać dodana później, gdy będzie realnie potrzebna.

---

## 10. Aktualizacja DI (MauiProgram.cs)

```csharp
#if ANDROID

builder.Services.AddScoped<
    ISubscriptionManagementService,
    PaymentProviderSubscriptionManagementService>();

#else

builder.Services.AddScoped<
    ISubscriptionManagementService,
    MockSubscriptionManagementService>();

#endif
```

Należy zachować istniejący kontrakt:

```text
ISubscriptionManagementService
```

Dzięki temu:

```text
ProfilePage
FeatureAccessService
```

nie muszą znać szczegółów Google Play Billing.

---

## 11. Aktualizacja UI

### ProfilePage.xaml.cs

Minimalne zmiany.

Obecny flow:

```text
ProfilePage
    ↓
ISubscriptionManagementService
    ↓
ChangePlanAsync
```

może pozostać bez zmian.

Należy jedynie upewnić się, że UI poprawnie obsługuje:

```text
Completed
Pending
Failed
```

### Po zakupie

```text
Pending
    ↓
"Trwa weryfikacja zakupu..."
```

```text
Completed
    ↓
"Premium aktywne"
```

```text
Failed
    ↓
"Nie udało się zweryfikować zakupu"
```

### Cancel

Tekst przycisku może brzmieć:

```text
Zarządzaj subskrypcją w Google Play
```

ponieważ aplikacja nie anuluje subskrypcji bezpośrednio.

---

## 12. `SubscriptionOperationEntry`

Model powinien przechowywać dane potrzebne do retry lokalnej operacji.

Rekomendowane pola:

```text
Id
AccountId
TargetPlan
Status
IdempotencyKey
RetryCount
LastError
CreatedAt
UpdatedAt
```

Dodatkowo dane Google Play:

```text
PaymentProvider
ProviderProductId
PurchaseToken
PurchaseTimeUtc
ExpiryTimeUtc
IsAcknowledged
LastProviderStatus
```

### Ważne

PurchaseToken jest wrażliwą informacją operacyjną.

Nie należy:

```text
- logować go w plaintext
- wysyłać go do analityki
- umieszczać go w publicznych logach
```

Może być przechowywany w bazie operacji pending, jeśli jest to potrzebne do retry.

Docelowe dane historyczne i zakupowe powinny znajdować się w:

```text
subscription_purchases
subscription_events
```

---

## 13. Acknowledge — strategia

### Decyzja: Acknowledge po stronie backendu

Flow:

```text
Edge Function:
    ↓
Google Play API:
    subscriptionsv2.get
    ↓
sprawdź acknowledgement state
    ↓
jeśli wymagane:
    acknowledge purchase
    ↓
zapisz zakup
    ↓
zaktualizuj users
```

Backend powinien być głównym miejscem wykonywania acknowledge.

Jeżeli acknowledge zakończy się błędem:

```text
- nie należy udawać pełnego sukcesu
- zakup może zostać zapisany jako zweryfikowany
- należy zachować możliwość retry acknowledge
- należy monitorować błąd
```

Aplikacja może mieć fallback do BillingClient acknowledge, jeśli jest to wymagane przez konkretny flow i wersję Billing Library, ale nie należy traktować tego jako podstawowej ścieżki.

---

## 14. RTDN — Real-Time Developer Notifications

RTDN jest częścią głównego systemu lifecycle.

### Architektura

```text
Google Play
    ↓
Pub/Sub Topic
foodbook-play-notifications
    ↓
Push Subscription
foodbook-rtdn-push
    ↓
HTTP POST
    ↓
handle-rtdn
    ↓
Google Play Developer API
    ↓
subscription_purchases
subscription_events
users
```

### Obsługiwane scenariusze

```text
SUBSCRIPTION_RECOVERED
SUBSCRIPTION_RENEWED
SUBSCRIPTION_CANCELED
SUBSCRIPTION_PURCHASED
SUBSCRIPTION_ON_HOLD
SUBSCRIPTION_IN_GRACE_PERIOD
SUBSCRIPTION_RESTARTED
SUBSCRIPTION_PRICE_CHANGE_CONFIRMED
SUBSCRIPTION_DEFERRED
SUBSCRIPTION_PAUSED
SUBSCRIPTION_PAUSE_SCHEDULE_CHANGED
SUBSCRIPTION_EXPIRED
SUBSCRIPTION_REVOKED
```

Dokładne mapowanie musi być zgodne z aktualną dokumentacją Google Play RTDN.

### Ogólna zasada

Każdy RTDN:

```text
1. Jest eventem
2. Powinien być zapisany idempotentnie
3. Powinien doprowadzić do odczytania aktualnego stanu Google Play
4. Powinien zaktualizować lokalny model
```

---

## 15. Diagram sekwencji — pełny flow zakupu

```text
┌──────┐    ┌──────────────────────┐    ┌───────────────┐    ┌──────────────┐    ┌──────────┐
│ User │    │  FoodbookApp         │    │ Google Play   │    │ Edge Function│    │ Supabase │
│      │    │  PaymentProvider     │    │ BillingClient │    │ verify       │    │          │
└──┬───┘    └──────────┬───────────┘    └───────┬───────┘    └──────┬───────┘    └────┬─────┘
   │                   │                        │                   │                 │
   │ "Kup Monthly"     │                        │                   │                 │
   │──────────────────>│                        │                   │                 │
   │                   │ QueryProductDetails    │                   │                 │
   │                   │───────────────────────>│                   │                 │
   │                   │ ProductDetails         │                   │                 │
   │                   │<───────────────────────│                   │                 │
   │                   │ LaunchBillingFlow      │                   │                 │
   │                   │───────────────────────>│                   │                 │
   │<───────────────────────────────────────────│                   │                 │
   │ Potwierdź zakup   │                        │                   │                 │
   │──────────────────────────────────────────->│                   │                 │
   │                   │ OnPurchasesUpdated     │                   │                 │
   │                   │<───────────────────────│                   │                 │
   │                   │ Purchase + Token       │                   │                 │
   │                   │                        │                   │                 │
   │                   │ Operation → Pending    │                   │                 │
   │                   │                        │                   │                 │
   │                   │ POST /verify-purchase  │                   │                 │
   │                   │───────────────────────────────────────────>│                 │
   │                   │                        │                   │ Google API      │
   │                   │                        │                   │ verify token    │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │                 │
   │                   │                        │                   │ acknowledge     │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │                 │
   │                   │                        │                   │ UPDATE purchase │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │                 │
   │                   │                        │                   │ UPDATE users    │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │                 │
   │                   │   { isValid: true }    │                   │                 │
   │                   │<───────────────────────────────────────────│                 │
   │                   │                        │                   │                 │
   │                   │ Operation → Completed  │                   │                 │
   │                   │ RefreshAccessAsync()   │                   │                 │
   │ "Premium aktywne" │                        │                   │                 │
   │<──────────────────│                        │                   │                 │
```

---

## 16. Diagram lifecycle — RTDN

```text
Google Play
    │
    │ Subscription renewed / canceled / expired
    ↓
Pub/Sub Topic
    │
    ↓
Push Subscription
    │
    │ HTTP POST
    ↓
handle-rtdn
    │
    ├── Decode notification
    │
    ├── Extract purchaseToken
    │
    ├── Check idempotency
    │
    ├── Call Google Play API
    │
    ├── Update subscription_purchases
    │
    ├── Insert subscription_events
    │
    └── Update users
```

---

## 17. Obsługa błędów i edge cases

### 17.1 Scenariusze błędów

| Scenariusz                      | Zachowanie aplikacji                       |
| ------------------------------- | ------------------------------------------ |
| Google Play niedostępny         | BillingClient error → alert użytkownika    |
| Zakup OK, Edge Function timeout | Pending + zachowanie PurchaseToken + retry |
| Token invalid                   | Failed + brak premium                      |
| Google API error                | Pending / retry                            |
| Brak internetu po zakupie       | Pending                                    |
| User anulował purchase sheet    | Brak operacji                              |
| Acknowledge failure             | Retry backendowy                           |
| Reinstall                       | QueryPurchases + verify                    |
| Subscription expired            | RTDN / sync → `is_premium = false`         |
| Subscription canceled           | Premium pozostaje do `expiryTime`          |
| Subscription renewed            | Nowy `expiryTime` → update                 |
| Subscription revoked            | Natychmiastowe odebranie dostępu           |
| Duplicate RTDN                  | Idempotentne pominięcie                    |
| Duplicate purchase verification | Idempotentny upsert                        |

---

### 17.2 Retry logic

```text
MaxRetryCount = 5
```

Przy błędzie tymczasowym:

```text
RetryCount++
Status = Pending
LastError = ...
```

Po przekroczeniu limitu:

```text
Status = Failed
```

Należy jednak rozróżnić:

```text
Błąd tymczasowy:
    timeout
    network
    Google API unavailable

Błąd permanentny:
    invalid token
    wrong package name
    invalid product ID
```

Błędy permanentne nie powinny być bez końca retryowane.

---

## 18. Idempotency

Weryfikacja zakupu musi być idempotentna.

Ten sam request:

```text
purchaseToken = X
```

może zostać wysłany więcej niż raz.

Backend powinien:

```text
1. Rozpoznać istniejący purchase token
2. Ponownie pobrać aktualny stan z Google Play
3. Zaktualizować istniejący rekord
4. Nie tworzyć duplikatów
```

RTDN również może zostać dostarczony ponownie.

Dlatego:

```text
subscription_events
```

powinna mieć mechanizm zapobiegania duplikatom.

---

## 19. Testowanie

### 19.1 Checklist testowy

| #  | Test                                     | Oczekiwany wynik                             |
| -- | ---------------------------------------- | -------------------------------------------- |
| 1  | Zakup monthly                            | `users.is_premium = true`                    |
| 2  | Zakup yearly                             | `users.is_premium = true`                    |
| 3  | PurchaseToken weryfikowany przez backend | `subscription_purchases` updated             |
| 4  | Acknowledge                              | Google Play pokazuje zakup jako acknowledged |
| 5  | Zakup → brak internetu                   | Operation Pending                            |
| 6  | Retry pending                            | Operation Completed                          |
| 7  | Restore po reinstalacji                  | Premium odzyskane                            |
| 8  | Monthly → Yearly                         | Google Play zmienia subskrypcję              |
| 9  | Yearly → Monthly                         | Google Play zmienia subskrypcję              |
| 10 | Cancel                                   | Auto-renew wyłączony, premium do expiry      |
| 11 | Renewal                                  | `premium_to` przedłużone                     |
| 12 | Expiry                                   | `is_premium = false`                         |
| 13 | RTDN                                     | Event odebrany przez Pub/Sub                 |
| 14 | Duplicate RTDN                           | Brak duplikatu                               |
| 15 | Duplicate verification                   | Brak duplikatu                               |
| 16 | Invalid token                            | Brak premium                                 |
| 17 | Wrong package name                       | Request odrzucony                            |
| 18 | Wrong product ID                         | Request odrzucony                            |
| 19 | Mock na Windows/iOS                      | Mock działa jak wcześniej                    |

---

## 20. Kolejność wdrażania

### Faza 1 — Backend i Google Play

```text
1. Google Play Console:
   ├── utworzenie subscription monthly
   ├── utworzenie subscription yearly
   ├── base plans
   └── license tester

2. Google Cloud:
   ├── Android Publisher API
   ├── Service Account
   └── Service Account access in Play Console

3. Supabase:
   ├── secrets
   ├── verify-purchase
   └── handle-rtdn

4. Pub/Sub:
   └── push subscription do handle-rtdn
```

### Faza 2 — Weryfikacja backendu

Przed implementacją aplikacji należy przetestować:

```text
verify-purchase
```

i:

```text
handle-rtdn
```

z rzeczywistym lub testowym zakupem.

Sprawdzić:

```text
- Google API authentication
- purchase verification
- acknowledge
- subscription_purchases
- subscription_events
- users
```

### Faza 3 — Aplikacja Android

```text
1. BillingConstants.cs
2. Billing Library
3. BillingClient lifecycle
4. ProductDetails
5. Purchase flow
6. PurchaseToken
7. verify-purchase
8. Pending operations
9. Retry
10. Restore
11. Change plan
12. Cancel
```

### Faza 4 — DI i UI

```text
1. Podmiana Mock → PaymentProvider
2. Test ProfilePage
3. Test FeatureAccessService
4. Test Pending UI
5. Test błędów
```

### Faza 5 — Lifecycle i stabilizacja

```text
1. RTDN
2. Renewal
3. Cancel
4. Expire
5. Revoke
6. Grace period
7. On hold
8. Idempotency
9. Retry
10. Monitoring
```

---

## 21. Pliki do modyfikacji — podsumowanie

| Plik                                                                    | Akcja                             | Opis                                |
| ----------------------------------------------------------------------- | --------------------------------- | ----------------------------------- |
| `Constants/BillingConstants.cs`                                         | **NOWY**                          | Package name i mapowanie Product ID |
| `Models/SubscriptionOperationEntry.cs`                                  | **MODYFIKACJA**                   | Dane potrzebne do pending/retry     |
| `FoodbookApp.App.csproj`                                                | **MODYFIKACJA**                   | Google Play Billing binding         |
| `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` | **PRZEPISANIE**                   | Integracja BillingClient            |
| `MauiProgram.cs`                                                        | **MODYFIKACJA**                   | DI per platform                     |
| `Views/ProfilePage.xaml`                                                | **OPCJONALNA MODYFIKACJA**        | Teksty i statusy                    |
| `Views/ProfilePage.xaml.cs`                                             | **MINIMALNA MODYFIKACJA**         | Obsługa statusów                    |
| `supabase/functions/verify-purchase/`                                   | **NOWY / AKTUALIZACJA**           | Weryfikacja + acknowledge           |
| `supabase/functions/handle-rtdn/`                                       | **NOWY / AKTUALIZACJA**           | RTDN lifecycle                      |
| `supabase/functions/sync-play-subscription/`                            | **OPCJONALNY**                    | Batch sync                          |
| `subscription_purchases`                                                | **WERYFIKACJA SCHEMATU**          | Dane zakupów                        |
| `subscription_events`                                                   | **WERYFIKACJA SCHEMATU**          | Historia eventów                    |
| `users`                                                                 | **BEZ KONIECZNOŚCI ROZSZERZANIA** | Aktualny stan premium               |

---

## 22. Decyzje architektoniczne — podsumowanie

| Decyzja                                | Wybór                                               | Uzasadnienie                                          |
| -------------------------------------- | --------------------------------------------------- | ----------------------------------------------------- |
| Backend weryfikacji                    | Supabase Edge Functions                             | Istniejący backend                                    |
| Google API authentication              | Service Account                                     | Serwerowa weryfikacja                                 |
| Source of truth dla aktualnego dostępu | `users`                                             | FeatureAccessService już korzysta z tego modelu       |
| Source of truth dla zakupów            | Google Play + `subscription_purchases`              | Dane zakupowe nie powinny być w `users`               |
| Source of truth dla lifecycle          | Google Play Developer API                           | RTDN informuje o zmianie, API dostarcza aktualny stan |
| Historia eventów                       | `subscription_events`                               | Audyt i idempotency                                   |
| Acknowledge                            | Backend                                             | Centralna i bezpieczna obsługa                        |
| Typ subskrypcji                        | Auto-renewing subscriptions                         | Standardowy model SaaS                                |
| Cancel flow                            | Google Play Manage Subscriptions                    | Użytkownik zarządza auto-renewal w Google Play        |
| Aktualizacja po cancel                 | RTDN + API verification                             | Premium pozostaje do expiry                           |
| Restore                                | BillingClient QueryPurchases + backend verification | Obsługa reinstalacji i nowych urządzeń                |
| Zmiana planu                           | Google Play Subscription Update Flow                | Nie tworzyć niezależnej drugiej subskrypcji           |
| RTDN                                   | Włączone od początku                                | Automatyczna synchronizacja lifecycle                 |
| Inne platformy                         | Mock / stub                                         | Android-only w Fazie 1                                |
| `users`                                | Bez Google-specific columns                         | Dane zakupowe są w dedykowanych tabelach              |
| Idempotency                            | Wymagana                                            | Retry i duplicate RTDN są normalne                    |
| Package name                           | `com.ppapps.foodbookapp`                            | Rzeczywisty package aplikacji                         |

---

## 23. Instrukcje dla agenta implementującego

Przed napisaniem kodu agent musi:

```text
1. Przeanalizować istniejący ISubscriptionManagementService.
2. Przeanalizować istniejący MockSubscriptionManagementService.
3. Przeanalizować PaymentProviderSubscriptionManagementService.
4. Przeanalizować SubscriptionOperationEntry.
5. Przeanalizować ProfilePage.xaml.cs.
6. Przeanalizować MauiProgram.cs.
7. Sprawdzić rzeczywisty target framework projektu.
8. Sprawdzić aktualną wersję .NET MAUI.
9. Sprawdzić istniejącą wersję Android Billing bindingu.
10. Sprawdzić rzeczywisty schemat subscription_purchases.
11. Sprawdzić rzeczywisty schemat subscription_events.
12. Nie zmieniać users bez uzasadnienia.
13. Nie dodawać Google-specific columns do users, jeśli dane mogą być przechowywane w istniejących tabelach.
14. Nie używać SERVICE_ROLE_KEY w aplikacji mobilnej.
15. Nie hardkodować sekretów w aplikacji.
16. Nie hardkodować cen.
17. Nie zakładać API Billing Library bez sprawdzenia wersji bindingu.
18. Nie implementować zmiany planu jako niezależnego drugiego zakupu.
19. Nie odbierać premium natychmiast po anulowaniu auto-renew.
20. Implementować idempotency dla verification i RTDN.
```

Agent powinien implementować rozwiązanie etapami:

```text
1. Najpierw analiza istniejącego kodu.
2. Następnie lista konkretnych zmian.
3. Następnie implementacja backendu.
4. Następnie implementacja BillingClient.
5. Następnie integracja z istniejącym interface.
6. Następnie testy i poprawki kompilacji.
```

Nie należy tworzyć całkowicie nowej architektury obok istniejącej.

Celem jest:

```text
Istniejący UI
        ↓
Istniejący interface
        ↓
Nowa implementacja Google Play Billing
        ↓
Supabase Edge Functions
        ↓
Google Play Developer API
```

---

> **Ten plan jest gotowy do implementacji, ale agent musi najpierw przeanalizować rzeczywisty kod projektu i rzeczywisty schemat tabel Supabase. Nie wolno implementować go na podstawie pseudokodu bez dopasowania do istniejących klas, metod, wersji NuGet i nazw kolumn.**
