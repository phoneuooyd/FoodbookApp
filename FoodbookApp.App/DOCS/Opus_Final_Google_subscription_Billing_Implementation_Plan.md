# Opus Final — Google Play Subscription Billing Implementation Plan

## FoodbookApp · .NET MAUI · Supabase Backend

---

## 1. Streszczenie wykonawcze

Niniejszy plan opisuje pełną implementację subskrypcji Google Play Billing w aplikacji FoodbookApp (.NET MAUI) z weryfikacją po stronie serwera (Supabase Edge Functions). Celem jest zastąpienie obecnego `MockSubscriptionManagementService` prawdziwą integracją z Google Play, zachowując istniejącą architekturę (`ISubscriptionManagementService`, `FeatureAccessService`, `SubscriptionOperationEntry`, `ProfilePage.xaml.cs`).

### Source of Truth

| Aspekt | Obecny mock | Google Play Billing |
|--------|------------|-------------------|
| Kto decyduje o premium? | Aplikacja sama | Google Play → backend weryfikuje → Supabase |
| Skąd pochodzi PurchaseToken? | Brak | Google Play BillingClient |
| Kto aktualizuje `users.IsPremium`? | `MockSubscriptionManagementService` bezpośrednio | Supabase Edge Function po weryfikacji z Google API |
| Acknowledge | Brak | Wymagany — bez niego Google anuluje zakup po 3 dniach |
| Lifecycle (renew/cancel/expire) | Brak | RTDN (Real-Time Developer Notifications) → webhook |

---

## 2. Obecna architektura (analiza stanu)

### 2.1 Flow subskrypcji (mock)

```
User klika "Kup Premium" w ProfilePage
    ↓
ProfilePage.xaml.cs → ExecuteSubscriptionActionAsync()
    ↓
ISubscriptionManagementService.ChangePlanAsync(targetPlan)
    ↓
MockSubscriptionManagementService:
  1. Tworzy SubscriptionOperationEntry (Pending)
  2. UpsertSupabaseUserPlanAsync() → bezpośrednio zapisuje do tabeli `users`:
     - IsPremium = true/false
     - PremiumFrom = now
     - PremiumTo = now + 1month / 1year
     - UserType = 0 (free) / 1 (premium)
  3. Zapisuje snapshot do SecureStorage
  4. Oznacza operację jako Completed
    ↓
ProfilePage → HandleSubscriptionActionResultAsync()
  - SaveSubscriptionPlanChoice() do Preferences
  - FeatureAccessService.RefreshAccessAsync() → odczytuje `users` z Supabase
  - RefreshSubscriptionSectionAsync() → aktualizuje UI
```

### 2.2 Kluczowe pliki

| Plik | Rola |
|------|------|
| `Interfaces/ISubscriptionManagementService.cs` | Kontrakt: `ChangePlanAsync`, `CancelSubscriptionAsync`, `GetPendingOperationAsync`, `ResumePendingOperationAsync` |
| `Services/Subscription/MockSubscriptionManagementService.cs` | Obecna implementacja — bezpośredni upsert do Supabase, bez weryfikacji płatności |
| `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` | **Stub** — `NotSupportedException` — tu trafi implementacja Google Play |
| `Services/Subscription/SupabaseEdgeSubscriptionManagementService.cs` | **Stub** — przeznaczony na wrapper do Edge Functions |
| `Services/FeatureAccessService.cs` | Odczytuje `users` z Supabase → buduje `FeatureAccessSnapshot` → decyduje o uprawnieniach |
| `Views/ProfilePage.xaml.cs` | UI subskrypcji: buttony, pending operations, plan details |
| `Views/ProfilePage.xaml` | Layout XAML z sekcjami: PlanDetailsView, PendingSubscriptionCard, przyciski zmiany planu |
| `Models/SubscriptionOperationEntry.cs` | Entity: `Id`, `AccountId`, `TargetPlan`, `Status`, `IdempotencyKey`, `RetryCount`, `LastError`, itd. |
| `Models/SubscriptionPlan.cs` | Enum: `Free`, `PremiumMonthly`, `PremiumYearly` |
| `Models/SubscriptionActionResult.cs` | Wynik akcji: `Success`, `CurrentPlan`, `ActionState`, `OperationId`, `UiMessage` |
| `Models/SubscriptionPendingOperation.cs` | DTO pendingowej operacji dla UI |
| `Models/SubscriptionActionState.cs` | Enum: `Completed`, `Pending`, `Failed` |
| `MauiProgram.cs` | DI — obecnie rejestruje `MockSubscriptionManagementService` jako `ISubscriptionManagementService` |

