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



SONNET PLAN:

# Plan Implementacji: Google Play Billing — FoodBook App

**Status:** gotowy do przekazania agentowi AI (Codex)
**Bazuje na:** `Google Play Billing – Ocena i plan` (dokument źródłowy PP) + weryfikacja aktualnego stanu API Google (18.07.2026)
**Dla:** .NET MAUI 9 / C# 13, Supabase (Postgres + Edge Functions), Android (Google Play)

---

## 0. ZANIM AGENT ZACZNIE KODOWAĆ — rzeczy krytyczne

### 0.1 Pilny deadline Google (nowa informacja, nie było w dokumencie źródłowym)

Zweryfikowałem to przed napisaniem planu, bo zmienia priorytety:

- **31 sierpnia 2026** — wszystkie nowe aplikacje i aktualizacje muszą używać **Google Play Billing Library w wersji 8 lub nowszej**. Można poprosić o przedłużenie do **1 listopada 2026** przez ticket w Play Console, ale to nie jest pewne.
- To jest **~6 tygodni od teraz**. Faza 1 i 4 (BillingClient) powinny mieć priorytet nad kosmetyką w innych częściach premium (np. Foodbook templates), bo inaczej ryzykujesz blokadę publikacji aktualizacji.
- Dobra wiadomość: oficjalny binding `.NET for Android/MAUI` istnieje i jest aktualny — pakiet **`Xamarin.Android.Google.BillingClient` w wersji `8.3.0.1`** (nie `7.*` jak w dokumencie źródłowym). Trzeba go użyć w wersji 8.x, nie 7.x.
- **Wymagany wpis w `AndroidManifest.xml`** (często pomijany, potem Play Console odrzuca wysyłkę):
  ```xml
  <meta-data android:name="com.google.android.play.billingclient.version" android:value="8" />
  ```

### 0.2 Zmiana API po stronie Google (dokument źródłowy używał starego API)

- `purchases.subscriptions.get` (v1) jest **deprecated** → trzeba użyć **`purchases.subscriptionsv2.get`**.
- Odpowiedź ma inny kształt: `expiryTimeMillis` (v1, long) → `lineItems[].expiryTime` (v2, ISO 8601 string), `subscriptionState` zamiast prostego `paymentState`.
- `acknowledge` **zostaje bez zmian** (nie ma wersji v2) — ale **do zweryfikowania tuż przed wdrożeniem**, czy segment `{subscriptionId}` w URL nadal jest wymagany (dokumentacja Google od maja 2025 sygnalizuje, że subscriptionId nie jest już wymagany, ale referencja REST wciąż go pokazuje jako parametr wymagany — to się mogło zmienić, sprawdź `developers.google.com/android-publisher/api-ref/rest/v3/purchases.subscriptions/acknowledge` przed implementacją).
- Google wymusza Billing Library v8+ (patrz 0.1) — to niezależna, ale powiązana zmiana.

### 0.3 Reguła krytyczna dla RTDN (webhooki)

Pub/Sub gwarantuje **at-least-once delivery** i **brak gwarancji kolejności**. To oznacza:
- **Deduplikacja po `messageId`** Pub/Sub jest obowiązkowa (nie tylko po `purchase_token`), inaczej podwójne/nie-po-kolei komunikaty popsują stan.
- Rekomendowane podejście (potwierdzone dobrą praktyką branżową): **notification z RTDN to tylko sygnał "coś się zmieniło"** — po jego otrzymaniu zawsze wykonaj `purchases.subscriptionsv2.get`, żeby pobrać aktualny, autorytatywny stan, zamiast ufać samej treści notyfikacji.

### 0.4 Weryfikacja istniejącego kodu — KROK 0 dla agenta, obowiązkowy

Zanim agent utworzy jakikolwiek nowy plik, **musi**:

1. Znaleźć w repo aktualne nazwy i sygnatury: `IFeatureAccessService`, `ISubscriptionService` / `ISubscriptionManagementService` (nazwa mogła się różnić — w dokumencie źródłowym PP użyto `ISubscriptionManagementService`, we wcześniejszych ustaleniach projektu pojawia się też `IPurchaseService` — **agent ma ustalić, która nazwa faktycznie istnieje w kodzie i nie tworzyć duplikatu**).
2. Sprawdzić istniejące Edge Functions: `activate-premium`, `deactivate-expired-premium`, `get-feature-access-snapshot`, `register-ad-unlock` (jeśli już istnieją — **nowa funkcja `verify-purchase` powinna je wywoływać / reużywać logikę, a nie duplikować** upsert do tabeli premium — zgodnie z zasadą DRY z `AGENTS.md`).
3. Sprawdzić aktualny schemat tabeli w Supabase odpowiedzialnej za status premium (prawdopodobnie `users` albo `subscriptions`) — plan niżej zakłada rozszerzenie istniejącej tabeli, nie tworzenie nowej, ale trzeba potwierdzić nazwę.
4. Sprawdzić `MauiProgram.cs` pod kątem obecnych rejestracji DI związanych z premium/subskrypcją oraz konwencję lifetime (Scoped/Singleton/Transient) używaną dla serwisów (nie ViewModeli) w tym projekcie, żeby nowe serwisy były spójne.
5. Sprawdzić `.csproj` pod kątem `TargetFrameworks` (musi zawierać `-android`) i istniejących warunkowych `ItemGroup Condition="...android..."`.

**Jeśli którekolwiek z powyższych nie zgadza się z założeniami w tym planie — agent ma dostosować nazwy/pliki do stanu faktycznego repo, zachowując logikę i kolejność faz.**

### 0.5 Ograniczenia środowiska Codex — WAŻNE dla planowania weryfikacji

- Środowisko Codex: `.NET SDK 9.0.204`, **brak workloadu MAUI**, brak `dotnet restore` w setup script.
- **Konsekwencja:** kod platformowy Android (`Platforms/Android/...`, referencje do `Xamarin.Android.Google.BillingClient`) **nie da się zbudować/zweryfikować kompilacją w Codex**. Agent powinien:
  - Pisać ten kod starannie i statycznie go przejrzeć (poprawność składni, nazw typów wg dokumentacji bindingu), ale **oznaczyć wyraźnie w opisie PR, że wymaga weryfikacji lokalnej w Visual Studio 2022 z workloadem `.NET MAUI` + Android SDK**.
  - Priorytetowo budować i testować (unit testy xUnit) całą logikę **niezależną od Androida** (interfejsy, `GooglePlaySubscriptionManagementService` z mockiem `IBillingService`, `SubscriptionBackendApi`) — to *da się* zweryfikować w Codex.
