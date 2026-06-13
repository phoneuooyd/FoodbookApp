using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace Foodbook.Views;

public partial class BarcodeScannerPage : ContentPage
{
    private readonly IOpenFoodFactsService _openFoodFactsService;
    private OpenFoodFactsProductResult? _detectedProduct;
    private bool _isProcessing;
    private string _lastDetectedCode = string.Empty;
    private bool _isTorchOn;
    private List<CameraInfo> _availableCameras = new();
    private int _currentCameraIndex;

    public static OpenFoodFactsProductResult? LastResult { get; set; }
    public Task<OpenFoodFactsProductResult?> ScannerTask => _tcs.Task;
    private readonly TaskCompletionSource<OpenFoodFactsProductResult?> _tcs = new();

    private static string R(string key, string fallback)
        => IngredientFormPageResources.ResourceManager.GetString(key, IngredientFormPageResources.Culture) ?? fallback;

    public BarcodeScannerPage(IOpenFoodFactsService openFoodFactsService)
    {
        InitializeComponent();
        _openFoodFactsService = openFoodFactsService;

        ScannerView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.Ean13 | BarcodeFormat.Ean8 | BarcodeFormat.UpcA,
            AutoRotate = true,
            Multiple = false
        };
        ScannerView.CameraLocation = CameraLocation.Rear;
        ScannerView.BarcodesDetected += OnBarcodesDetected;

        StatusLabel.Text = R("ScannerStatusWaiting", "Waiting for barcode...");
        AddProductButton.Text = R("ScannerAddButton", "Add ingredient from barcode");
        CancelButton.Text = ButtonResources.Cancel;
        Title = R("ScannerTitle", "Scan Barcode");

        _ = LogCameraDiagnosticsAsync();
    }

    private async Task LogCameraDiagnosticsAsync()
    {
        try
        {
            _availableCameras = (await ScannerView.GetAvailableCameras()).ToList();
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Available cameras ({_availableCameras.Count}):");
            for (int i = 0; i < _availableCameras.Count; i++)
            {
                var cam = _availableCameras[i];
                System.Diagnostics.Debug.WriteLine($"  [{i}] {cam.Name} | DeviceId={cam.DeviceId} | Location={cam.Location}");
            }

            if (_availableCameras.Count > 0)
            {
                var rear = _availableCameras.FirstOrDefault(c => c.Location == CameraLocation.Rear);
                _currentCameraIndex = rear != null ? _availableCameras.IndexOf(rear) : 0;
                ScannerView.SelectedCamera = _availableCameras[_currentCameraIndex];
                System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Using camera [{_currentCameraIndex}]: {_availableCameras[_currentCameraIndex].Name}");
            }

            MainThread.BeginInvokeOnMainThread(() =>
                SwitchCameraButton.IsVisible = _availableCameras.Count > 1);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] GetAvailableCameras failed: {ex.Message}");
        }
    }

    private static bool IsValidEan(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var digits = code.Where(char.IsDigit).ToArray();

        if (digits.Length != 13 && digits.Length != 8)
            return false;

        if (digits.Length == 13)
        {
            int sum = 0;
            for (int i = 0; i < 12; i++)
                sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
            int check = (10 - (sum % 10)) % 10;
            return check == (digits[12] - '0');
        }

        return true;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing || _detectedProduct != null)
            return;

        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrWhiteSpace(result.Value))
            return;

        var code = result.Value.Trim();
        var format = result.Format;
        var timestamp = DateTime.UtcNow;

        System.Diagnostics.Debug.WriteLine(
            $"[BarcodeScanner] DETECTED | Format={format} Value={code} Time={timestamp:HH:mm:ss.fff}");

        if (!IsValidEan(code))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BarcodeScanner] REJECTED — invalid EAN checksum: {code}");
            return;
        }

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
            System.Diagnostics.Debug.WriteLine(
                $"[BarcodeScanner] FETCHING barcode={_detectedProduct.Barcode} at {DateTime.UtcNow:HH:mm:ss.fff}");

            var product = await _openFoodFactsService.GetProductByBarcodeAsync(_detectedProduct.Barcode);

            System.Diagnostics.Debug.WriteLine(
                $"[BarcodeScanner] RESULT for {_detectedProduct.Barcode}: {(product != null ? $"found '{product.Name}'" : "not found")}");

            if (product != null && (!string.IsNullOrWhiteSpace(product.Name) || product.Calories100g >= 0))
            {
                LastResult = product;
                _tcs.TrySetResult(product);
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
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] FETCH EXCEPTION for {_detectedProduct.Barcode}");
            StatusLabel.Text = R("ScannerFetchError", "Error fetching product data");
            _isProcessing = false;
        }
    }

    private async void OnTorchClicked(object? sender, EventArgs e)
    {
        try
        {
            _isTorchOn = !_isTorchOn;
            ScannerView.IsTorchOn = _isTorchOn;
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Torch={( _isTorchOn ? "ON" : "OFF" )}");
        }
        catch (Exception ex)
        {
            _isTorchOn = false;
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Torch failed: {ex.Message}");
        }
    }

    private async void OnSwitchCameraClicked(object? sender, EventArgs e)
    {
        if (_availableCameras.Count < 2)
            return;

        try
        {
            _currentCameraIndex = (_currentCameraIndex + 1) % _availableCameras.Count;
            ScannerView.SelectedCamera = _availableCameras[_currentCameraIndex];
            var cam = _availableCameras[_currentCameraIndex];

            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Switched to camera [{_currentCameraIndex}]: {cam.Name} (Location={cam.Location})");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = string.Format(R("ScannerCameraSwitched", "Camera: {0}"), cam.Name);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarcodeScanner] Camera switch failed: {ex.Message}");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        LastResult = null;
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("[BarcodeScanner] OnAppearing — camera should start");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        System.Diagnostics.Debug.WriteLine("[BarcodeScanner] OnDisappearing — camera shutting down");
        _tcs.TrySetResult(null);
        ScannerView.BarcodesDetected -= OnBarcodesDetected;
        try { ScannerView.IsTorchOn = false; } catch { }
    }
}