### 2.3 Tabela `users` w Supabase

```sql
-- Obecne kolumny związane z subskrypcją:
id          UUID PRIMARY KEY  -- = auth.users.id
email       TEXT
is_premium  BOOLEAN DEFAULT false
premium_from TIMESTAMPTZ
premium_to  TIMESTAMPTZ
user_type   INTEGER DEFAULT 0  -- 0=free, 1=premium
```

---

## 3. Docelowa architektura (Google Play Billing)

### 3.1 Docelowy flow zakupu

```
┌───────────────────────────────────────────────────────────┐
│  APLIKACJA (.NET MAUI / Android)                          │
│                                                           │
│  1. User klika "Kup Premium Monthly/Yearly"               │
│     ↓                                                     │
│  2. GooglePlaySubscriptionManagementService                │
│     .ChangePlanAsync(targetPlan)                          │
│     ↓                                                     │
│  3. BillingClient.LaunchBillingFlow()                     │
│     → Google Play purchase sheet                          │
│     ↓                                                     │
│  4. OnPurchasesUpdated callback                           │
│     → otrzymuje Purchase z PurchaseToken                  │
│     ↓                                                     │
│  5. Wysyłka do Supabase Edge Function:                    │
│     POST /verify-play-purchase                            │
│     Body: { supabaseUserId, productId, purchaseToken,     │
│             packageName }                                 │
└───────────────────────────────────────────────────────────┘
                         ↓
┌───────────────────────────────────────────────────────────┐
│  SUPABASE EDGE FUNCTION: verify-play-purchase             │
│                                                           │
│  1. Walidacja inputu                                      │
│  2. Google Play Developer API:                            │
│     purchases.subscriptionsv2.get(purchaseToken)          │
│  3. Weryfikacja:                                          │
│     - subscriptionState === SUBSCRIPTION_STATE_ACTIVE     │
│     - expiryTime > now                                    │
│  4. Acknowledge jeśli acknowledgementState !== ACKNOWLEDGED│
│  5. Mapowanie productId → SubscriptionPlan                │
│  6. UPDATE users SET:                                     │
│     is_premium = true,                                    │
│     premium_from = startTime,                             │
│     premium_to = expiryTime,                              │
│     user_type = 1,                                        │
│     google_purchase_token = token,                        │
│     google_product_id = productId                         │
│  7. Response: { isValid, plan, premiumFrom, premiumTo }   │
└───────────────────────────────────────────────────────────┘
                         ↓
┌───────────────────────────────────────────────────────────┐
│  APLIKACJA                                                │
│                                                           │
│  6. Edge Function → sukces                                │
│     → Oznacz SubscriptionOperationEntry jako Completed    │
│     → FeatureAccessService.RefreshAccessAsync()           │
│     → UI: "Premium aktywne"                               │
│                                                           │
│  7. Edge Function → błąd / timeout                        │
│     → Oznacz jako Pending + zapisz PurchaseToken          │
│     → ResumePendingOperationAsync() po reconnect          │
└───────────────────────────────────────────────────────────┘
```

### 3.2 Flow anulowania

```
User klika "Anuluj subskrypcję"
    ↓
CancelSubscriptionAsync()
    ↓
Android: Intent do Google Play Manage Subscriptions
    (deep link: https://play.google.com/store/account/subscriptions
     ?sku=PRODUCT_ID&package=PACKAGE_NAME)
    ↓
Google Play pozwala anulować
    ↓
(Opcja A — RTDN webhook) Google wysyła event → Edge Function → users update
(Opcja B — polling) App odpytuje sync-play-subscription na start → refresh
```

### 3.3 Flow przywracania zakupów (restore)

```
User reinstaluje aplikację / nowe urządzenie
    ↓
OnAppearing → TryAutoResumePendingOperationAsync()
    ↓
BillingClient.QueryPurchasesAsync(SUBS)
    ↓
Dla każdego aktywnego Purchase:
    POST /sync-play-subscription
    Body: { supabaseUserId, packageName, purchases[] }
    ↓
Edge Function weryfikuje wszystkie tokeny z Google
    → wybiera najlepszą aktywną subskrypcję
    → update users
    ↓
RefreshAccessAsync() → UI update
```

---

## 4. Szczegółowy plan implementacji

### Krok 1: Konfiguracja Google Play Console

