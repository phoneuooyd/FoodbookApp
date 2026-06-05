using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
using ZXing.Net.Maui;

namespace Foodbook.Views;

public partial class BarcodeScannerPage : ContentPage
{
    private readonly IOpenFoodFactsService _openFoodFactsService;
    private OpenFoodFactsProductResult? _detectedProduct;
    private bool _isProcessing;
    private string _lastDetectedCode = string.Empty;

    public static OpenFoodFactsProductResult? LastResult { get; set; }

    private static string R(string key, string fallback)
        => IngredientFormPageResources.ResourceManager.GetString(key, IngredientFormPageResources.Culture) ?? fallback;

    public BarcodeScannerPage(IOpenFoodFactsService openFoodFactsService)
    {
        InitializeComponent();
        _openFoodFactsService = openFoodFactsService;

        ScannerView.Options = new ZXing.Net.Maui.BarcodeReaderOptions
        {
            Formats = ZXing.Net.Maui.BarcodeFormats.OneDimensional,
            AutoRotate = true,
            Multiple = false
        };
        ScannerView.CameraLocation = ZXing.Net.Maui.CameraLocation.Rear;
        ScannerView.BarcodesDetected += OnBarcodesDetected;

        StatusLabel.Text = R("ScannerStatusWaiting", "Waiting for barcode...");
        AddProductButton.Text = R("ScannerAddButton", "Add ingredient from barcode");
        CancelButton.Text = ButtonResources.Cancel;
        Title = R("ScannerTitle", "Scan Barcode");
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing || _detectedProduct != null)
            return;

        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrWhiteSpace(result.Value))
            return;

        var code = result.Value.Trim();
        if (code == _lastDetectedCode)
            return;

        _lastDetectedCode = code;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            DetectedCodeLabel.Text = code;
            StatusLabel.Text = string.Format(R("ScannerStatusDetected", "Barcode detected: {0}"), code);
            _detectedProduct = new OpenFoodFactsProductResult { Barcode = code };
            AddProductButton.IsEnabled = true;
        });
    }

    private async void OnAddProductClicked(object? sender, EventArgs e)
    {
        if (_detectedProduct == null || string.IsNullOrWhiteSpace(_detectedProduct.Barcode))
            return;

        _isProcessing = true;
        AddProductButton.IsEnabled = false;

        StatusLabel.Text = R("ScannerStatusFetching", "Fetching product data...");

        try
        {
            var product = await _openFoodFactsService.GetProductByBarcodeAsync(_detectedProduct.Barcode);
            if (product != null && (!string.IsNullOrWhiteSpace(product.Name) || product.Calories100g >= 0))
            {
                LastResult = product;
                await Navigation.PopModalAsync();
            }
            else
            {
                StatusLabel.Text = R("ScannerStatusNotFound", "Product not found in OpenFoodFacts");
                _isProcessing = false;
            }
        }
        catch
        {
            StatusLabel.Text = R("ScannerFetchError", "Error fetching product data");
            _isProcessing = false;
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        LastResult = null;
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ScannerView.BarcodesDetected -= OnBarcodesDetected;
    }
}