- Supabase Edge Functions (Deno/TypeScript) **nie wymagają** workloadu MAUI — agent może je pisać i sprawdzać składniowo w Codex, ale **deployment (`supabase functions deploy`, `supabase secrets set`) prawdopodobnie wymaga lokalnego CLI z auth PP** — potraktować jako krok manualny, chyba że Codex ma skonfigurowany dostęp do Supabase CLI z tokenem projektu.

### 0.6 Zmiana architektoniczna względem wcześniejszych ustaleń

We wcześniejszym planowaniu premium (Etap A) wspominany był RevenueCat jako warstwa pośrednicząca w IAP. Dokument źródłowy do tego planu **nie używa RevenueCat** — jest to bezpośrednia integracja z Google Play Billing Library. Plan poniżej realizuje wariant **bez RevenueCat** (zgodnie z dokumentem źródłowym). Jeśli to nie jest aktualna decyzja, trzeba to skorygować przed startem, bo zmienia całą Fazę 3–4 (SDK RevenueCat zamiast własnego `GooglePlayBillingService`).

---

## 1. Cel i zakres

Zastąpić `MockSubscriptionManagementService` prawdziwą integracją z Google Play Billing, z pełnym cyklem: zakup → weryfikacja backendowa → aktywacja premium w Supabase → obsługa odnowień/anulowań przez webhooki → restore purchases przy starcie aplikacji.

Zakres **nie obejmuje** iOS/Windows billing (poza zaślepką rzucającą `NotSupportedException`, żeby DI się nie wysypało na innych platformach).

---

## 2. Architektura docelowa (warstwy)

```
UI (PremiumGateBottomSheet, ProfilePage)
        │
ISubscriptionManagementService  ← jedyne miejsce zmiany dla UI/FeatureAccessService
        │
        ├── IBillingService (Singleton, cross-platform interfejs)
        │       └── Platforms/Android/GooglePlayBillingService (BillingClient v8)
        │       └── Platforms/{iOS,Windows}/UnsupportedBillingService (stub)
        │
        └── ISubscriptionBackendApi
                └── HTTP → Supabase Edge Function `verify-purchase`
                                └── Google Play Developer API (subscriptionsv2)
                                └── Supabase Postgres (upsert premium)

Google Play ──RTDN──> Cloud Pub/Sub ──push──> Edge Function `handle-rtdn`
                                                    └── subscriptionsv2.get (re-weryfikacja)
                                                    └── Supabase Postgres (update)
```

`IFeatureAccessService` pozostaje bez zmian koncepcyjnych — po `RefreshAccessAsync()` czyta zaktualizowany stan z Supabase.

---

## 3. Mapa plików (nowe / modyfikowane)

| Ścieżka | Akcja | Uwagi |
|---|---|---|
| `FoodbookApp.App/Interfaces/IBillingService.cs` | nowy | patrz PR1 |
| `FoodbookApp.App/Interfaces/ISubscriptionBackendApi.cs` | nowy | patrz PR2 |
| `FoodbookApp.App/Models/PurchaseResult.cs` | nowy | record |
| `FoodbookApp.App/Models/DTOs/VerifyPurchaseRequest.cs` | nowy | DTO do Edge Function |
| `FoodbookApp.App/Models/DTOs/VerifyPurchaseResponse.cs` | nowy | DTO z Edge Function |
| `FoodbookApp.App/Services/Subscription/SubscriptionBackendApi.cs` | nowy | HTTP klient |
| `FoodbookApp.App/Services/Subscription/GooglePlaySubscriptionManagementService.cs` | nowy | zastępuje/uzupełnia mock |
| `FoodbookApp.App/Platforms/Android/Services/GooglePlayBillingService.cs` | nowy | wymaga lokalnego build |
| `FoodbookApp.App/Platforms/iOS/Services/UnsupportedBillingService.cs` | nowy | stub |
| `FoodbookApp.App/Platforms/Windows/Services/UnsupportedBillingService.cs` | nowy | stub |
| `FoodbookApp.App/FoodbookApp.App.csproj` | edycja | dodanie PackageReference warunkowego |
| `FoodbookApp.App/Platforms/Android/AndroidManifest.xml` | edycja | meta-data billingclient.version |
| `FoodbookApp.App/MauiProgram.cs` | edycja | rejestracja DI |
| `FoodbookApp.App/Localization/*.resx` (pl, en, de, fr, es, kr) | edycja | nowe klucze błędów/statusów |
| `FoodbookApp.Tests/Services/GooglePlaySubscriptionManagementServiceTests.cs` | nowy | mock `IBillingService` |
| `FoodbookApp.Tests/Services/SubscriptionBackendApiTests.cs` | nowy | mock `HttpMessageHandler` |
| `supabase/migrations/xxxx_billing_subscriptions.sql` | nowy | patrz PR5 |
| `supabase/functions/verify-purchase/index.ts` | nowy | patrz PR5 |
| `supabase/functions/handle-rtdn/index.ts` | nowy | patrz PR5 |
| `FoodbookApp.App/DOCS/DOCUMENTATION-GUIDE.md` | edycja | nowa sekcja §7 wg szablonu §2.13 |
| `FoodbookApp.App/DOCS/PROJECT-FILES.md` | edycja | nowe katalogi |
| `FoodbookApp.App/DOCS/ARCHITECTURE.md` | edycja | jeśli opisuje warstwy usług |

---

## 4. FAZA 0 — Google Play Console (checklist ręczny, NIE kod)

```
[ ] Produkty subskrypcyjne: foodbook_premium_monthly, foodbook_premium_yearly
    (jeśli już utworzone wg wcześniejszych ustaleń — tylko zweryfikować base plans/offers)
[ ] Setup → API access → utworzenie Service Account (jeśli nie istnieje)
[ ] Nadanie uprawnień service account: "Financial data", "Orders and subscriptions"
    (wystarczy view/read do weryfikacji zakupów)
[ ] Pobranie klucza JSON service account
    → NIE commitować do repo. Zapisać jako Supabase secret (patrz PR5, sekcja bezpieczeństwo)
[ ] Google Cloud Console → włączyć "Google Play Android Developer API"
[ ] Google Cloud Console → Pub/Sub → utworzyć topic (np. play-billing-rtdn)
[ ] Nadać uprawnienie publikowania na topic dla:
    google-play-developer-notifications@system.gserviceaccount.com (rola: Pub/Sub Publisher)
[ ] Utworzyć push subscription na topicu wskazującą na URL Edge Function `handle-rtdn`
[ ] Play Console → Monetization setup → połączyć topic RTDN z aplikacją
[ ] Setup → License testing → dodać e-maile testerów (unikniesz obciążania prawdziwej karty)
[ ] Sprawdzić plan closed alpha (12 testerów / 14 dni) — testy sandboxowe działają
    niezależnie od statusu produkcyjnego, ale realny zakup w Production wymaga
    ukończenia alpha (masz to już w toku wg wcześniejszych ustaleń)
[ ] Rozważyć zgłoszenie przedłużenia terminu Billing Library v8 (do 1.11.2026) jako
    bufor bezpieczeństwa, NIE jako powód do opóźniania Fazy 1/4
```