**Gdzie**: Google Play Console → twoja aplikacja

**Czynności**:
1. Przejdź do **Monetize → Products → Subscriptions**
2. Utwórz 2 subskrypcje:

| Product ID | Nazwa | Okres | Cena sugerowana |
|-----------|-------|-------|-----------------|
| `foodbook_premium_monthly` | Foodbook Premium Miesięczny | 1 miesiąc | ~19.99 PLN |
| `foodbook_premium_yearly` | Foodbook Premium Roczny | 12 miesięcy | ~149.99 PLN |

3. Dla każdej subskrypcji skonfiguruj:
   - Base plan (auto-renewing)
   - Offer (opcjonalnie: free trial, introductory price)
   - Grace period: 7 dni (zalecane)
   - Resubscribe: włącz

4. Dodaj **License testers**: Settings → License testing → dodaj email testowy
5. Opublikuj aplikację na **Internal testing track** (wymagane do testowania billing)

### Krok 2: Konfiguracja Google Cloud + Service Account

**Cel**: Supabase Edge Functions muszą mieć dostęp do Google Play Developer API

1. Google Cloud Console → Projekt powiązany z Google Play Console
2. Włącz **Google Play Android Developer API**
3. Utwórz **Service Account**:
   - IAM & Admin → Service Accounts → Create
   - Nadaj rolę: `roles/androidpublisher.appEditor` (lub custom)
   - Utwórz klucz JSON → pobierz
4. Google Play Console → Settings → API Access:
   - Połącz z projektem GCP
   - Nadaj Service Account uprawnienia: **View financial data**, **Manage orders and subscriptions**

**Wynik**: Plik JSON klucza service account → trafi jako secret do Supabase

### Krok 3: Mapowanie Product ID → SubscriptionPlan

**Plik**: `Constants/BillingConstants.cs` (NOWY)

```csharp
namespace FoodbookApp.Constants;

public static class BillingConstants
{
    /// <summary>
    /// Package name aplikacji w Google Play.
    /// </summary>
    public const string PackageName = "com.takis.foodbook"; // <- Twój package name

    /// <summary>
    /// Google Play Product ID → SubscriptionPlan mapping.
    /// </summary>
    public static readonly Dictionary<string, SubscriptionPlan> ProductPlanMap = new()
    {
        ["foodbook_premium_monthly"] = SubscriptionPlan.PremiumMonthly,
        ["foodbook_premium_yearly"] = SubscriptionPlan.PremiumYearly
    };

    /// <summary>
    /// SubscriptionPlan → Google Play Product ID mapping.
    /// </summary>
    public static readonly Dictionary<SubscriptionPlan, string> PlanProductMap = new()
    {
        [SubscriptionPlan.PremiumMonthly] = "foodbook_premium_monthly",
        [SubscriptionPlan.PremiumYearly] = "foodbook_premium_yearly"
    };

    /// <summary>
    /// Supabase Edge Function endpoints.
    /// </summary>
    public const string VerifyPurchaseEndpoint = "verify-play-purchase";
    public const string SyncSubscriptionEndpoint = "sync-play-subscription";
}
```

### Krok 4: Rozszerzenie modelu SubscriptionOperationEntry

**Plik**: `Models/SubscriptionOperationEntry.cs` (MODYFIKACJA)

Dodaj pola przechowujące dane Google Play (na wypadek retry):

```csharp
// === Nowe pola provider ===

/// <summary>
/// Nazwa providera płatności (np. "GooglePlay", "AppStore").
/// </summary>
public string? PaymentProvider { get; set; }

/// <summary>
/// Google Play Product ID (np. "foodbook_premium_monthly").
/// </summary>
public string? ProviderProductId { get; set; }

/// <summary>
/// Google Play Purchase Token — dowód zakupu, potrzebny do weryfikacji.
/// </summary>
public string? PurchaseToken { get; set; }

/// <summary>
/// Czas zakupu (UTC) zgłoszony przez Google.
/// </summary>
public DateTime? PurchaseTimeUtc { get; set; }

/// <summary>
/// Czas wygaśnięcia subskrypcji (UTC) zgłoszony przez Google.
/// </summary>
public DateTime? ExpiryTimeUtc { get; set; }

/// <summary>
/// Czy zakup został acknowledged po stronie Google.
/// </summary>
public bool IsAcknowledged { get; set; }

/// <summary>
/// Ostatni status z Google (np. "SUBSCRIPTION_STATE_ACTIVE").
/// </summary>
public string? LastProviderStatus { get; set; }
```

