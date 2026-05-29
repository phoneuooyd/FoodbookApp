---
name: .NET MAUI - BillingService
description: Cross-platform billing implementation for Android (Google Play Billing), iOS (StoreKit), Mac Catalyst (StoreKit), and Windows (Microsoft Store) using .NET MAUI with MVVM architecture.
page_type: sample
languages:
  - csharp
  - xaml
products:
  - dotnet-maui
urlFragment: cross-platform-billing-service
---

# BillingService (MAUI + Cross-Platform Billing)

A comprehensive .NET MAUI sample that demonstrates implementing in-app purchases for Android, iOS, Mac Catalyst, and Windows applications. This sample shows how to integrate platform-specific billing systems (Google Play Billing for Android, StoreKit for iOS/Mac Catalyst, and Microsoft Store for Windows) with a unified interface and clean MVVM architecture.

![BillingService Demo](Images/billing_demo.png)

## What you'll learn

• How to implement cross-platform billing in a .NET MAUI application across multiple platforms
• How to create a unified billing service interface for Android, iOS, Mac Catalyst, and Windows
• How to use Google Play Billing Client (Android), StoreKit (iOS/Mac Catalyst), and Microsoft Store APIs (Windows)
• How to use MVVM pattern with dependency injection for billing operations
• How to handle product listings, purchases, and purchase restoration on all platforms
• How to implement value converters for dynamic UI updates based on purchase state
• How to structure a billing service with proper initialization and error handling
• Platform-specific best practices for Android, iOS, Mac Catalyst, and Windows billing

## Prerequisites

• .NET 10.0 SDK or later
• Visual Studio 2022 17.13+ or Visual Studio Code with .NET MAUI extension
• Android SDK (for Android deployment)
• Xcode and iOS SDK (for iOS and Mac Catalyst deployment on macOS)
• Windows SDK (for Windows deployment on Windows)
• Google Play Console account (for Android production billing setup)
• Apple Developer account and App Store Connect access (for iOS and Mac Catalyst production billing setup)
• Microsoft Partner Center account (for Windows production billing setup)
• Android device or emulator / iOS device or simulator / Mac device or simulator / Windows device for testing

## Features

### Core Billing Functionality

- **Product Discovery**: Retrieve available in-app products from Google Play / App Store / Microsoft Store
- **Purchase Flow**: Handle secure purchase transactions on all platforms
- **Purchase Restoration**: Restore previous purchases for users across platforms
- **Ownership Verification**: Check if products are already owned
- **Cross-Platform Abstraction**: Unified interface across Android, iOS, Mac Catalyst, and Windows

### Architecture Components

- **IBillingService**: Unified billing service interface
- **BaseBillingService**: Shared base functionality and business logic
- **Services/BillingService.Android.cs**: Android implementation using Google Play Billing Client v7
- **Services/BillingService.iOS.cs**: iOS and Mac Catalyst shared implementation using Apple StoreKit 1
- **Services/BillingService.Windows.cs**: Windows implementation using Microsoft Store APIs
- **MVVM Pattern**: Clean separation with ViewModels and data binding
- **Dependency Injection**: Platform-specific service registration with conditional compilation

### UI Features

- **Product Grid**: Display available products with pricing
- **Purchase Status**: Visual indicators for owned/unowned products
- **Loading States**: User feedback during billing operations
- **Error Handling**: Graceful handling of billing errors

## Project Structure

```
BillingService/
├── Services/
│   ├── IBillingService.cs              # Unified billing service interface
│   ├── BaseBillingService.cs           # Shared base implementation
│   ├── BillingService.Android.cs       # Android billing (Google Play Billing v7)
│   ├── BillingService.iOS.cs           # iOS & Mac Catalyst billing (StoreKit 1)
│   └── BillingService.Windows.cs       # Windows billing (Microsoft Store APIs)
├── Platforms/
│   ├── Android/
│   │   ├── AndroidManifest.xml         # Android permissions and configuration
│   │   └── MainActivity.cs             # Android main activity
│   ├── iOS/
│   │   ├── Info.plist                  # iOS configuration
│   │   └── AppDelegate.cs              # iOS app delegate
│   ├── MacCatalyst/
│   │   ├── Info.plist                  # Mac Catalyst configuration
│   │   └── AppDelegate.cs              # Mac Catalyst app delegate
│   └── Windows/
│       ├── Package.appxmanifest        # Windows package configuration
│       └── App.xaml.cs                 # Windows app configuration
├── Models/
│   ├── Product.cs                      # Product data model
│   └── PurchaseResult.cs               # Purchase result model
├── ViewModels/
│   ├── BaseViewModel.cs                # Base ViewModel with INotifyPropertyChanged
│   └── ProductsViewModel.cs            # Products page ViewModel
├── Views/
│   ├── ProductsPage.xaml               # Products listing page
│   └── ProductsPage.xaml.cs            # Code-behind
└── Converters/
    └── ValueConverters.cs              # XAML value converters
```

## How it's wired

• **`Services/IBillingService.cs`**: Defines the unified contract for billing operations including initialization, product retrieval, and purchase handling across all platforms.

• **`Services/BaseBillingService.cs`**: Provides shared business logic, product definitions, and ownership tracking used by all platform implementations.

• **`Services/BillingService.Android.cs`**: Implements Android billing using Google Play Billing Client v7 with support for product queries, purchases, and restoration. Conditionally compiled for Android targets.

• **`Services/BillingService.iOS.cs`**: Implements iOS and Mac Catalyst billing using StoreKit 1 APIs with transaction observers and purchase restoration. Shared by both iOS and Mac Catalyst platforms through conditional compilation.

• **`Services/BillingService.Windows.cs`**: Implements Windows billing using Microsoft Store APIs (Windows.Services.Store) with support for product queries, purchases, and license verification. Conditionally compiled for Windows targets.

• **`BillingService.csproj`**: Disables default compile items and uses explicit conditional `<Compile Include>` directives to include platform-specific billing implementations based on target framework. Platform files are visible in Solution Explorer via `<None Include>` while being conditionally compiled per platform, enabling code sharing between iOS and Mac Catalyst while maintaining clean separation.

• **`MauiProgram.cs`**: Registers the billing service implementation (`Services.BillingService`) and ViewModels in the dependency injection container, automatically resolving to the correct platform-specific implementation at runtime.

• **`ViewModels/ProductsViewModel.cs`**: Exposes billing operations as commands, manages product collections, and handles UI state updates in a platform-agnostic manner.

• **`Views/ProductsPage.xaml`**: CollectionView displaying products with purchase buttons and visual indicators for ownership status.

• **`Converters/ValueConverters.cs`**: Provides XAML converters for boolean-to-text, boolean-to-color, and inverse boolean transformations.

## Configuration

### Android Setup