---

## 5. FAZA 1 (PR1) — Kontrakty i modele (bez zależności od Androida)

**Cel:** kod w 100% buildowalny w Codex, stanowi fundament pod resztę faz.

### 5.1 `Models/PurchaseResult.cs`

```csharp
namespace FoodbookApp.Models;

/// <summary>
/// Wynik operacji zakupu lub odczytu subskrypcji zwracany przez natywny billing client.
/// </summary>
/// <param name="IsSuccess">Czy operacja zakończyła się sukcesem.</param>
/// <param name="PurchaseToken">Token zakupu do weryfikacji backendowej (null przy błędzie/anulowaniu).</param>
/// <param name="ProductId">Identyfikator produktu/subskrypcji, którego dotyczy wynik.</param>
/// <param name="ErrorMessage">Komunikat błędu (m.in. "UserCancelled") gdy IsSuccess == false.</param>
public sealed record PurchaseResult(
    bool IsSuccess,
    string? PurchaseToken,
    string? ProductId,
    string? ErrorMessage);
```

### 5.2 `Interfaces/IBillingService.cs`

```csharp
using FoodbookApp.Models;

namespace FoodbookApp.Interfaces;

/// <summary>
/// Platformowa abstrakcja nad natywnym klientem płatności (Google Play Billing na Androidzie).
/// Implementacja właściwa: Platforms/Android/Services/GooglePlayBillingService.
/// Na platformach bez wsparcia rejestrowana jest implementacja zaślepkowa.
/// </summary>
public interface IBillingService
{
    /// <summary>Inicjalizuje połączenie z natywnym billing clientem.</summary>
    Task<bool> InitializeAsync();

    /// <summary>Uruchamia natywny ekran zakupu dla wskazanego identyfikatora produktu.</summary>
    Task<PurchaseResult> LaunchPurchaseFlowAsync(string productId);

    /// <summary>Zwraca identyfikatory aktualnie posiadanych, aktywnych subskrypcji.</summary>
    Task<IReadOnlyList<string>> GetOwnedSubscriptionsAsync();

    /// <summary>Wywoływane asynchronicznie po zakończeniu przepływu zakupu (sukces/anulowanie/błąd).</summary>
    event EventHandler<PurchaseResult>? PurchaseCompleted;
}
```

### 5.3 `Models/DTOs/VerifyPurchaseRequest.cs` / `VerifyPurchaseResponse.cs`

```csharp
namespace FoodbookApp.Models.DTOs;

/// <summary>Żądanie weryfikacji zakupu wysyłane do Edge Function `verify-purchase`.</summary>
public sealed record VerifyPurchaseRequest(
    string PurchaseToken,
    string ProductId,
    string UserId);

/// <summary>Odpowiedź z Edge Function `verify-purchase`.</summary>
public sealed record VerifyPurchaseResponse(
    bool IsSuccess,
    DateTimeOffset? PremiumUntilUtc,
    string? ErrorMessage);
```

### 5.4 `Interfaces/ISubscriptionBackendApi.cs`

```csharp
using FoodbookApp.Models.DTOs;

namespace FoodbookApp.Interfaces;

/// <summary>Klient komunikujący się z Supabase Edge Functions odpowiedzialnymi za weryfikację zakupów.</summary>
public interface ISubscriptionBackendApi
{
    /// <summary>
    /// Wysyła token zakupu do backendu w celu weryfikacji w Google Play Developer API,
    /// potwierdzenia (acknowledge) zakupu i aktywacji premium w Supabase.
    /// </summary>
    Task<VerifyPurchaseResponse> VerifyAndActivateAsync(string purchaseToken, string productId);
}
```

### Kryteria akceptacji PR1
- [ ] Projekt się buduje (`dotnet build` na netstandard/non-android TFM) bez ostrzeżeń Sonar.
- [ ] Wszystkie publiczne typy mają dokumentację XML (wymóg `AGENTS.md`).
- [ ] Nullable reference types respektowane.
- [ ] Brak logiki biznesowej — to czysto kontraktowy PR.

---

## 6. FAZA 2 (PR2) — `SubscriptionBackendApi`

```csharp
namespace FoodbookApp.Services.Subscription;

/// <summary>
/// Implementacja <see cref="ISubscriptionBackendApi"/> komunikująca się z Supabase Edge Functions
/// przez HTTPS. Nie zawiera logiki weryfikacji Google — to wyłącznie klient HTTP.
/// </summary>
public sealed class SubscriptionBackendApi : ISubscriptionBackendApi
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService; // zakładam istniejący serwis dostarczający UserId/token sesji — zweryfikować nazwę w repo

    public SubscriptionBackendApi(HttpClient httpClient, IAuthService authService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<VerifyPurchaseResponse> VerifyAndActivateAsync(string purchaseToken, string productId)
    {
        var userId = await _authService.GetCurrentUserIdAsync();
        var request = new VerifyPurchaseRequest(purchaseToken, productId, userId);

        using var response = await _httpClient.PostAsJsonAsync("verify-purchase", request);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return new VerifyPurchaseResponse(false, null, errorBody);
        }

        var result = await response.Content.ReadFromJsonAsync<VerifyPurchaseResponse>();
        return result ?? new VerifyPurchaseResponse(false, null, "Pusta odpowiedź backendu");
    }
}
```

**Uwaga dla agenta:** `HttpClient.BaseAddress` powinien wskazywać na `{SUPABASE_URL}/functions/v1/`, a nagłówek `Authorization: Bearer {SUPABASE_ANON_KEY albo user JWT}` musi być ustawiony tam, gdzie w repo skonfigurowane są już inne wywołania do Supabase (prawdopodobnie istniejący `HttpClientFactory`/`SupabaseHttpClient` — użyć tego samego mechanizmu zamiast tworzyć nowy, zgodnie z DRY).

### Testy (xUnit + Moq)
- Mock `HttpMessageHandler` zwracający 200 z poprawnym JSON → sprawdzić poprawne zmapowanie `VerifyPurchaseResponse`.
- Mock zwracający 4xx/5xx → sprawdzić `IsSuccess == false` i wypełnione `ErrorMessage`.
- Mock zwracający pusty content przy 200 → sprawdzić fallback bez wyjątku.

---

## 7. FAZA 3 (PR3) — `GooglePlaySubscriptionManagementService` + DI