**EF Migration**: Dodaj migrację po zmianie modelu.

### Krok 5: NuGet — Google Play Billing Library

**Plik**: `FoodbookApp.App.csproj` (MODYFIKACJA)

```xml
<!-- Google Play Billing Client v7 for Android -->
<ItemGroup Condition="$(TargetFramework.Contains('android'))">
  <PackageReference Include="Xamarin.Android.Google.BillingClient"
                    Version="7.1.1" />
</ItemGroup>
```

> **Uwaga**: Wersja `7.1.1` (lub najnowsza stabilna). Upewnij się, że `net10.0-android` target jest skonfigurowany.

### Krok 6: Implementacja GooglePlaySubscriptionManagementService

**Plik**: `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` (PRZEPISANIE)

To jest **główny serwis** — implementacja `ISubscriptionManagementService` dla Androida.

```csharp
// Pseudokod struktury — pełna implementacja poniżej

public sealed class PaymentProviderSubscriptionManagementService
    : ISubscriptionManagementService
{
    // Injected dependencies:
    //   AppDbContext, IAccountService, SupabaseRestClient,
    //   ISecureStorageAdapter, IClock, ILogger

    // === Internal state ===
    private BillingClient? _billingClient;
    private TaskCompletionSource<Purchase?>? _purchaseTcs;

    // ---- ISubscriptionManagementService ----

    public async Task<SubscriptionActionResult> ChangePlanAsync(
        SubscriptionPlan targetPlan, CancellationToken ct)
    {
        // 1. Jeśli targetPlan == Free → CancelSubscriptionAsync()
        // 2. Mapuj targetPlan → productId via BillingConstants
        // 3. EnsureBillingClientConnected()
        // 4. QueryProductDetailsAsync(productId, SUBS)
        // 5. LaunchBillingFlow() → czekaj na OnPurchasesUpdated
        // 6. Po Purchase:
        //    a) Utwórz SubscriptionOperationEntry (Pending)
        //       z PurchaseToken, ProviderProductId
        //    b) POST /verify-play-purchase → Supabase Edge Function
        //    c) Sukces → Completed + RefreshSnapshot
        //    d) Błąd → Pending (retry later)
    }

    public async Task<SubscriptionActionResult> CancelSubscriptionAsync(
        CancellationToken ct)
    {
        // 1. Otwórz deep link do Google Play Manage Subscriptions
        //    URL: "https://play.google.com/store/account/subscriptions
        //          ?sku=PRODUCT_ID&package=PACKAGE_NAME"
        // 2. AbandonOpenOperations()
        // 3. Sync z Edge Function → odśwież status
    }

    public async Task<SubscriptionPendingOperation?> GetPendingOperationAsync(
        CancellationToken ct)
    {
        // Identyczny z MockSubscriptionManagementService
    }

    public async Task<SubscriptionActionResult> ResumePendingOperationAsync(
        CancellationToken ct)
    {
        // 1. Pobierz ostatnią otwartą operację z DB
        // 2. Jeśli ma PurchaseToken → POST /verify-play-purchase
        // 3. Jeśli brak tokenu → QueryPurchasesAsync() + sync
        // 4. Sukces → Completed, Błąd → re-Pending
    }
}
```

#### Kluczowe metody wewnętrzne:

```csharp
// --- BillingClient lifecycle ---

private async Task<bool> EnsureBillingClientConnectedAsync()
{
    // Inicjalizuj BillingClient jeśli null
    // .SetListener(purchasesUpdatedListener)
    // .EnablePendingPurchases(OneTimeProducts + PrepaidPlans)
    // StartConnection → czekaj na OnBillingSetupFinished
}

// --- Google Play purchase flow ---

private async Task<Purchase?> LaunchPurchaseFlowAsync(
    string productId, CancellationToken ct)
{
    // 1. QueryProductDetailsAsync(productId, BillingClient.ProductType.Subs)
    // 2. BillingFlowParams z offerToken
    // 3. LaunchBillingFlow(activity, params)
    // 4. Czekaj na _purchaseTcs.Task (ustawiony w OnPurchasesUpdated)
}

// --- PurchasesUpdatedListener callback ---

internal void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
{
    // Jeśli OK + purchases != null:
    //   _purchaseTcs?.TrySetResult(purchases.FirstOrDefault())
    // Jeśli UserCancelled:
    //   _purchaseTcs?.TrySetResult(null)
    // Inaczej:
    //   _purchaseTcs?.TrySetException(...)
}

// --- Backend verification ---

private async Task<VerifyPurchaseResponse> VerifyPurchaseWithBackendAsync(
    string supabaseUserId,
    string productId,
    string purchaseToken,
    CancellationToken ct)
{
    // POST do Supabase Edge Function /verify-play-purchase
    // Body: JSON { supabaseUserId, productId, purchaseToken, packageName }
    // Headers: Authorization: Bearer <supabase_anon_key>
    // Response: { isValid, plan, premiumFrom, premiumTo, acknowledged, message }
}

// --- Restore / sync ---

private async Task SyncAllPurchasesAsync(
    string supabaseUserId, CancellationToken ct)
{
    // 1. QueryPurchasesAsync(SUBS)
    // 2. Collect all active Purchase tokens
    // 3. POST /sync-play-subscription
    //    Body: { supabaseUserId, packageName, purchases[] }
    // 4. Odśwież snapshot
}
```