1. **Product Configuration**:
   Update the product IDs in your billing service to match those configured in Google Play Console:
   - Sign in to [Google Play Console](https://play.google.com/console)
   - Navigate to your app → Monetize → In-app products
   - Create products with IDs: `Team_license`, `Global_license`, `Unlimited_license`

2. **Testing**:
   - Add license testers in Google Play Console
   - Use internal testing track for testing purchases

### iOS Setup

1. **Product Configuration**:
   Update the product IDs in your billing service to match those configured in App Store Connect:
   - Sign in to [App Store Connect](https://appstoreconnect.apple.com/)
   - Navigate to your app → Features → In-App Purchases
   - Create products with IDs: `Team_license`, `Global_license`, `Unlimited_license`

2. **Testing**:
   - Create sandbox tester accounts in App Store Connect
   - Use sandbox account on device for testing purchases

3. **Additional Requirements**:
   - Sign Paid Applications Agreement in App Store Connect
   - Configure tax and banking information

### Mac Catalyst Setup

1. **Product Configuration**:
   Update the product IDs in your billing service to match those configured in App Store Connect:
   - Sign in to [App Store Connect](https://appstoreconnect.apple.com/)
   - Navigate to your app → Features → In-App Purchases
   - Create products with IDs: `Team_license`, `Global_license`, `Unlimited_license`
   - Note: Mac Catalyst apps can share the same products as iOS apps

2. **Testing**:
   - Create sandbox tester accounts in App Store Connect
   - Use sandbox account on Mac for testing purchases

3. **Additional Requirements**:
   - Sign Paid Applications Agreement in App Store Connect
   - Configure tax and banking information
   - Enable Mac Catalyst capability in your app

### Windows Setup

1. **Product Configuration**:
   Update the product IDs in your billing service to match those configured in Partner Center:
   - Sign in to [Microsoft Partner Center](https://partner.microsoft.com/)
   - Navigate to your app → Monetize → Add-ons
   - Create products with IDs: `Team_license`, `Global_license`, `Unlimited_license`

2. **App Association**:
   - Associate your app with the Microsoft Store in Visual Studio
   - Project → Store → Associate App with the Store
   - Complete the wizard to link your project to your Partner Center app

3. **Testing**:
   - Publish your app to the Store (can be hidden from discovery for testing)
   - Install the Store version on your development device
   - The local license will be used for testing

## Run the Application

### Android

1. Ensure Android SDK is properly configured
2. Set up an Android device or emulator
3. Build and deploy:

   ```bash
   dotnet build -f net10.0-android
   dotnet run -f net10.0-android
   ```

### iOS

1. Ensure Xcode and iOS SDK are installed (macOS only)
2. Set up an iOS device or simulator
3. Build and deploy:

   ```bash
   dotnet build -f net10.0-ios
   dotnet run -f net10.0-ios
   ```

### Mac Catalyst

1. Ensure Xcode and macOS SDK are installed (macOS only)
2. Set up a Mac device or simulator
3. Build and deploy:

   ```bash
   dotnet build -f net10.0-maccatalyst
   dotnet run -f net10.0-maccatalyst
   ```

### Windows

1. Ensure Windows SDK is properly configured (Windows only)
2. Set up a Windows device
3. Build and deploy:

   ```bash
   dotnet build -f net10.0-windows10.0.22000.0
   dotnet run -f net10.0-windows10.0.22000.0
   ```

### Key Features Demonstrated

**Product Listing**: The app retrieves and displays available in-app products with their details and pricing from each platform's store.

**Purchase Flow**: Tapping a product initiates the platform-specific purchase flow (Google Play on Android, StoreKit on iOS/Mac Catalyst, Microsoft Store on Windows) with proper error handling.

**Visual Feedback**: Products show different states (owned/not owned) with color coding and text changes across all platforms.

**Restoration**: Users can restore previous purchases across devices.

## Dependencies

• **Microsoft.Maui.Controls**: Core MAUI framework
• **Xamarin.Android.Google.BillingClient**: Google Play Billing integration
• **Syncfusion.Maui.Toolkit**: Additional UI components
• **Microsoft.Extensions.Logging.Debug**: Debug logging support

## Architecture Patterns

### MVVM Implementation

- Uses `INotifyPropertyChanged` for data binding
- Commands for user interactions
- Separation of concerns between Views and ViewModels

### Service Pattern

- Unified billing interface with platform-specific implementations
- Android: Google Play Billing Client v7
- iOS & Mac Catalyst: StoreKit 1 with transaction observers (shared implementation)
- Windows: Microsoft Store APIs (Windows.Services.Store)
- Conditional compilation in `.csproj` to include appropriate platform files
- Dependency injection for loose coupling and automatic platform resolution

### Error Handling

- Comprehensive error handling in billing operations
- User-friendly error messages
- Graceful degradation when billing is unavailable

## Useful docs and resources

• Google Play Billing documentation — [developer.android.com/google/play/billing](https://developer.android.com/google/play/billing)
• Apple StoreKit documentation — [developer.apple.com/storekit](https://developer.apple.com/storekit/)
• Microsoft Store in-app purchases — [learn.microsoft.com/windows/uwp/monetize/in-app-purchases-and-trials](https://learn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials)
• .NET MAUI documentation — [learn.microsoft.com/dotnet/maui](https://learn.microsoft.com/dotnet/maui/)
• NuGet packages:
  ◦ [Microsoft.Maui.Controls](https://www.nuget.org/packages/Microsoft.Maui.Controls)
  ◦ [Xamarin.Android.Google.BillingClient](https://www.nuget.org/packages/Xamarin.Android.Google.BillingClient)
  ◦ [Syncfusion.Maui.Toolkit](https://www.nuget.org/packages/Syncfusion.Maui.Toolkit)

## Notes

• This sample demonstrates cross-platform billing for Android (Google Play Billing), iOS (StoreKit), Mac Catalyst (StoreKit), and Windows (Microsoft Store)
• Platform-specific billing implementations are located in the `Services/` folder and conditionally compiled based on target framework
• iOS and Mac Catalyst share a single billing implementation file (`BillingService.iOS.cs`) as both platforms use identical StoreKit 1 APIs
• Testing in-app purchases requires:
  - Android: Google Play Console setup and signed APKs
  - iOS: App Store Connect setup and sandbox tester accounts
  - Mac Catalyst: App Store Connect setup and sandbox tester accounts
  - Windows: Partner Center setup and published/test apps
• Always test thoroughly on all platforms before publishing to production

## Security Considerations

• Validate purchases server-side for production applications
• Implement receipt verification
• Handle edge cases like network interruptions during purchases
• Secure storage of purchase information
• For Windows: Ensure app is properly associated with Microsoft Store


Code samples:

1. Android Billig service:
`
using BillingService.Models;
using Microsoft.Extensions.Logging;
using Android.BillingClient.Api;
using AndroidBillingResult = Android.BillingClient.Api.BillingResult;

namespace BillingService.Services;

public class BillingService : BaseBillingService
{
    private BillingClient? _billingClient;
    private readonly object _lockObject = new();
    private BillingClientStateListener? _stateListener;
    private PurchasesUpdatedListener? _purchaseListener;

    public BillingService(ILogger<BaseBillingService> logger) : base(logger)
    {
        InitializeListeners();
    }

    private void InitializeListeners()
    {
        _stateListener = new BillingClientStateListener(this);
        _purchaseListener = new PurchasesUpdatedListener(this);
    }

    protected override async Task<bool> InitializePlatformAsync()
    {
        return await Task.Run(() =>
       {
           try
           {
               var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
               if (context == null)
               {
                   _logger.LogError("No current activity available for billing initialization");
                   return false;
               }

               if (_purchaseListener == null)
               {
                   _logger.LogError("Purchase listener not initialized");
                   return false;
               }

               var pendingPurchasesParams = PendingPurchasesParams.NewBuilder()
                   .EnableOneTimeProducts()
                   .Build();

               _billingClient = BillingClient.NewBuilder(context)
                   .SetListener(_purchaseListener)
                   .EnablePendingPurchases(pendingPurchasesParams)
                   .Build();

               _logger.LogInformation("Starting billing client connection...");
               if (_stateListener != null)
               {
                   _billingClient.StartConnection(_stateListener);
               }
               return true;
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Failed to initialize billing client");
               return false;
           }
       });
    }

    protected override async Task<List<Product>> GetPlatformProductsAsync(List<Product> baseProducts)
    {
        try
        {
            var updatedProducts = new List<Product>();

            // Create product list for querying
            var productList = new List<QueryProductDetailsParams.Product>();

            foreach (var product in baseProducts)
            {
                var queryProduct = QueryProductDetailsParams.Product.NewBuilder()
                    .SetProductId(product.Id)
                    .SetProductType(BillingClient.ProductType.Inapp)
                    .Build();

                productList.Add(queryProduct);
            }

            if (productList.Count == 0)
            {
                _logger.LogWarning("No products to query");
                return baseProducts;
            }

            var queryParams = QueryProductDetailsParams.NewBuilder()
                .SetProductList(productList)
                .Build();

            _logger.LogInformation("Querying product details for {Count} products", productList.Count);

            if (_billingClient == null)
            {
                _logger.LogError("Billing client is null when querying product details");
                return baseProducts;
            }

            var productResult = await _billingClient.QueryProductDetailsAsync(queryParams);

            if (productResult.Result.ResponseCode == BillingResponseCode.Ok)
            {
                _logger.LogInformation("Successfully retrieved {Count} product details", productResult.ProductDetails.Count);

                // Create a dictionary for quick lookup of ProductDetails by ID
                var productDetailsDict = productResult.ProductDetails.ToDictionary(pd => pd.ProductId, pd => pd);

                foreach (var baseProduct in baseProducts)
                {
                    var updatedProduct = new Product
                    {
                        Id = baseProduct.Id,
                        Name = baseProduct.Name,
                        Description = baseProduct.Description,
                        Price = baseProduct.Price,
                        PriceAmount = baseProduct.PriceAmount,
                        IsOwned = baseProduct.IsOwned,
                        ImageUrl = baseProduct.ImageUrl
                    };

                    // Update with actual product details from Google Play
                    if (productDetailsDict.TryGetValue(baseProduct.Id, out var productDetails))
                    {
                        updatedProduct.Name = productDetails.Name ?? baseProduct.Name;
                        updatedProduct.Description = productDetails.Description ?? baseProduct.Description;

                        var formattedPrice = GetFormattedPrice(productDetails);
                        if (!string.IsNullOrEmpty(formattedPrice))
                        {
                            updatedProduct.Price = formattedPrice;
                        }

                        var priceAmount = GetPriceAmount(productDetails);
                        if (priceAmount.HasValue)
                        {
                            updatedProduct.PriceAmount = priceAmount.Value;
                        }

                        // Check if this product is owned
                        updatedProduct.IsOwned = _ownedProducts.Contains(baseProduct.Id);

                        _logger.LogDebug("Updated product {ProductId}: {Name} - {Price}",
                            updatedProduct.Id, updatedProduct.Name, updatedProduct.Price);
                    }
                    else
                    {
                        _logger.LogWarning("Product details not found for {ProductId}, using base product info", baseProduct.Id);
                        // Still check if owned even if details not found
                        updatedProduct.IsOwned = _ownedProducts.Contains(baseProduct.Id);
                    }

                    updatedProducts.Add(updatedProduct);
                }

                return updatedProducts;
            }
            else
            {
                _logger.LogError("Failed to query product details: {ResponseCode} {DebugMessage}",
                    productResult.Result.ResponseCode, productResult.Result.DebugMessage);

                // Even if query fails, update ownership status
                foreach (var product in baseProducts)
                {
                    product.IsOwned = _ownedProducts.Contains(product.Id);
                }

                return baseProducts;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying platform products");

            // Update ownership status even on exception
            foreach (var product in baseProducts)
            {
                product.IsOwned = _ownedProducts.Contains(product.Id);
            }

            return baseProducts;
        }
    }

    protected override async Task<List<string>> GetPlatformPurchasedProductsAsync()
    {
        try
        {
            var purchasedProducts = new List<string>();
            var tcs = new TaskCompletionSource<List<string>>();

            var queryPurchasesParams = QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Inapp)
                .Build();

            var purchaseResponseListener = new PurchasesResponseListener((billingResult, purchases) =>
            {
                if (billingResult.ResponseCode == BillingResponseCode.Ok && purchases != null)
                {
                    foreach (var purchase in purchases)
                    {
                        if (purchase.PurchaseState == PurchaseState.Purchased)
                        {
                            purchasedProducts.AddRange(purchase.Products);
                        }
                    }
                }
                tcs.SetResult(purchasedProducts);
            });

            if (_billingClient != null)
            {
                _billingClient.QueryPurchases(queryPurchasesParams, purchaseResponseListener);
            }
            else
            {
                _logger.LogError("Billing client is null when querying purchases");
                tcs.SetResult(purchasedProducts);
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying purchased products");
            return new List<string>();
        }
    }

    protected override async Task<PurchaseResult> PurchasePlatformProductAsync(string productId)
    {
        try
        {
            var productList = QueryProductDetailsParams.Product.NewBuilder()
            .SetProductType("InApp")
            .SetProductId(productId)
            .Build();

            var activity = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
            {
                return await Task.FromResult(new PurchaseResult
                {
                    IsSuccess = false,
                    ProductId = productId,
                    ErrorMessage = "No current activity available"
                });
            }

            var productDetailsParams = QueryProductDetailsParams.NewBuilder().SetProductList(new[] { productList });

            if (_billingClient == null)
            {
                return await Task.FromResult(new PurchaseResult
                {
                    IsSuccess = false,
                    ProductId = productId,
                    ErrorMessage = "Billing client is not initialized"
                });
            }

            var productResult = await _billingClient.QueryProductDetailsAsync(productDetailsParams.Build());

            var skuDetails = productResult.ProductDetails.FirstOrDefault() ?? throw new ArgumentException($"{productId} does not exist");
            BillingFlowParams.ProductDetailsParams productDetailsParamsList;
            productDetailsParamsList = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(skuDetails)
            .Build();
            var billingFlowParams = BillingFlowParams.NewBuilder()
                .SetProductDetailsParamsList(new[] { productDetailsParamsList })
                .Build();

            var billingResult = _billingClient.LaunchBillingFlow(activity, billingFlowParams);

            if (billingResult.ResponseCode == BillingResponseCode.Ok)
            {
                return await Task.FromResult(new PurchaseResult
                {
                    IsSuccess = true,
                    ProductId = productId,
                    ErrorMessage = ""
                });
            }
            else
            {
                return await Task.FromResult(new PurchaseResult
                {
                    IsSuccess = false,
                    ProductId = productId,
                    ErrorMessage = $"Purchase failed: {billingResult.DebugMessage}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during purchase flow for product {ProductId}", productId);
            return await Task.FromResult(new PurchaseResult
            {
                IsSuccess = false,
                ProductId = productId,
                ErrorMessage = ex.Message
            });
        }
    }

    protected override async Task<bool> RestorePlatformPurchasesAsync()
    {
        try
        {
            var restoredPurchases = await QueryExistingPurchasesAsync();
            _logger.LogInformation("Restored {Count} purchases", restoredPurchases.Count);

            // Process each restored purchase
            foreach (var purchase in restoredPurchases)
            {
                ProcessPurchase(purchase);
            }

            return restoredPurchases.Count >= 0; // Return true even for 0 purchases (successful query)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring purchases");
            return false;
        }
    }

    #region Internal Event Handlers

    internal void OnBillingServiceDisconnected()
    {
        _logger.LogInformation("Billing service disconnected");

    }

    internal void OnBillingSetupFinished(AndroidBillingResult billingResult)
    {
        var responseCode = billingResult.ResponseCode;
        var debugMessage = billingResult.DebugMessage;

        _logger.LogInformation("Billing setup finished: {ResponseCode} {DebugMessage}", responseCode, debugMessage);

    }

    internal void OnPurchasesUpdated(AndroidBillingResult billingResult, IList<Purchase>? purchases)
    {
        if (billingResult.ResponseCode == BillingResponseCode.Ok && purchases != null)
        {
            _logger.LogInformation("Purchase updated: {Count} purchases", purchases.Count);

            foreach (var purchase in purchases)
            {
                ProcessPurchase(purchase);
            }
        }
        else if (billingResult.ResponseCode == BillingResponseCode.UserCancelled)
        {
            _logger.LogInformation("User canceled purchase");
        }
        else
        {
            _logger.LogError("Purchase failed: {ResponseCode} {DebugMessage}",
                billingResult.ResponseCode, billingResult.DebugMessage);
        }
    }

    #endregion

    #region Helper Methods
    private async Task<List<Purchase>> QueryExistingPurchasesAsync()
    {
        try
        {
            var queryParams = QueryPurchasesParams.NewBuilder()
                .SetProductType(BillingClient.ProductType.Inapp)
                .Build();

            if (_billingClient == null)
            {
                _logger.LogWarning("Billing client is null");
                return new List<Purchase>();
            }

            var purchasesResult = await _billingClient.QueryPurchasesAsync(queryParams);
            _logger.LogInformation("Successfully queried {Count} existing purchases", purchasesResult.Purchases.Count);
            return purchasesResult.Purchases.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying existing purchases");
            return new List<Purchase>();
        }
    }

    private void ProcessPurchase(Purchase purchase)
    {
        try
        {
            if (purchase.PurchaseState == PurchaseState.Purchased)
            {
                // Add to owned products
                foreach (var productId in purchase.Products)
                {
                    _ownedProducts.Add(productId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing purchase");
        }
    }

    private string? GetFormattedPrice(ProductDetails productDetails)
    {
        try
        {
            var offerDetails = productDetails.GetOneTimePurchaseOfferDetails();
            return offerDetails?.FormattedPrice;
        }
        catch
        {
            return null;
        }
    }

    private decimal? GetPriceAmount(ProductDetails productDetails)
    {
        try
        {
            var offerDetails = productDetails.GetOneTimePurchaseOfferDetails();
            var priceAmountMicros = offerDetails?.PriceAmountMicros;
            if (priceAmountMicros != null)
            {
                return priceAmountMicros.Value / 1_000_000m;
            }
        }
        catch
        {
            // Fallback to default
        }
        return null;
    }
    #endregion
}

#region Listener Classes

internal class BillingClientStateListener : Java.Lang.Object, IBillingClientStateListener
{
    private readonly BillingService _service;

    public BillingClientStateListener(BillingService service)
    {
        _service = service;
    }

    public void OnBillingServiceDisconnected()
    {
        _service.OnBillingServiceDisconnected();
    }

    public void OnBillingSetupFinished(AndroidBillingResult billingResult)
    {
        _service.OnBillingSetupFinished(billingResult);
    }
}

internal class PurchasesUpdatedListener : Java.Lang.Object, IPurchasesUpdatedListener
{
    private readonly BillingService _service;

    public PurchasesUpdatedListener(BillingService service)
    {
        _service = service;
    }

    public void OnPurchasesUpdated(AndroidBillingResult billingResult, IList<Purchase>? purchases)
    {
        _service.OnPurchasesUpdated(billingResult, purchases);
    }
}

internal class PurchasesResponseListener : Java.Lang.Object, IPurchasesResponseListener
{
    private readonly Action<AndroidBillingResult, IList<Purchase>> _onResponse;

    public PurchasesResponseListener(Action<AndroidBillingResult, IList<Purchase>> onResponse)
    {
        _onResponse = onResponse;
    }

    public void OnQueryPurchasesResponse(AndroidBillingResult billingResult, IList<Purchase> purchases)
    {
        _onResponse(billingResult, purchases);
    }
}

#endregion
`

2. BaseBillingService:
`
using BillingService.Models;
using BillingService.Services;
using Microsoft.Extensions.Logging;

namespace BillingService.Services;

public abstract class BaseBillingService : IBillingService
{
    protected readonly ILogger<BaseBillingService> _logger;
    protected bool _isInitialized;

    // Sample product definitions - shared across all platforms
    protected readonly List<Product> _sampleProducts = new()
    {
        new Product { Id = "Team_license", Name = "Team License", Description = "Team licenses offer the best value to get started", Price = "$300.99", PriceAmount = 400.99m, ImageUrl = "Team.png" },
        new Product { Id = "Global_license", Name = "Global License", Description = "Get Our Entire Product Line for Free", Price = "$600.99", PriceAmount = 700.99m, ImageUrl = "Global_license.png" },
        new Product { Id = "Unlimited_license", Name = "Unlimited License", Description = "Cover everyone for one low, annual fee", Price = "$700.99", PriceAmount = 600.99m, ImageUrl = "no_ads.png" }
    };

    // For demo purposes, simulate 2 owned items initially
    protected readonly HashSet<string> _ownedProducts = new() { "Team_license" };

    public bool IsInitialized => _isInitialized;

    protected BaseBillingService(ILogger<BaseBillingService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
            return true;

        try
        {
            var result = await InitializePlatformAsync();
            _isInitialized = result;
            _logger.LogInformation("Billing service initialized: {Success}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize billing service");
            return false;
        }
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        try
        {
            // Get platform-specific product details
            var products = await GetPlatformProductsAsync(_sampleProducts);

            // Mark owned products
            foreach (var product in products)
            {
                product.IsOwned = _ownedProducts.Contains(product.Id);
            }

            _logger.LogInformation("Retrieved {Count} products", products.Count);
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get products");
            return _sampleProducts.Select(p => new Product
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                PriceAmount = p.PriceAmount,
                ImageUrl = p.ImageUrl,
                IsOwned = _ownedProducts.Contains(p.Id)
            }).ToList();
        }
    }

    public async Task<PurchaseResult> PurchaseAsync(string productId)
    {
        _logger.LogInformation("Attempting to purchase product: {ProductId}", productId);

        try
        {
            var result = await PurchasePlatformProductAsync(productId);

            if (result.IsSuccess)
            {
                _ownedProducts.Add(productId);
                _logger.LogInformation("Purchase successful for product: {ProductId}", productId);
            }
            else
            {
                _logger.LogWarning("Purchase failed for product: {ProductId}, Error: {Error}", productId, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during purchase: {ProductId}", productId);
            return new PurchaseResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ProductId = productId
            };
        }
    }

    public async Task<List<string>> GetPurchasedProductsAsync()
    {
        try
        {
            var platformOwned = await GetPlatformPurchasedProductsAsync();

            // Merge with local owned products
            foreach (var product in platformOwned)
            {
                _ownedProducts.Add(product);
            }

            return _ownedProducts.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get purchased products");
            return _ownedProducts.ToList();
        }
    }
    public async Task<bool> RestorePurchasesAsync()
    {
        try
        {
            var success = await RestorePlatformPurchasesAsync();
            _logger.LogInformation("Purchases restored: {Success}", success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore purchases");
            return false;
        }
    }

    // Abstract methods to be implemented by platform-specific classes
    protected abstract Task<bool> InitializePlatformAsync();
    protected abstract Task<List<Product>> GetPlatformProductsAsync(List<Product> baseProducts);
    protected abstract Task<PurchaseResult> PurchasePlatformProductAsync(string productId);
    protected abstract Task<List<string>> GetPlatformPurchasedProductsAsync();
    protected abstract Task<bool> RestorePlatformPurchasesAsync();
}
`

3. IBillingService:
`
using BillingService.Models;

namespace BillingService.Services;

public interface IBillingService
{
    Task<bool> InitializeAsync();
    Task<List<Product>> GetProductsAsync();
    Task<PurchaseResult> PurchaseAsync(string productId);
    Task<List<string>> GetPurchasedProductsAsync();
    Task<bool> RestorePurchasesAsync();
    bool IsInitialized { get; }
}
`

4. MauiProgram from the codesample (dont use it! just look how the service is registered):

`
namespace BillingService;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureSyncfusionToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddTransient<IBillingService, Services.BillingService>();

		// Register ViewModels
		builder.Services.AddTransient<ProductsViewModel>();
		// Register Views
		builder.Services.AddTransient<ProductsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
`

5. Viewmodele:

`
using System.Collections.ObjectModel;
using System.Windows.Input;
using BillingService.Models;
using BillingService.Services;
using Microsoft.Extensions.Logging;

namespace BillingService.ViewModels;

public class ProductsViewModel : BaseViewModel
{
    private bool isLoading;
    private readonly IBillingService _billingService;
    private readonly ILogger<ProductsViewModel> _logger;

    public ObservableCollection<Product> Products { get; } = new();

    public ICommand RestorePurchasesCommand { get; }
    public ICommand LoadProductsCommand { get; }
    public ICommand PurchaseCommand { get; }

    public bool IsLoading
    {
        get => isLoading;
        set => SetProperty(ref isLoading, value);
    }

    public ProductsViewModel(IBillingService billingService, ILogger<ProductsViewModel> logger)
    {
        _billingService = billingService;
        _logger = logger;
        Title = "Subscriptions & Purchases";

        LoadProductsCommand = new Command(async () => await LoadProductsAsync());
        PurchaseCommand = new Command<Product>(async (product) => await PurchaseProductAsync(product));
        RestorePurchasesCommand = new Command(async () => await RestorePurchasesAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            if (!_billingService.IsInitialized)
            {
                IsLoading = true;
                await Task.Delay(2000);
                var initialized = await _billingService.InitializeAsync();

                if (!initialized)
                {
                    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Failed to initialize billing service", "OK");
                    return;
                }
            }

            var products = await _billingService.GetProductsAsync();

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            _logger.LogInformation("Loaded {Count} products", products.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading products");
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Failed to load products", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PurchaseProductAsync(Product product)
    {
        if (product == null || product.IsOwned)
            return;

        try
        {
            var confirm = await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
                "Confirm Purchase",
                $"Purchase {product.Name} for {product.Price}?",
                "Yes",
                "No");

            if (!confirm)
                return;

            // Show loading indicator during purchase
            IsLoading = true;

            var result = await _billingService.PurchaseAsync(product.Id);

            if (result.IsSuccess)
            {
                product.IsOwned = true;
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Success", $"Successfully purchased {product.Name}!", "OK");
                _logger.LogInformation("Purchase successful: {ProductId}", product.Id);
            }
            else
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Purchase Failed", result.ErrorMessage, "OK");
                _logger.LogWarning("Purchase failed: {ProductId}, Error: {Error}", product.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during purchase: {ProductId}", product.Id);
            await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "An error occurred during purchase", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RestorePurchasesAsync()
    {
        try
        {
            IsLoading = true;

            var success = await _billingService.RestorePurchasesAsync();

            if (success)
            {
                await (Application.Current?.Windows[0].Page?.DisplayAlertAsync("Success", "Purchases restored successfully!", "OK") ?? Task.CompletedTask);
                _logger.LogInformation("Purchases restored successfully");
            }
            else
            {
                await (Application.Current?.Windows[0].Page?.DisplayAlertAsync("Error", "Failed to restore purchases", "OK") ?? Task.CompletedTask);
                _logger.LogWarning("Failed to restore purchases");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring purchases");
            await (Application.Current?.Windows[0].Page?.DisplayAlertAsync("Error", "An error occurred while restoring purchases", "OK") ?? Task.CompletedTask);
        }
        finally
        {
            IsLoading = false;
        }
    }
}`

`
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BillingService.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action? onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        onChanged?.Invoke();
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
`
6. Convertety:
`
using System.Globalization;

namespace BillingService.Converters;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            var options = paramString.Split('|');
            if (options.Length == 2)
            {
                return boolValue ? options[0] : options[1];
            }
        }
        return "Unknown";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            var options = paramString.Split('|');
            if (options.Length == 2)
            {
                var colorString = boolValue ? options[0] : options[1];

                // Handle static resource references
                if (colorString.StartsWith("{StaticResource ") && colorString.EndsWith("}"))
                {
                    var resourceKey = colorString.Substring(16, colorString.Length - 17);
                    if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true)
                    {
                        if (resource is Color color)
                            return color;
                        if (resource is SolidColorBrush brush)
                            return brush.Color;
                    }
                    // Fallback for common resource keys
                    return resourceKey switch
                    {
                        "Primary" => Color.FromArgb("#512BD4"),
                        "Secondary" => Color.FromArgb("#DFD8F7"),
                        _ => Colors.Gray
                    };
                }

                // Handle direct color names and hex values
                try
                {
                    if (colorString.Equals("LightGray", StringComparison.OrdinalIgnoreCase))
                        return Colors.LightGray;
                    
                    return Color.FromArgb(colorString);
                }
                catch
                {
                    return Colors.Gray;
                }
            }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }
}
`


7. Dokumentacja Google dla Android Billing:
`
System rozliczeniowy Google Play to usługa umożliwiająca sprzedaż produktów i treści cyfrowych w aplikacji na Androida. Możesz dzięki niej zarabiać na jednorazowych zakupach lub oferować subskrypcje swoich usług. Google Play oferuje pełny zestaw interfejsów API do integracji z aplikacją na Androida i serwerem backendu, które zapewniają użytkownikom znajome i bezpieczne zakupy w Google Play.
Uwaga: system rozliczeniowy Google Play jest przeznaczony tylko do produktów cyfrowych. W przypadku produktów fizycznych i usług lub innych treści niedigitalnych zapoznaj się z pakietem Google Pay SDK.
Architektura integracji

W tej sekcji przedstawiamy różne moduły funkcjonalne, które możesz utworzyć, oraz interfejsy API i biblioteki, które ułatwią Ci ten proces.
Aplikacja na Androida współpracuje z backendem dewelopera i backendem Google Play (za pomocą Usług Google Play).
Rysunek 1. Schemat typowej integracji z usługą Płatności w Google Play.

System rozliczeniowy Google Play możesz zintegrować z aplikacją na Androida za pomocą biblioteki Płatności w Google Play. Ta biblioteka umożliwia komunikację z warstwą Usług Google Play, która udostępnia zlokalizowaną ofertę produktów dostępną dla każdego użytkownika w Twojej aplikacji, a także metody obsługi innych niezbędnych działań użytkownika, takich jak uruchamianie procesu zakupu i obsługa jego wyniku.

Musisz też zintegrować system rozliczeniowy Google Play z backendem serwera, aby utworzyć niezbędne procesy deweloperskie. Jest to niezbędne, aby zagwarantować wydajne i bezpieczne zarządzanie zakupami oraz uprawnieniami na wielu platformach. Możesz utworzyć tę integrację za pomocą interfejsu Subscriptions and in-app purchases API udostępnianego przez interfejs Google Play Developer API. Integracja backendu korzysta też z niektórych narzędzi platformy Google Cloud.
Rysunek 2. interfejsy API i usługi udostępniane przez interfejs Google Play Developer API;
Terminologia

W tej sekcji znajdziesz listę i opis najważniejszych technologii i koncepcji, z którymi możesz się spotkać podczas integrowania systemu rozliczeniowego Google Play z aplikacją. Korzystaj z tej listy podczas wdrażania integracji.
Technologie

    Google Play. Sklep internetowy, w którym użytkownicy mogą pobierać aplikacje i inne produkty cyfrowe.
    Konsoli Google Play. Platforma, która udostępnia interfejs umożliwiający publikowanie aplikacji w Google Play. Konsola Google Play zawiera też szczegółowe informacje o aplikacji, w tym o produktach i treściach, które sprzedajesz w Google Play.
    Google Cloud Console. Platforma, która zarządza interfejsami API backendu, takimi jak interfejs Google Play Developer API.
    Biblioteka Płatności w Google Play Interfejs API, którego możesz użyć do zintegrowania systemu rozliczeniowego Google Play z aplikacją.
    Interfejs Google Play Developer API. Interfejs API REST, którego możesz używać do programowego wykonywania zadań związanych z publikowaniem aplikacji i zarządzaniem nimi.
    Cloud Pub/Sub. Usługa do przesyłania wiadomości w czasie rzeczywistym w pełni zarządzana, która umożliwia wysyłanie i odbieranie wiadomości między niezależnymi aplikacjami. Google Play używa Cloud Pub/Sub do dostarczania powiadomień w czasie rzeczywistym dla deweloperów. Aby korzystać z Cloud Pub/Sub, musisz mieć projekt na Google Cloud Platform (GCP) z włączonym interfejsem Cloud Pub/Sub API. Jeśli nie znasz GCP i Cloud Pub/Sub, zapoznaj się z krótkim wprowadzeniem.
    Powiadomienia w czasie rzeczywistym dla deweloperów Mechanizm, który umożliwia monitorowanie zmian stanu subskrypcji zarządzanych przez Google Play w czasie rzeczywistym za pomocą Cloud Pub/Sub.
    Bezpieczny serwer backendu. W ramach integracji systemu rozliczeniowego Google Play z aplikacją zdecydowanie zalecamy używanie bezpiecznego serwera backendu do implementowania zadań związanych z rozliczeniami, takich jak weryfikacja zakupu, funkcje związane z subskrypcją i obsługa powiadomień dla deweloperów w czasie rzeczywistym.
    Aplikacja Sklep Google Play – aplikacja, która zarządza wszystkimi operacjami związanymi z Google Play. Wszystkie żądania wysyłane przez aplikację są obsługiwane przez aplikację Sklep Google Play.

Pojęcia

    Flow Schemat przedstawia typowe kroki związane z zadaniem dotyczącym rozliczeń. Na przykład ścieżka zakupu opisuje czynności, które wykonuje użytkownik, gdy kupuje Twój produkt. Proces subskrypcji może pokazywać, jak subskrypcja przechodzi między stanami.
    Uprawnienie Gdy użytkownik kupi produkt w aplikacji, uzyskuje do niego prawo. W przypadku produktów kupowanych raz oznacza to, że użytkownik powinien mieć stały dostęp do produktu. W przypadku subskrypcji oznacza to, że użytkownik powinien mieć dostęp do usługi, dopóki subskrypcja jest aktywna.
    Identyfikator produktu Identyfikator konkretnego typu produktu.
    Token zakupu Ciąg znaków reprezentujący uprawnienia kupującego do produktu w Google Play. Oznacza to, że użytkownik Google zapłacił za konkretny produkt.
    Identyfikator zamówienia Ciąg znaków reprezentujący transakcję finansową w Google Play. Identyfikator zamówienia jest tworzony za każdym razem, gdy następuje transakcja finansowa. Ten ciąg znaków jest dołączany do paragonu wysyłanego e-mailem do kupującego. Identyfikator zamówienia możesz wykorzystać do zarządzania zwrotami środków w sekcji Zarządzanie zamówieniami w Konsoli Google Play. Identyfikatory zamówień są też używane w raportach sprzedaży i wypłat.

`


Jak obecnie działa ogarnianie subksrypcji w Foodbookapp:
`
Sprawdzam lokalny przepływ subskrypcji i integrację Supabase: najpierw serwisy w Services/Subscription, potem miejsca, gdzie ProfilePage.xaml je wywołuje.

Searched for files matching `**/FoodbookApp.App/Services/Subscription/**/*.cs`, 3 matches

Searched for regex `Supabase|subscription|plan|billing` (`**/FoodbookApp.App/**/*.cs`), 200 results

Read memory [](file:///c%3A/Users/Takis/AppData/Roaming/Code/User/workspaceStorage/98b2669b4a4682902717ca8801133d90/GitHub.copilot-chat/memory-tool/memories/repo/subscription-profile-ui.md)

Read memory [](file:///c%3A/Users/Takis/AppData/Roaming/Code/User/workspaceStorage/98b2669b4a4682902717ca8801133d90/GitHub.copilot-chat/memory-tool/memories/repo/recipe-reload-regression.md)

Mam już punkt zaczepienia: UI tylko pokazuje stan, a właściwy zapis do Supabase prawdopodobnie siedzi w FeatureAccessService albo AccountService. Teraz czytam tylko ten tor, żeby opisać faktyczny przepływ, nie domysły.

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/Subscription/SupabaseEdgeSubscriptionManagementService.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/Subscription/PaymentProviderSubscriptionManagementService.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/FeatureAccessService.cs#1-1), lines 1 to 380

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/Subscription/MockSubscriptionManagementService.cs#1-1), lines 1 to 260

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Views/ProfilePage.xaml.cs#1-1), lines 1 to 420

Znalazłem główny konsument UI. Teraz domykam brakujące ogniwa: co dokładnie zapisuje MockSubscriptionManagementService do Supabase i jak ten stan wraca do UI.

Searched for regex `interface ISubscriptionManagementService|record SubscriptionActionResult|enum SubscriptionPlan|class SubscriptionPendingOperation|class SubscriptionOperationEntry|UpsertSupabaseUserPlanAsync|SaveSnapshotAsync|GetSnapshotAsync|ApplyPlan\(` (`**/FoodbookApp.App/**/*.cs`), 24 results

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Views/ProfilePage.xaml.cs#420-420), lines 420 to 900

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/Subscription/MockSubscriptionManagementService.cs#260-260), lines 260 to 520

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Interfaces/IFeatureAccessService.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/MauiProgram.cs#60-60), lines 60 to 180

Mam już kluczowy fakt: UI nie wysyła nic bezpośrednio do Supabase. Zapis robi serwis subskrypcji, a ProfilePage.xaml tylko woła event handlers. Teraz sprawdzam, skąd bierze aktualny plan i gdzie ląduje snapshot/status.

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Views/ProfilePage.xaml.cs#900-900), lines 900 to 1220

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Interfaces/ISubscriptionManagementService.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Models/SubscriptionPlan.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Models/SubscriptionPendingOperation.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Models/SubscriptionOperationEntry.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/AppEvents.cs#1-1), lines 1 to 120

Widzę już sedno: przy zmianie planu serwis robi dwa zapisy równolegle w dwóch warstwach stanu. Dopytuję końcówkę MockSubscriptionManagementService, żeby opisać dokładnie co idzie do Supabase, a co zostaje lokalnie.

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Services/Subscription/MockSubscriptionManagementService.cs#360-360), lines 360 to 523

Searched for regex `SaveSubscriptionPlanChoice\(|GetSubscriptionPlanChoice\(|PlanChoiceToString\(|ParsePlanChoice\(` (`**/FoodbookApp.App/Views/ProfilePage.xaml.cs`), 11 results

Searched for regex `CurrentPlanNameLabel|CurrentPlanStatusLabel|PendingSubscription|SubscriptionPending|SwitchToMonthlyPlanButton|CancelSubscriptionButton` (`**/FoodbookApp.App/Views/ProfilePage.xaml`), 13 results

Jest już dość danych. Ostatni krok: potwierdzam, że “status” w Twoim UI to nie osobny zapis do Supabase, tylko mieszanka lokalnego `SubscriptionOperationStatus` i odczytu flag premium z tabeli `users`.

Searched for regex `class SubscriptionActionResult|enum SubscriptionActionState` (`**/FoodbookApp.App/**/*.cs`), 2 results

Searched for regex `IsPremium|PremiumFrom|PremiumTo|UserType|PlanCountThisMonth|AdUnlocksJson` (`**/FoodbookApp.App/Services/FeatureAccessService.cs`), 28 results

Searched for regex `RegisterScoped<ISubscriptionManagementService|MockSubscriptionManagementService|SupabaseEdgeSubscriptionManagementService|PaymentProviderSubscriptionManagementService` (`**/FoodbookApp.App/MauiProgram.cs`), 1 result

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Models/SubscriptionActionResult.cs)

Read [](file:///c%3A/Users/Takis/source/repos/FoodbookApp/FoodbookApp.App/Models/SubscriptionActionState.cs)

Obecnie nie wysyłasz statusu planu z XAML bezpośrednio do Supabase. ProfilePage.xaml tylko podłącza przyciski do handlerów, a realny zapis robi serwis z DI: MauiProgram.cs rejestruje ISubscriptionManagementService jako MockSubscriptionManagementService.

Jak to idzie dziś:
- Klik w Change plan / Cancel / Resume w ProfilePage.xaml.cs woła ChangePlanAsync, CancelSubscriptionAsync albo ResumePendingOperationAsync.
- MockSubscriptionManagementService najpierw zapisuje operację lokalnie w SubscriptionOperations jako Pending / InProgress / Failed / Abandoned / Completed. To jest stan procesu, nie stan Supabase.
- Dopiero potem robi upsert do Supabase w tabeli users. Payload buduje w BuildSupabaseUserPayload: Id, Email, IsPremium, PremiumFrom, PremiumTo, UserType. PremiumMonthly i PremiumYearly ustawiają IsPremium = true oraz daty ważności; Free daje IsPremium = false i null w datach. Zobacz MockSubscriptionManagementService.cs.
- Jeśli rekord users nie istnieje, robi INSERT. Jeśli istnieje, robi UPDATE po SupabaseUserId.
- Po sukcesie ProfilePage zapisuje wybrany plan do Preferences i odpala RefreshAccessAsync. FeatureAccessService.cs potem czyta users z Supabase i z IsPremium + PremiumTo wylicza IsPremiumUser oraz uprawnienia premium. To odświeża UI i limity.
- PendingSubscriptionCard w UI pokazuje lokalny stan operacji z SubscriptionPendingOperation, nie osobny status z Supabase.

Dwa pozostałe serwisy w folderze, SupabaseEdgeSubscriptionManagementService i PaymentProviderSubscriptionManagementService, są dziś tylko stubami z NotSupportedException. Nie są podpięte przez DI.
`

Ocena architektury:
`
Twój obecny system jest już zrobiony bardzo sensownie pod przyszłe przejście na prawdziwy billing. Serio.
Ty de facto zbudowałeś już warstwę domenową subskrypcji, a Google Billing będzie tylko providerem płatności.

I to jest bardzo dobra architektura.

Największa różnica między Twoim mockiem a prawdziwym Google Billing polega na tym, kto jest „source of truth”.

Dzisiaj source of truth jest Twój kod + Supabase.

Przy Google Billing source of truth staje się Google Play.

I teraz pokażę Ci dokładnie co się zmienia.

---

# Jak działa Twój obecny mock

Na bazie Twoich serwisów:

* `MockSubscriptionManagementService`
* `FeatureAccessService`
* `ProfilePage.xaml.cs`
* `ISubscriptionManagementService`

flow wygląda mniej więcej tak: 

```text
User kliknął "Kup Premium"
↓
MockSubscriptionManagementService.ChangePlanAsync()
↓
lokalny status operacji (Pending / Completed)
↓
upsert do Supabase.users
↓
IsPremium = true
PremiumTo = date
↓
FeatureAccessService czyta users
↓
UI pokazuje Premium
```

Czyli:

## Twój backend SAM nadaje premium

To bardzo ważne.

Google jeszcze tutaj nie istnieje.

Twoja aplikacja sama decyduje:

```csharp
IsPremium = true
PremiumTo = DateTime.UtcNow.AddMonths(1)
```

I zapisuje to do Supabase.

---

# Co robi Google Billing inaczej

W Google Billing:

```text
Google Play decyduje czy user ma premium
```

Aplikacja NIE może sobie sama ufać.

I to jest fundamentalna różnica.

---

# Jak wygląda prawdziwy flow Google Billing

To wygląda bardziej tak:

```text
User kliknął "Kup Premium"
↓
Google Play purchase screen
↓
Google pobiera pieniądze
↓
Google zwraca PurchaseToken
↓
Aplikacja wysyła PurchaseToken do backendu
↓
Backend pyta Google API:
"Czy ten zakup jest prawdziwy?"
↓
Google odpowiada:
TAK / NIE
↓
Backend dopiero ustawia:
IsPremium = true
↓
Supabase update
↓
UI refresh
```

---

# Co w Twojej architekturze jest już bardzo dobre

To:

```text
UI
↓
ISubscriptionManagementService
↓
FeatureAccessService
↓
Supabase
```

Bo Ty możesz podmienić:

```text
MockSubscriptionManagementService
```

na:

```text
GooglePlaySubscriptionManagementService
```

i reszta appki praktycznie zostaje.

To jest ogromny plus.

---

# Co dziś robi mock, a czego NIE robi Google sample

Microsoft sample z `IBillingService` jest dużo bardziej low-level. 

On pokazuje:

* connect do billing client
* query produktów
* purchase flow
* restore purchases

Ale NIE pokazuje prawdziwej architektury backendowej.

To jest bardzo ważne.

Sample:

```text
BillingClient
↓
Purchase successful
↓
_local state_
```

Twój system:

```text
BillingClient
↓
backend
↓
Supabase
↓
feature access
```

jest dojrzalszy architektonicznie.

---

# Największy problem Microsoft sample

Tutaj:

```csharp
_ownedProducts.Add(productId);
```

To jest tylko lokalny stan pamięci aplikacji.

Czyli po reinstall:

* bye
* wszystko znika

To jest demo.

---

# Jak powinieneś to zrobić w FoodBookApp

Twoja przyszła implementacja powinna wyglądać tak:

```text
GoogleBillingService
↓
PurchaseToken
↓
ASP.NET Core API
↓
Google Play Developer API verify
↓
Supabase update
↓
FeatureAccessService.RefreshAccessAsync()
```

---

# Co dokładnie będzie się zmieniało w Twoim kodzie

Dzisiaj:

```csharp
await UpsertSupabaseUserPlanAsync(plan);
```

jutro:

```csharp
var purchase = await _billingService.PurchaseAsync(productId);

await _subscriptionBackendApi.VerifyPurchaseAsync(
    purchase.PurchaseToken);
```

i dopiero backend:

```csharp
IsPremium = true
```

---

# Najważniejszy element: PurchaseToken

Google zwraca token:

```text
purchaseToken
```

To jest dowód zakupu.

Backend musi:

1. dostać token
2. zapytać Google API
3. sprawdzić:

   * czy subscription aktywna
   * expiryTime
   * autoRenewing
   * canceled
   * acknowledged

Google udostępnia do tego API. ([Google for Developers][1])

---

# I tutaj dochodzi ACKNOWLEDGE

To czego jeszcze nie masz.

Google wymaga:

```text
acknowledgePurchase()
```

albo user dostanie refund po kilku dniach. ([Google for Developers][1])

W sample Microsoftu tego praktycznie brakuje jako pełnego flow.

Masz tylko:

```csharp
LaunchBillingFlow()
```

ale production flow powinien być:

```text
Purchase success
↓
send token to backend
↓
verify
↓
grant entitlement
↓
acknowledge purchase
```

---

# Twój obecny mock odpowiada mniej więcej temu etapowi:

```text
grant entitlement
```

Czyli:

```csharp
IsPremium = true
```

Ale bez:

* prawdziwego zakupu
* verify
* acknowledge
* lifecycle renewals

---

# Lifecycle subskrypcji

I tutaj zaczyna się prawdziwe SaaS/mobile dev.

Bo subskrypcja nie jest:
„kupił i koniec”.

Google wysyła eventy:

* renewed
* canceled
* expired
* grace period
* payment failed

I backend musi reagować. ([Android Developers][2])

Czyli:

```text
Google webhook
↓
Twój backend
↓
Update Supabase
↓
Premium off
```

---

# Co bym zrobił u Ciebie

Ja bym zostawił dokładnie:

* `FeatureAccessService`
* `ISubscriptionManagementService`
* cały flow UI

i wymienił tylko implementation layer.

Czyli:

DZIŚ:

```text
MockSubscriptionManagementService
```

JUTRO:

```text
GooglePlaySubscriptionManagementService
```

który:

* korzysta z `IBillingService`
* pobiera token
* gada z backendem
* backend gada z Google API
* backend updateuje Supabase

To jest bardzo clean architecture.

---

# Największy plus Twojego projektu

Ty już myślisz backendowo.

Większość ludzi robi:

```text
if(purchased)
 premium = true;
```

u Ciebie już:

* jest warstwa domenowa
* są pending operations
* jest snapshot state
* jest centralny feature access
* jest Supabase jako persistence

To się naprawdę dobrze skaluje pod production billing.

[1]: https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.subscriptions/acknowledge?utm_source=chatgpt.com "Method: purchases.subscriptions.acknowledge  |  Google Play Developer API  |  Google for Developers"
[2]: https://developer.android.com/google/play/billing/lifecycle/subscriptions?utm_source=chatgpt.com "Subscription lifecycle  |  Play Billing  |  Android Developers"

`