```csharp
namespace FoodbookApp.Services.Subscription;

/// <summary>
/// Implementacja <see cref="ISubscriptionManagementService"/> oparta o Google Play Billing.
/// Deleguje uruchomienie natywnego ekranu zakupu do <see cref="IBillingService"/>,
/// a weryfikację/aktywację do <see cref="ISubscriptionBackendApi"/>.
/// </summary>
public sealed class GooglePlaySubscriptionManagementService : ISubscriptionManagementService, IDisposable
{
    private readonly IBillingService _billingService;
    private readonly ISubscriptionBackendApi _backendApi;
    private readonly IFeatureAccessService _featureAccessService;

    public GooglePlaySubscriptionManagementService(
        IBillingService billingService,
        ISubscriptionBackendApi backendApi,
        IFeatureAccessService featureAccessService)
    {
        _billingService = billingService ?? throw new ArgumentNullException(nameof(billingService));
        _backendApi = backendApi ?? throw new ArgumentNullException(nameof(backendApi));
        _featureAccessService = featureAccessService ?? throw new ArgumentNullException(nameof(featureAccessService));

        _billingService.PurchaseCompleted += OnPurchaseCompleted;
    }

    public async Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan plan)
    {
        var productId = PlanToProductId(plan);

        var initialized = await _billingService.InitializeAsync();
        if (!initialized)
            return SubscriptionActionResult.Failed("Nie udało się połączyć z usługą płatności Google Play");

        var launchResult = await _billingService.LaunchPurchaseFlowAsync(productId);

        return launchResult.IsSuccess
            ? SubscriptionActionResult.Pending()
            : SubscriptionActionResult.Failed(launchResult.ErrorMessage);
    }

    /// <summary>
    /// Wywoływane przy starcie aplikacji / wejściu na ProfilePage — sprawdza aktywne
    /// subskrypcje po stronie Google i re-weryfikuje je backendowo (restore purchases).
    /// </summary>
    public async Task RestorePurchasesAsync()
    {
        await _billingService.InitializeAsync();
        var owned = await _billingService.GetOwnedSubscriptionsAsync();

        // Brak PurchaseToken z tej metody API — potrzebne osobne query purchases
        // przez BillingClient.QueryPurchasesAsync (already zwraca purchase.PurchaseToken).
        // TODO agenta: rozszerzyć IBillingService o metodę zwracającą pełne obiekty
        // (ProductId + PurchaseToken), bo same identyfikatory produktów nie wystarczą
        // do wywołania VerifyAndActivateAsync. Patrz uwaga w sekcji 7.1 poniżej.
    }

    private async void OnPurchaseCompleted(object? sender, PurchaseResult purchase)
    {
        if (!purchase.IsSuccess || purchase.PurchaseToken is null || purchase.ProductId is null)
            return;

        try
        {
            var result = await _backendApi.VerifyAndActivateAsync(purchase.PurchaseToken, purchase.ProductId);

            if (result.IsSuccess)
                await _featureAccessService.RefreshAccessAsync();
            else
                Debug.WriteLine($"[Subscription] Weryfikacja backendowa nieudana: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Subscription] Błąd weryfikacji zakupu: {ex.Message}");
        }
    }

    private static string PlanToProductId(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.PremiumMonthly => "foodbook_premium_monthly",
        SubscriptionPlan.PremiumYearly => "foodbook_premium_yearly",
        _ => throw new ArgumentOutOfRangeException(nameof(plan))
    };

    public void Dispose() => _billingService.PurchaseCompleted -= OnPurchaseCompleted;
}
```

### 7.1 Ważna poprawka względem dokumentu źródłowego

Dokument źródłowy zakładał, że `GetOwnedSubscriptionsAsync()` zwraca same `ProductId` — **to za mało do restore purchases**, bo do weryfikacji backendowej potrzebny jest `PurchaseToken`. Trzeba rozszerzyć `IBillingService`:

```csharp
public interface IBillingService
{
    // ... istniejące metody ...

    /// <summary>Zwraca aktywne zakupy subskrypcji wraz z tokenami (do restore purchases).</summary>
    Task<IReadOnlyList<PurchaseResult>> GetActivePurchasesAsync();
}
```

W `GooglePlayBillingService` (Faza 4) implementacja mapuje `QueryPurchasesAsync` na listę `PurchaseResult(true, purchase.PurchaseToken, product, null)` per każdy `Purchase`. `RestorePurchasesAsync()` w Fazie 3 iteruje po tej liście i wywołuje `_backendApi.VerifyAndActivateAsync` dla każdego wpisu.

### DI (`MauiProgram.cs`)

```csharp
#if ANDROID
builder.Services.AddSingleton<IBillingService, FoodbookApp.Platforms.Android.Services.GooglePlayBillingService>();
#elif IOS
builder.Services.AddSingleton<IBillingService, FoodbookApp.Platforms.iOS.Services.UnsupportedBillingService>();
#else
builder.Services.AddSingleton<IBillingService, FoodbookApp.Platforms.Windows.Services.UnsupportedBillingService>();
#endif

builder.Services.AddScoped<ISubscriptionBackendApi, SubscriptionBackendApi>();

// UWAGA: zweryfikować dokładny lifetime używany dziś dla ISubscriptionManagementService
// (Mock) w repo i zachować spójność — poniżej założenie Scoped.
builder.Services.AddScoped<ISubscriptionManagementService, GooglePlaySubscriptionManagementService>();
```

**Uwaga:** `IBillingService` jako `Singleton` jest celowe — `BillingClient` utrzymuje jedno połączenie na cały cykl życia aplikacji; tworzenie go per-scope byłoby błędem (wielokrotne połączenia = wycieki i race conditions).

### Testy (xUnit + Moq + FluentAssertions)
- Mock `IBillingService` emitujący `PurchaseCompleted` z sukcesem → sprawdzić wywołanie `_backendApi.VerifyAndActivateAsync` z poprawnym tokenem i wywołanie `_featureAccessService.RefreshAccessAsync()`.
- Mock zwracający `IsSuccess == false` (np. `UserCancelled`) → sprawdzić, że backend NIE jest wywoływany.
- Mock `_backendApi` rzucający wyjątek → sprawdzić brak propagacji wyjątku poza handler (nie crashuje appki).
- `Dispose()` → sprawdzić odpięcie handlera zdarzenia (weryfikacja przez ponowne wywołanie eventu po Dispose — nie powinno nic zrobić).

---

## 8. FAZA 4 (PR4) — `GooglePlayBillingService` (Android)

**Uwaga:** ten PR wymaga lokalnej weryfikacji budowania w Visual Studio 2022 (workload MAUI + Android SDK) — Codex tego nie zweryfikuje kompilacją.