### Krok 7: Aktualizacja DI (MauiProgram.cs)

**Plik**: `MauiProgram.cs` (MODYFIKACJA)

```csharp
// Rejestracja ISubscriptionManagementService per platform:

#if ANDROID
    builder.Services.AddScoped<ISubscriptionManagementService,
        PaymentProviderSubscriptionManagementService>();
#else
    // iOS/Windows/inne — stub lub MockSubscriptionManagementService
    builder.Services.AddScoped<ISubscriptionManagementService,
        MockSubscriptionManagementService>();
#endif
```

### Krok 8: Aktualizacja UI (ProfilePage.xaml / ProfilePage.xaml.cs)

**Minimalne zmiany** — dzięki `ISubscriptionManagementService` abstraction.

#### ProfilePage.xaml.cs — zmiany:

```csharp
// 1. OnSwitchToMonthlyClicked / OnSwitchToYearlyClicked
//    → Bez zmian! Dalej wołają ChangePlanAsync(targetPlan)

// 2. OnCancelSubscriptionClicked
//    → Bez zmian! CancelSubscriptionAsync() w Google Play
//      implementation otwiera Manage Subscriptions deep link

// 3. BuildCurrentPlanUi()
//    → Opcjonalnie: dodaj ceny z Google Play ProductDetails
//    → Dzisiaj hardkodowane "TBD / month" i "TBD / year"
//    → Po integracji: ceny pobrane z Google Play w lokalnej walucie
```

#### ProfilePage.xaml — opcjonalne zmiany:

```xml
<!-- Opcjonalnie: zmień tekst CancelSubscriptionButton na Androidzie -->
<!-- "Zarządzaj subskrypcją w Google Play" zamiast "Anuluj" -->

<!-- Opcjonalnie: dodaj wskaźnik weryfikacji -->
<!-- Np. "Weryfikacja zakupu..." podczas POST do Edge Function -->
```

### Krok 9: Migracja EF Core

```bash
# Po dodaniu nowych pól do SubscriptionOperationEntry:
dotnet ef migrations add AddGooglePlayBillingFields \
    --project FoodbookApp.App \
    --startup-project FoodbookApp.App
```

---

## 5. Supabase Edge Functions (szczegóły)

> Pełna specyfikacja Edge Functions znajduje się w osobnym pliku:
> **`supabase_edge_functions.md`**

### 5.1 Lista Edge Functions

| Function | Cel | Wołana przez |
|----------|-----|-------------|
| `verify-play-purchase` | Weryfikacja pojedynczego zakupu + acknowledge + update `users` | Aplikacja po zakupie / resume |
| `sync-play-subscription` | Batch sync wszystkich aktywnych subskrypcji | Aplikacja na starcie / restore |

### 5.2 Sekrety Supabase

```bash
supabase secrets set GOOGLE_SERVICE_ACCOUNT_JSON='<cały JSON klucza SA>'
supabase secrets set GOOGLE_PACKAGE_NAME='com.takis.foodbook'
supabase secrets set SUPABASE_SERVICE_ROLE_KEY='<service-role key>'
```

---

## 6. Produkty Google Play — konfiguracja

### 6.1 Subskrypcje

