using Foodbook.Models;
using FoodbookApp.Interfaces;
using Newtonsoft.Json.Linq;

namespace Foodbook.Services;

public class OpenFoodFactsService : IOpenFoodFactsService
{
    private readonly HttpClient _httpClient;

    public OpenFoodFactsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OpenFoodFactsProductResult?> GetProductByBarcodeAsync(string barcode)
    {
        var url = $"https://world.openfoodfacts.net/api/v2/product/{Uri.EscapeDataString(barcode)}";
        System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] GET {url}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.TryParseAdd("FoodbookApp/1.0.1");
            var response = await _httpClient.SendAsync(request);

            var statusCode = (int)response.StatusCode;
            System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] Response {statusCode} for barcode={barcode}");

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] HTTP ERROR {statusCode} for barcode={barcode} — {(statusCode == 404 ? "Product not found" : statusCode == 401 ? "Unauthorized" : statusCode == 500 ? "Server error" : statusCode == 503 ? "Service unavailable" : $"Unexpected error")}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] Body length={content.Length} for barcode={barcode}");
            var root = JObject.Parse(content);

            var statusRaw = root["status"]?.ToString();
            int.TryParse(statusRaw, out var apiStatus);
            System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] API status_raw='{statusRaw}' parsed={apiStatus} for barcode={barcode}");

            if (apiStatus != 1)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] API status={apiStatus} — product not found (barcode={barcode})");
                return null;
            }

            var product = root["product"] as JObject;
            if (product == null)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] product object is null (barcode={barcode})");
                return null;
            }

            var productName = product["product_name"]?.Value<string>() ?? string.Empty;
            var nutriments = product["nutriments"] as JObject;

            if (nutriments == null)
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] nutriments is null (barcode={barcode}, name={productName})");
            else
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] nutriments found (barcode={barcode}, name={productName})");

            static double TryGet(JObject? src, string key)
            {
                if (src == null) return -1;
                var token = src[key];
                if (token == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] key '{key}' not found in nutriments");
                    return -1;
                }
                try { return token.Value<double>(); }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] failed to parse '{key}' value='{token}'");
                    return -1;
                }
            }

            var result = new OpenFoodFactsProductResult
            {
                Barcode = barcode,
                Name = productName,
                Calories100g = TryGet(nutriments, "energy-kcal_100g"),
                Protein100g = TryGet(nutriments, "proteins_100g"),
                Fat100g = TryGet(nutriments, "fat_100g"),
                Carbs100g = TryGet(nutriments, "carbohydrates_100g")
            };

            bool hasValidData = result.Calories100g >= 0 || result.Protein100g >= 0 || result.Fat100g >= 0 || result.Carbs100g >= 0;
            System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] result: name='{result.Name}', calories={result.Calories100g}, protein={result.Protein100g}, fat={result.Fat100g}, carbs={result.Carbs100g}, hasValidData={hasValidData} (barcode={barcode})");

            if (string.IsNullOrWhiteSpace(result.Name) && !hasValidData)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] no name and no valid nutrition — returning null (barcode={barcode})");
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenFoodFactsService] EXCEPTION for barcode={barcode}: {ex}");
            return null;
        }
    }
}