### 8.1 `.csproj`

```xml
<ItemGroup Condition="$(TargetFramework.Contains('-android'))">
  <PackageReference Include="Xamarin.Android.Google.BillingClient" Version="8.3.0.1" />
</ItemGroup>
```

### 8.2 `AndroidManifest.xml`

```xml
<application ...>
    <meta-data android:name="com.google.android.play.billingclient.version" android:value="8" />
</application>
```

### 8.3 `Platforms/Android/Services/GooglePlayBillingService.cs`

```csharp
#if ANDROID
using Android.BillingClient.Api;
using FoodbookApp.Interfaces;
using FoodbookApp.Models;

namespace FoodbookApp.Platforms.Android.Services;

public sealed class GooglePlayBillingService : Java.Lang.Object,
    IBillingClientStateListener,
    IPurchasesUpdatedListener,
    IBillingService
{
    private BillingClient? _client;
    private TaskCompletionSource<bool>? _connectionTcs;
    private int _reconnectAttempts;
    private const int MaxReconnectAttempts = 3;

    public event EventHandler<PurchaseResult>? PurchaseCompleted;

    public async Task<bool> InitializeAsync()
    {
        _connectionTcs = new TaskCompletionSource<bool>();

        var context = Platform.CurrentActivity
            ?? throw new InvalidOperationException("Brak bieżącej aktywności Androida");

        var pendingParams = PendingPurchasesParams.NewBuilder()
            .EnableOneTimeProducts()
            .Build();

        _client = BillingClient.NewBuilder(context)
            .SetListener(this)
            .EnablePendingPurchases(pendingParams)
            .Build();

        _client.StartConnection(this);

        return await _connectionTcs.Task;
    }

    public void OnBillingSetupFinished(BillingResult result)
    {
        var ok = result.ResponseCode == BillingResponseCode.Ok;
        _reconnectAttempts = 0;
        _connectionTcs?.TrySetResult(ok);
    }

    public void OnBillingServiceDisconnected()
    {
        // Retry z prostym backoffem — BillingClient nie łączy się ponownie automatycznie
        if (_reconnectAttempts >= MaxReconnectAttempts) return;
        _reconnectAttempts++;
        _client?.StartConnection(this);
    }

    public async Task<PurchaseResult> LaunchPurchaseFlowAsync(string productId)
    {
        if (_client is null || !_client.IsReady)
            return new PurchaseResult(false, null, productId, "BillingClient nie jest gotowy");

        var productList = new List<QueryProductDetailsParams.Product>
        {
            QueryProductDetailsParams.Product.NewBuilder()
                .SetProductId(productId)
                .SetProductType(BillingClient.ProductType.Subs)
                .Build()
        };

        var queryParams = QueryProductDetailsParams.NewBuilder()
            .SetProductList(productList)
            .Build();

        var queryResult = await _client.QueryProductDetailsAsync(queryParams);

        if (queryResult.Result.ResponseCode != BillingResponseCode.Ok)
            return new PurchaseResult(false, null, productId, $"Zapytanie nieudane: {queryResult.Result.DebugMessage}");

        var productDetails = queryResult.ProductDetails.FirstOrDefault(p => p.ProductId == productId);
        if (productDetails is null)
            return new PurchaseResult(false, null, productId, "Nie znaleziono produktu");

        var offerToken = productDetails.GetSubscriptionOfferDetails()?.FirstOrDefault()?.OfferToken;
        // UWAGA: dla subskrypcji z wieloma base plans/offers trzeba wybrać właściwy offerToken,
        // nie zawsze pierwszy z listy — do doprecyzowania wg konfiguracji w Play Console.

        var productDetailsParamsBuilder = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(productDetails);

        if (!string.IsNullOrEmpty(offerToken))
            productDetailsParamsBuilder.SetOfferToken(offerToken);

        var flowParams = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList(new[] { productDetailsParamsBuilder.Build() })
            .Build();

        var activity = Platform.CurrentActivity!;
        var billingResult = _client.LaunchBillingFlow(activity, flowParams);

        if (billingResult.ResponseCode != BillingResponseCode.Ok)
            return new PurchaseResult(false, null, productId, $"Uruchomienie nieudane: {billingResult.DebugMessage}");

        return new PurchaseResult(true, null, productId, null);
    }

    public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        if (result.ResponseCode == BillingResponseCode.Ok && purchases is not null)
        {
            foreach (var purchase in purchases.Where(p => p.PurchaseState == PurchaseState.Purchased))
            {
                PurchaseCompleted?.Invoke(this, new PurchaseResult(
                    true, purchase.PurchaseToken, purchase.Products.FirstOrDefault(), null));
            }
        }
        else if (result.ResponseCode == BillingResponseCode.UserCancelled)
        {
            PurchaseCompleted?.Invoke(this, new PurchaseResult(false, null, null, "UserCancelled"));
        }
        else
        {
            PurchaseCompleted?.Invoke(this, new PurchaseResult(false, null, null, result.DebugMessage));
        }
    }

    public async Task<IReadOnlyList<string>> GetOwnedSubscriptionsAsync()
    {
        var purchases = await GetActivePurchasesAsync();
        return purchases.Select(p => p.ProductId!).ToList();
    }

    public async Task<IReadOnlyList<PurchaseResult>> GetActivePurchasesAsync()
    {
        if (_client is null) return Array.Empty<PurchaseResult>();

        var queryParams = QueryPurchasesParams.NewBuilder()
            .SetProductType(BillingClient.ProductType.Subs)
            .Build();

        var result = await _client.QueryPurchasesAsync(queryParams);

        return result.Purchases
            .Where(p => p.PurchaseState == PurchaseState.Purchased)
            .Select(p => new PurchaseResult(true, p.PurchaseToken, p.Products.FirstOrDefault(), null))
            .ToList();
    }
}
#endif
```

### 8.4 Stub dla iOS/Windows

```csharp
namespace FoodbookApp.Platforms.iOS.Services; // analogicznie Windows

public sealed class UnsupportedBillingService : IBillingService
{
    public Task<bool> InitializeAsync() => throw new NotSupportedException("Google Play Billing dostępny tylko na Androidzie");
    public Task<PurchaseResult> LaunchPurchaseFlowAsync(string productId) => throw new NotSupportedException();
    public Task<IReadOnlyList<string>> GetOwnedSubscriptionsAsync() => throw new NotSupportedException();
    public Task<IReadOnlyList<PurchaseResult>> GetActivePurchasesAsync() => throw new NotSupportedException();
    public event EventHandler<PurchaseResult>? PurchaseCompleted { add { } remove { } }
}
```