```yaml
Subscription 1:
  product_id: "foodbook_premium_monthly"
  name: "Foodbook Premium Miesięczny"
  description: "Pełny dostęp do AI, planów, szablonów"
  base_plan:
    billing_period: P1M  # 1 miesiąc
    renewal_type: AUTO_RENEWING
    price: 19.99 PLN  # lub inną kwotę
  grace_period: 7 days
  resubscribe: enabled

Subscription 2:
  product_id: "foodbook_premium_yearly"
  name: "Foodbook Premium Roczny"
  description: "Pełny dostęp — oszczędzasz ~25%"
  base_plan:
    billing_period: P1Y  # 12 miesięcy
    renewal_type: AUTO_RENEWING
    price: 149.99 PLN  # lub inną kwotę
  grace_period: 7 days
  resubscribe: enabled
```

### 6.2 Mapowanie do SubscriptionPlan

```
foodbook_premium_monthly  →  SubscriptionPlan.PremiumMonthly
foodbook_premium_yearly   →  SubscriptionPlan.PremiumYearly
(brak aktywnej subskrypcji) →  SubscriptionPlan.Free
```

---

## 7. Tabela `users` — rozszerzenie schematu

```sql
-- Nowe kolumny do dodania (migration):
ALTER TABLE users ADD COLUMN IF NOT EXISTS google_purchase_token TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS google_product_id TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS google_order_id TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS subscription_state TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS auto_renewing BOOLEAN DEFAULT false;
ALTER TABLE users ADD COLUMN IF NOT EXISTS subscription_updated_at TIMESTAMPTZ;
```

### Kolumny po migracji

| Kolumna | Typ | Opis |
|---------|-----|------|
| `is_premium` | BOOLEAN | Czy user ma aktywną subskrypcję |
| `premium_from` | TIMESTAMPTZ | Początek bieżącego okresu |
| `premium_to` | TIMESTAMPTZ | Koniec bieżącego okresu (expiry) |
| `user_type` | INTEGER | 0=free, 1=premium |
| `google_purchase_token` | TEXT | Ostatni PurchaseToken z Google |
| `google_product_id` | TEXT | `foodbook_premium_monthly` / `yearly` |
| `google_order_id` | TEXT | Identyfikator transakcji Google |
| `subscription_state` | TEXT | Stan subskrypcji z Google API |
| `auto_renewing` | BOOLEAN | Czy auto-renew aktywny |
| `subscription_updated_at` | TIMESTAMPTZ | Kiedy ostatnio Edge Function zaktualizowała |

---

## 8. Acknowledge — strategia

### Decyzja: Acknowledge w Edge Function (serwer-side)

**Dlaczego server-side**:
- Bezpieczniejszy — klient nie może sfałszować acknowledge
- Google zaleca server-side acknowledge dla subscriptions
- Edge Function już ma Service Account z uprawnieniami

**Flow**:
```
Edge Function:
  1. Verify purchase → isValid
  2. Check acknowledgementState
  3. Jeśli NOT_ACKNOWLEDGED:
     → purchases.subscriptions.acknowledge(token)
  4. Update users
  5. Response: { acknowledged: true }
```

**Fallback**: Jeśli Edge Function nie może acknowledge (np. problem z SA):
- Zwróć `{ isValid: true, acknowledged: false }`
- Aplikacja może użyć `BillingClient.AcknowledgePurchase()` jako fallback
- **Ważne**: Google daje 3 dni na acknowledge — po tym anuluje zakup

---

## 9. RTDN (Real-Time Developer Notifications) — Faza 2

> **To jest osobny etap** — można wdrożyć po uruchomieniu podstawowego billing.

### Cel
Automatyczne reagowanie na eventy lifecycle subskrypcji:
- `SUBSCRIPTION_RENEWED` → przedłuż `premium_to`
- `SUBSCRIPTION_CANCELED` → oznacz `auto_renewing = false`
- `SUBSCRIPTION_EXPIRED` → `is_premium = false`
- `SUBSCRIPTION_IN_GRACE_PERIOD` → zachowaj premium, wyślij notyfikację
- `SUBSCRIPTION_REVOKED` → natychmiast `is_premium = false`

### Architektura RTDN

```
Google Play → Cloud Pub/Sub → Push endpoint (Supabase Edge Function)
                                      ↓
                              handle-rtdn Edge Function
                                      ↓
                              Update tabeli `users`
```

### Edge Function: `handle-rtdn` (Faza 2)

```typescript
// POST /handle-rtdn
// Body: { message: { data: base64(DeveloperNotification) } }
// → Decode → sprawdź subscriptionNotification.notificationType
// → purchases.subscriptionsv2.get(token)
// → Update users
```

---

## 10. Diagram sekwencji — pełny flow zakupu