### Kryteria akceptacji PR4 (weryfikacja MANUALNA przez PP, nie przez Codex)
- [ ] Build Android w VS 2022 przechodzi bez błędów.
- [ ] `adb logcat` pokazuje `OnBillingSetupFinished` z `Ok` po starcie appki na urządzeniu/emulatorze z zalogowanym kontem testowym.
- [ ] Zapytanie o produkty (`QueryProductDetailsAsync`) zwraca oba SKU (`foodbook_premium_monthly`, `foodbook_premium_yearly`) — wymaga wcześniej ukończonej Fazy 0.

---

## 9. FAZA 5 (PR5) — Backend Supabase

### 9.1 Migracja SQL

```sql
-- supabase/migrations/xxxx_billing_subscriptions.sql
-- Zakładam istniejącą tabelę `subscriptions` z wcześniejszego Etapu A (activate-premium).
-- Jeśli tabela ma inną nazwę — dostosować.

alter table public.subscriptions
  add column if not exists purchase_token text,
  add column if not exists product_id text,
  add column if not exists subscription_state text,
  add column if not exists last_rtdn_message_id text,
  add column if not exists last_notification_at timestamptz;

create unique index if not exists subscriptions_purchase_token_idx
  on public.subscriptions (purchase_token)
  where purchase_token is not null;

-- Deduplikacja RTDN po Pub/Sub messageId (at-least-once delivery)
create table if not exists public.processed_rtdn_messages (
  message_id text primary key,
  processed_at timestamptz not null default now()
);

-- RLS: użytkownik nie może samodzielnie ustawić is_premium/purchase_token —
-- te kolumny modyfikowane WYŁĄCZNIE przez Edge Functions z service_role key.
-- (zweryfikować istniejące policy na tabeli `subscriptions`, dopisać restrykcję
-- na UPDATE tych konkretnych kolumn dla roli `authenticated`, jeśli jeszcze jej nie ma)
```

### 9.2 `supabase/functions/verify-purchase/index.ts`

```typescript
import { serve } from "https://deno.land/std/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js";
import { GoogleAuth } from "npm:google-auth-library";

const PACKAGE_NAME = Deno.env.get("ANDROID_PACKAGE_NAME")!;
const SERVICE_ACCOUNT_JSON = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON")!;
const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SUPABASE_SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

async function getGoogleAccessToken(): Promise<string> {
  const credentials = JSON.parse(SERVICE_ACCOUNT_JSON);
  const auth = new GoogleAuth({
    credentials,
    scopes: ["https://www.googleapis.com/auth/androidpublisher"],
  });
  const client = await auth.getClient();
  const { token } = await client.getAccessToken();
  if (!token) throw new Error("Nie udało się uzyskać tokenu dostępu Google");
  return token;
}

serve(async (req) => {
  try {
    const { purchaseToken, productId, userId } = await req.json();
    if (!purchaseToken || !productId || !userId) {
      return new Response(JSON.stringify({ isSuccess: false, errorMessage: "Brak wymaganych parametrów" }), { status: 400 });
    }

    const accessToken = await getGoogleAccessToken();

    // subscriptionsv2 — v1 (purchases.subscriptions.get) jest deprecated
    const verifyUrl = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${PACKAGE_NAME}/purchases/subscriptionsv2/tokens/${purchaseToken}`;
    const googleRes = await fetch(verifyUrl, { headers: { Authorization: `Bearer ${accessToken}` } });

    if (!googleRes.ok) {
      const details = await googleRes.text();
      return new Response(JSON.stringify({ isSuccess: false, errorMessage: `Weryfikacja Google nieudana: ${details}` }), { status: 502 });
    }

    const sub = await googleRes.json();
    const isActive = sub.subscriptionState === "SUBSCRIPTION_STATE_ACTIVE"
      || sub.subscriptionState === "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";

    if (!isActive) {
      return new Response(JSON.stringify({ isSuccess: false, errorMessage: `Subskrypcja w stanie: ${sub.subscriptionState}` }), { status: 400 });
    }

    const expiryTime: string | undefined = sub.lineItems?.[0]?.expiryTime; // ISO 8601 w v2

    const supabase = createClient(SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY);
    const { error } = await supabase.from("subscriptions").upsert({
      user_id: userId,
      is_premium: true,
      purchase_token: purchaseToken,
      product_id: productId,
      subscription_state: sub.subscriptionState,
      premium_until: expiryTime,
      last_notification_at: new Date().toISOString(),
    }, { onConflict: "purchase_token" });

    if (error) {
      return new Response(JSON.stringify({ isSuccess: false, errorMessage: `Błąd zapisu Supabase: ${error.message}` }), { status: 500 });
    }

    // Acknowledge — OBOWIĄZKOWE w ciągu 3 dni.
    // UWAGA: sprawdzić przed wdrożeniem, czy segment {subscriptionId} jest nadal
    // wymagany w tym URL (dokumentacja Google sygnalizuje zmianę od maja 2025).
    const ackUrl = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${PACKAGE_NAME}/purchases/subscriptions/${productId}/tokens/${purchaseToken}:acknowledge`;
    await fetch(ackUrl, { method: "POST", headers: { Authorization: `Bearer ${accessToken}` } });

    return new Response(JSON.stringify({ isSuccess: true, premiumUntilUtc: expiryTime }), { status: 200 });
  } catch (e) {
    return new Response(JSON.stringify({ isSuccess: false, errorMessage: e instanceof Error ? e.message : "Nieznany błąd" }), { status: 500 });
  }
});
```

**Uwaga o `npm:google-auth-library`:** wymaga Deno 2.x z obsługą specyfikatorów `npm:` (aktualne środowisko Supabase Edge Functions to wspiera). Jeśli wystąpią problemy z kompatybilnością, alternatywa: ręczne budowanie i podpisywanie JWT (RS256) biblioteką `djwt` lub Web Crypto API bezpośrednio z `private_key` service accounta, a następnie wymiana na access token przez `POST https://oauth2.googleapis.com/token` z `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer`.

### 9.3 `supabase/functions/handle-rtdn/index.ts`

```typescript
import { serve } from "https://deno.land/std/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js";
import { GoogleAuth } from "npm:google-auth-library";

const PACKAGE_NAME = Deno.env.get("ANDROID_PACKAGE_NAME")!;
const SERVICE_ACCOUNT_JSON = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON")!;

// Wartości potwierdzone z oficjalnej dokumentacji RTDN (developer.android.com/google/play/billing/rtdn-reference)
// jako liczby całkowite w polu notificationType:
const NotificationType = {
  SUBSCRIPTION_RECOVERED: 1,
  SUBSCRIPTION_RENEWED: 2,
  SUBSCRIPTION_CANCELED: 3,
  SUBSCRIPTION_PURCHASED: 4,
  SUBSCRIPTION_ON_HOLD: 5,
  SUBSCRIPTION_IN_GRACE_PERIOD: 6,
  SUBSCRIPTION_RESTARTED: 7,
  SUBSCRIPTION_PRICE_CHANGE_CONFIRMED: 8,
  SUBSCRIPTION_DEFERRED: 9,
  SUBSCRIPTION_PAUSED: 10,
  SUBSCRIPTION_PAUSE_SCHEDULE_CHANGED: 11,
  SUBSCRIPTION_REVOKED: 12,
  SUBSCRIPTION_EXPIRED: 13,
} as const;

async function getGoogleAccessToken(): Promise<string> {
  const credentials = JSON.parse(SERVICE_ACCOUNT_JSON);
  const auth = new GoogleAuth({ credentials, scopes: ["https://www.googleapis.com/auth/androidpublisher"] });
  const client = await auth.getClient();
  const { token } = await client.getAccessToken();
  return token!;
}

serve(async (req) => {
  const body = await req.json();
  const messageId: string | undefined = body?.message?.messageId;
  const dataBase64: string | undefined = body?.message?.data;

  if (!messageId || !dataBase64) return new Response("Brak danych", { status: 400 });

  const supabase = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!);

  // Deduplikacja — Pub/Sub gwarantuje at-least-once, może dostarczyć duplikat
  const { data: existing } = await supabase
    .from("processed_rtdn_messages")
    .select("message_id")
    .eq("message_id", messageId)
    .maybeSingle();

  if (existing) return new Response("OK (już przetworzone)", { status: 200 });

  const decoded = JSON.parse(atob(dataBase64));
  const notification = decoded.subscriptionNotification;

  if (!notification) {
    // testNotification / oneTimeProductNotification — potwierdź odbiór, nic więcej
    await supabase.from("processed_rtdn_messages").insert({ message_id: messageId });
    return new Response("OK", { status: 200 });
  }

  try {
    // Zasada: notyfikacja to tylko sygnał — zawsze pobierz autorytatywny stan
    // przez subscriptionsv2.get zamiast ufać wyłącznie treści notyfikacji
    // (rekomendacja branżowa dot. RTDN, bo Pub/Sub nie gwarantuje kolejności).
    const accessToken = await getGoogleAccessToken();
    const verifyUrl = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${PACKAGE_NAME}/purchases/subscriptionsv2/tokens/${notification.purchaseToken}`;
    const googleRes = await fetch(verifyUrl, { headers: { Authorization: `Bearer ${accessToken}` } });

    if (googleRes.ok) {
      const sub = await googleRes.json();
      const isActive = sub.subscriptionState === "SUBSCRIPTION_STATE_ACTIVE"
        || sub.subscriptionState === "SUBSCRIPTION_STATE_IN_GRACE_PERIOD";
      const expiryTime = sub.lineItems?.[0]?.expiryTime;

      await supabase.from("subscriptions").update({
        is_premium: isActive,
        subscription_state: sub.subscriptionState,
        premium_until: expiryTime,
        last_rtdn_message_id: messageId,
        last_notification_at: new Date().toISOString(),
      }).eq("purchase_token", notification.purchaseToken);
    } else if (notification.notificationType === NotificationType.SUBSCRIPTION_EXPIRED
            || notification.notificationType === NotificationType.SUBSCRIPTION_REVOKED) {
      // Jeśli Google zwróci 404/410 dla wygasłego/unieważnionego tokenu — wyłącz premium mimo braku danych z API
      await supabase.from("subscriptions").update({
        is_premium: false,
        subscription_state: "EXPIRED_OR_REVOKED",
        last_rtdn_message_id: messageId,
        last_notification_at: new Date().toISOString(),
      }).eq("purchase_token", notification.purchaseToken);
    }

    await supabase.from("processed_rtdn_messages").insert({ message_id: messageId });
    return new Response("OK", { status: 200 });
  } catch (e) {
    // Zwrócenie błędu != 200 spowoduje retry przez Pub/Sub — to pożądane przy błędach przejściowych
    return new Response(e instanceof Error ? e.message : "Błąd", { status: 500 });
  }
});
```

### 9.4 Sekrety Supabase (do ustawienia przez PP lokalnie, nie w repo)

```bash
supabase secrets set GOOGLE_SERVICE_ACCOUNT_JSON="$(cat service-account.json)"
supabase secrets set ANDROID_PACKAGE_NAME="com.foodbook.app"   # dopasować do rzeczywistego package name
```

### Kryteria akceptacji PR5
- [ ] Migracja SQL nie koliduje z istniejącym schematem (agent MUSI sprawdzić realną nazwę tabeli premium przed napisaniem `alter table`).
- [ ] `verify-purchase` wywołuje `subscriptionsv2.get`, nie zdeprecjonowane v1.
- [ ] Idempotentność: dwukrotne wywołanie `verify-purchase` z tym samym tokenem nie tworzy duplikatu (unique index działa).
- [ ] `handle-rtdn` deduplikuje po `messageId` przed przetworzeniem.
- [ ] Żaden sekret (service account JSON, service role key) nie trafia do repo git.

---

## 10. FAZA 6 (PR6) — UI i lokalizacja

### 10.1 Nowe klucze `.resx` (dodać do pl/en/de/fr/es/kr — zgodnie z multi-locale projektu)

| Klucz | PL |
|---|---|
| `BillingResources.PurchaseInProgress` | "Trwa przetwarzanie zakupu..." |
| `BillingResources.PurchaseSuccess` | "Premium aktywowane!" |
| `BillingResources.PurchaseCancelled` | "Zakup anulowany" |
| `BillingResources.PurchaseFailed` | "Zakup nie powiódł się. Spróbuj ponownie." |
| `BillingResources.VerificationFailed` | "Weryfikacja zakupu nie powiodła się. Skontaktuj się z pomocą, jeśli problem się powtórzy." |
| `BillingResources.RestoringPurchases` | "Przywracanie zakupów..." |
| `BillingResources.RestoreNoneFound` | "Nie znaleziono aktywnych subskrypcji do przywrócenia." |
| `BillingResources.BillingUnavailable` | "Usługa płatności jest obecnie niedostępna." |

### 10.2 `PremiumGateBottomSheet` — wiring

CTA "Kup Premium" → `ISubscriptionManagementService.ChangePlanAsync(plan)`. Wzorzec zgodny z resztą VM (IsLoading/try-catch-finally, `Shell.Current.DisplayAlert` z zasobami .resx, brak logiki biznesowej w code-behind).

### 10.3 Restore purchases przy starcie

Wywołanie `RestorePurchasesAsync()` w `App.xaml.cs` (po zalogowaniu użytkownika) lub w `OnAppearing` ProfilePage — do ustalenia z PP które miejsce lepiej pasuje do istniejącego flow logowania. **Nie blokować UI** — uruchomić w tle, zaktualizować `FeatureAccessService` po zakończeniu.

---

## 11. FAZA 7 (PR7) — Testy końcowe

### 11.1 Testy jednostkowe (automatyczne, do >80% coverage nowego kodu zgodnie z `AGENTS.md`)
- `GooglePlaySubscriptionManagementServiceTests` (patrz Faza 3)
- `SubscriptionBackendApiTests` (patrz Faza 2)
- **Świadomie poza coverage:** `GooglePlayBillingService` — kod platformowy silnie związany z Android SDK / BillingClient, niepraktyczny do testów jednostkowych bez instrumentacji. Uzasadnić to w PR (zgodnie z zasadą "Preferuj jakość kodu" — izolacja logiki biznesowej do testowalnych klas jest właśnie po to, żeby ten fragment mógł pozostać cienką warstwą integracyjną).

### 11.2 Checklist manualny (sandbox, Internal Testing Track)
```
[ ] Zakup testowy (license tester) → sprawdzić w Supabase: is_premium=true, poprawny premium_until
[ ] Sprawdzić w Google Play Console, że zakup został acknowledged (brak ostrzeżenia o zwrocie po 3 dniach)
[ ] Anulowanie subskrypcji w Play Console (test panel) → sprawdzić SUBSCRIPTION_CANCELED w handle-rtdn logs
[ ] Odczekanie do wygaśnięcia (lub użycie narzędzia do symulacji) → sprawdzić is_premium=false
[ ] Reinstalacja appki → RestorePurchasesAsync przywraca premium bez ponownego zakupu
[ ] Wyłączenie internetu podczas zakupu → sprawdzić czytelny komunikat błędu (zasoby .resx)
```

---

## 12. Kolejność realizacji (dependency order)

```
PR1 (kontrakty) ─┬─> PR2 (backend API klient) ─┐
                 └─> PR3 (management service) ─┼─> PR6 (UI wiring)
                                                 │