```
┌──────┐    ┌──────────────────────┐    ┌───────────────┐    ┌──────────────┐    ┌──────────┐
│ User │    │  FoodbookApp         │    │ Google Play   │    │ Edge Function│    │ Supabase │
│      │    │  (PaymentProvider    │    │ BillingClient │    │ verify-play  │    │  users   │
│      │    │   SubscriptionMgmt) │    │               │    │  -purchase   │    │          │
└──┬───┘    └──────────┬───────────┘    └───────┬───────┘    └──────┬───────┘    └────┬─────┘
   │                   │                        │                   │                 │
   │ "Kup Monthly"     │                        │                   │                 │
   │──────────────────>│                        │                   │                 │
   │                   │ QueryProductDetails    │                   │                 │
   │                   │───────────────────────>│                   │                 │
   │                   │   ProductDetails       │                   │                 │
   │                   │<───────────────────────│                   │                 │
   │                   │ LaunchBillingFlow      │                   │                 │
   │                   │───────────────────────>│                   │                 │
   │                   │                        │ Purchase Sheet    │                 │
   │<───────────────────────────────────────────│                   │                 │
   │ Potwierdź zakup   │                        │                   │                 │
   │──────────────────────────────────────────->│                   │                 │
   │                   │ OnPurchasesUpdated     │                   │                 │
   │                   │<───────────────────────│                   │                 │
   │                   │ (Purchase + Token)     │                   │                 │
   │                   │                        │                   │                 │
   │                   │ Zapisz operację (Pending)                  │                 │
   │                   │───────┐                │                   │                 │
   │                   │       │ DB             │                   │                 │
   │                   │<──────┘                │                   │                 │
   │                   │                        │                   │                 │
   │                   │ POST /verify-play-purchase                 │                 │
   │                   │───────────────────────────────────────────>│                 │
   │                   │                        │                   │ Google API      │
   │                   │                        │                   │ verify token    │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │ (via Google)    │
   │                   │                        │                   │                 │
   │                   │                        │                   │ acknowledge     │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │ (via Google)    │
   │                   │                        │                   │                 │
   │                   │                        │                   │ UPDATE users    │
   │                   │                        │                   │────────────────>│
   │                   │                        │                   │   is_premium    │
   │                   │                        │                   │   = true        │
   │                   │                        │                   │<────────────────│
   │                   │   { isValid: true }    │                   │                 │
   │                   │<───────────────────────────────────────────│                 │
   │                   │                        │                   │                 │
   │                   │ Operacja → Completed   │                   │                 │
   │                   │ RefreshAccessAsync()   │                   │                 │
   │ "Premium aktywne" │                        │                   │                 │
   │<──────────────────│                        │                   │                 │
```

---

## 11. Obsługa błędów i edge cases

### 11.1 Scenariusze błędów

| Scenariusz | Zachowanie aplikacji |
|-----------|---------------------|
| Google Play niedostępny | BillingClient init fail → alert użytkownika, Pending |
| Zakup OK, ale Edge Function timeout | Operacja → Pending + zachowaj PurchaseToken → retry |
| Edge Function: token invalid | Operacja → Failed, alert: "Weryfikacja nie powiodła się" |
| Edge Function: Google API error | Response 500 → retry later |
| Brak internetu po zakupie | Operacja → Pending, auto-resume przy OnAppearing |
| User anulował purchase sheet | `OnPurchasesUpdated` z `UserCancelled` → brak operacji |
| 3-day acknowledge deadline | Edge Function ack → jeśli fail, klient ack fallback |
| Reinstall / nowe urządzenie | QueryPurchasesAsync → sync-play-subscription |

### 11.2 Retry logic

```csharp
// MaxRetryCount = 5 (zachowane z MockSubscriptionManagementService)
// Retry: po każdym fail zwiększ RetryCount
// Po MaxRetryCount → Status = Failed
// User może ręcznie wznowić z PendingSubscriptionCard
```

---

## 12. Testowanie

### 12.1 Checklist testowy

| # | Test | Oczekiwany wynik |
|---|------|-----------------|
| 1 | Zakup monthly (license tester) | `users.is_premium = true`, `premium_to = +1month`, UI → Premium |
| 2 | Zakup yearly (license tester) | `users.is_premium = true`, `premium_to = +1year`, UI → Premium |
| 3 | Zakup → brak internetu po purchase | Operacja Pending, token zapisany, resume po reconnect |
| 4 | Resume pending operation | Edge Function weryfikuje, operacja → Completed |
| 5 | Cancel w Google Play | Deep link otwiera się, po powrocie sync → status cancel |
| 6 | Reinstall → restore | QueryPurchases → sync → users update → Premium |
| 7 | Wygasła subskrypcja | `is_premium = false` (via sync lub RTDN) |
| 8 | Podwójny zakup (idempotency) | Edge Function sprawdza existing token → nie duplikuje |
| 9 | Mock na Windows/iOS | `MockSubscriptionManagementService` działa jak dziś |

### 12.2 Komendy build

```bash
# Android (z billing):
dotnet build -f net10.0-android

# Windows (mock):
dotnet build -f net10.0-windows10.0.22000.0
```

---

## 13. Kolejność wdrażania (fazy)

### Faza 1 — MVP Billing (ten plan)
1. ✅ Google Play Console: subskrypcje + service account
2. ✅ Supabase secrets + Edge Functions deploy
3. ✅ `BillingConstants.cs` — product ID mapping
4. ✅ `SubscriptionOperationEntry` — nowe pola
5. ✅ NuGet: `Xamarin.Android.Google.BillingClient`
6. ✅ `PaymentProviderSubscriptionManagementService` — pełna implementacja
7. ✅ `MauiProgram.cs` — DI per platform
8. ✅ EF Core migration
9. ✅ Tabela `users` — nowe kolumny
10. ✅ Testy z license tester

### Faza 2 — Lifecycle (RTDN)
- Cloud Pub/Sub setup
- `handle-rtdn` Edge Function
- Auto-expire, grace period, renew, revoke

### Faza 3 — Ceny dynamiczne
- Pobieranie cen z Google Play ProductDetails
- Wyświetlanie w UI w lokalnej walucie
- Free trial / introductory offers

### Faza 4 — iOS (opcjonalnie)
- StoreKit 2 integracja
- App Store Server Notifications v2
- `AppleSubscriptionManagementService`

---

## 14. Pliki do modyfikacji — podsumowanie

| Plik | Akcja | Opis |
|------|-------|------|
| `Constants/BillingConstants.cs` | **NOWY** | Product ID mapping, endpoints |
| `Models/SubscriptionOperationEntry.cs` | **MODYFIKACJA** | +7 pól: PurchaseToken, ProviderProductId, etc. |
| `FoodbookApp.App.csproj` | **MODYFIKACJA** | +NuGet `Xamarin.Android.Google.BillingClient` |
| `Services/Subscription/PaymentProviderSubscriptionManagementService.cs` | **PRZEPISANIE** | Pełna implementacja Google Play Billing |
| `MauiProgram.cs` | **MODYFIKACJA** | DI: Android → PaymentProvider, inne → Mock |
| `Views/ProfilePage.xaml` | **OPCJONALNA MODYFIKACJA** | Tekst cancel button na Androidzie |
| `Views/ProfilePage.xaml.cs` | **MINIMALNA MODYFIKACJA** | Opcjonalnie: dynamiczne ceny, cancel deep link |
| `supabase/functions/verify-play-purchase/` | **NOWY** | Edge Function — weryfikacja + acknowledge |
| `supabase/functions/sync-play-subscription/` | **NOWY** | Edge Function — batch sync |
| Supabase SQL migration | **NOWY** | Nowe kolumny w `users` |

---

## 15. Decyzje architektoniczne — podsumowanie

| Decyzja | Wybór | Uzasadnienie |
|---------|-------|-------------|
| Backend weryfikacji | Supabase Edge Functions (Deno) | Już używasz Supabase, brak potrzeby na osobny serwer |
| Acknowledge strategy | Server-side (Edge Function) | Bezpieczniejsze, Google zaleca |
| Source of truth dla premium | Supabase `users` (aktualizowane przez Edge Function) | Centralne źródło, `FeatureAccessService` czyta stamtąd |
| Typ subskrypcji w Google Play | Auto-renewing subscriptions | Standard dla mobile SaaS |
| Cancel flow | Deep link do Google Play | Google wymaga — nie można cancelować programmatycznie |
| Inne platformy | Mock (stub) | Android-only w Fazie 1 |
| RTDN | Faza 2 (osobny etap) | Nie blokuje MVP |

---

> **Ten plan jest gotowy do implementacji.** Architektura FoodbookApp jest bardzo dobrze przygotowana — `ISubscriptionManagementService` abstraction pozwala na podmianę implementacji bez zmian w UI/FeatureAccessService.