PR4 (Android billing, równolegle, wymaga         │
     lokalnego builda) ────────────────────────┘

PR5 (Supabase: SQL + Edge Functions) — niezależny od PR1-4, może iść równolegle,
     ale PR3/PR6 end-to-end nie zadziała bez wdrożonego PR5.

Faza 0 (Play Console) — wymagana przed jakimkolwiek testem end-to-end,
     niezależna od kodu, można robić równolegle z PR1-5.

PR7 (testy końcowe) — na samym końcu, wymaga wszystkich powyższych.
```

Codex może realizować **PR1, PR2, PR3, PR5 (kod, nie deployment)** z pełną weryfikacją. **PR4 i deployment PR5** wymagają PP lokalnie.

---

## 13. Aktualizacja dokumentacji (obowiązkowe wg `AGENTS.md`)

- [ ] `PROJECT-FILES.md` → dodać `Services/Subscription/`, `Platforms/Android/Services/` do struktury katalogów.
- [ ] `DOCUMENTATION-GUIDE.md` → dodać sekcję **"7. Moduł Subskrypcji (Google Play Billing)"** wg szablonu z §2.13 (Cel, Kluczowe klasy, Stany/Properties, Komendy, Przepływ główny, Reguły biznesowe, UI+Binding, Artefakty, Cache, Ryzyka, Przyszłe rozszerzenia).
- [ ] `ARCHITECTURE.md` (jeśli opisuje warstwy serwisów) → dodać `IBillingService` / `ISubscriptionBackendApi` do diagramu warstw.

---

## 14. Checklist jakości przed zamknięciem całego zadania (Sonar/AGENTS.md)

```
[ ] Async/await dla wszystkich operacji I/O
[ ] Obsługa wyjątków przewidywalna, bez pustych catch
[ ] Brak hardcoded stringów w UI — wszystko przez .resx
[ ] XML documentation na wszystkich publicznych API
[ ] Nullable reference types respektowane
[ ] Zdarzenia odsubskrybowane (Dispose / OnDisappearing)
[ ] Brak martwego kodu
[ ] Coverage >80% dla nowej logiki biznesowej (poza platformowym Android glue code)
[ ] Żaden sekret nie trafił do repo (grep po "GOOGLE_SERVICE_ACCOUNT", "service_role", kluczach JSON)
[ ] Dokumentacja projektowa zaktualizowana (sekcja 13 powyżej)
```

---

## 15. Ryzyka i mitygacje

| Ryzyko | Mitygacja |
|---|---|
| Deadline Billing Library v8 (31.08.2026) | Priorytetyzować PR4 nad innymi funkcjami premium; rozważyć wniosek o przedłużenie jako bufor |
| Codex nie zweryfikuje kompilacji Android | Jasno oznaczyć PR4 jako "wymaga lokalnej weryfikacji"; nie mergować bez buildu w VS |
| Zmiana API Google (v1→v2) w trakcie developmentu | Trzymać się wyłącznie `subscriptionsv2`; re-sprawdzić dokumentację Google przed deploymentem produkcyjnym (te API bywają aktualizowane) |
| Pub/Sub duplikaty/out-of-order | Deduplikacja po `messageId` + zawsze re-weryfikacja przez `subscriptionsv2.get` |
| Brak acknowledge w 3 dni → automatyczny zwrot | `verify-purchase` wywołuje acknowledge synchronicznie zaraz po aktywacji; dodać monitoring/alert jeśli się nie uda |
| Service account JSON w repo | Wyłącznie Supabase secrets; code review sprawdza `.gitignore` |
| Rozjazd nazw interfejsów (dokument źródłowy vs istniejący kod) | Krok 0.4 — agent weryfikuje repo przed pisaniem kodu |

---

## 16. Uwaga bezpieczeństwa — niezwiązana z tym zadaniem, ale zauważona

W plikach projektu (`PAT_for_github_modelds`) widoczny jest ciąg wyglądający jak prawdziwy token GitHub (`github_pat_...`). Jeśli to aktywny token — warto go zrotować/unieważnić w GitHub (Settings → Developer settings → Personal access tokens) i nie trzymać go w plikach projektu nawet lokalnie, tylko w menedżerze sekretów (np. zmienne środowiskowe Codex, Supabase secrets, itp.). Nie jest to część planu Billing, ale skoro cały ten plan mówi o niewrzucaniu sekretów do repo — dobra okazja, żeby to poprawić przy okazji.

---

**Ostatnia weryfikacja techniczna dokumentu:** 18.07.2026 (Google Play Developer API, Billing Library deadline, RTDN reference)