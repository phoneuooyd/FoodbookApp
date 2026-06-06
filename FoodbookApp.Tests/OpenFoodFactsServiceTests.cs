using FluentAssertions;
using Foodbook.Models;
using Foodbook.Services;
using System.Net;
using System.Text;


namespace FoodbookApp.Tests
{
    public class OpenFoodFactsServiceTests
    {
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseContent;
            private readonly HttpStatusCode _statusCode;

            public TestHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                _responseContent = responseContent;
                _statusCode = statusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = _statusCode,
                    Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
                });
            }
        }

        private static HttpClient CreateHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpClient(new TestHttpMessageHandler(responseJson, statusCode))
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        [Fact]
        public async Task GetProductByBarcodeAsync_ValidProduct_ReturnsMappedResult()
        {
            var json = "{\"code\":\"5901234123457\",\"status\":1,\"product\":{\"product_name\":\"Test Product\",\"nutriments\":{\"energy-kcal_100g\":250.0,\"proteins_100g\":12.5,\"fat_100g\":8.3,\"carbohydrates_100g\":30.1}}}";

            var httpClient = CreateHttpClient(json);
            var service = new OpenFoodFactsService(httpClient);

            var result = await service.GetProductByBarcodeAsync("5901234123457");

            result.Should().NotBeNull();
            result!.Barcode.Should().Be("5901234123457");
            result.Name.Should().Be("Test Product");
            result.Calories100g.Should().Be(250.0);
            result.Protein100g.Should().Be(12.5);
            result.Fat100g.Should().Be(8.3);
            result.Carbs100g.Should().Be(30.1);
        }

        [Fact]
        public async Task GetProductByBarcodeAsync_ProductNotFound_ReturnsNull()
        {
            var json = "{\"code\":\"5901234123457\",\"status\":0,\"product\":null}";

            var httpClient = CreateHttpClient(json);
            var service = new OpenFoodFactsService(httpClient);

            var result = await service.GetProductByBarcodeAsync("5901234123457");

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetProductByBarcodeAsync_MissingNutriments_ReturnsNameOnly()
        {
            var json = "{\"code\":\"5901234123457\",\"status\":1,\"product\":{\"product_name\":\"No Nutrition Product\",\"nutriments\":null}}";

            var httpClient = CreateHttpClient(json);
            var service = new OpenFoodFactsService(httpClient);

            var result = await service.GetProductByBarcodeAsync("5901234123457");

            result.Should().NotBeNull();
            result!.Name.Should().Be("No Nutrition Product");
            result.Calories100g.Should().Be(-1);
            result.Protein100g.Should().Be(-1);
        }

        [Fact]
        public async Task GetProductByBarcodeAsync_PartialNutriments_ReturnsAvailableData()
        {
            var json = "{\"code\":\"5901234123457\",\"status\":1,\"product\":{\"product_name\":\"Partial Product\",\"nutriments\":{\"energy-kcal_100g\":150.0}}}";

            var httpClient = CreateHttpClient(json);
            var service = new OpenFoodFactsService(httpClient);

            var result = await service.GetProductByBarcodeAsync("5901234123457");

            result.Should().NotBeNull();
            result!.Name.Should().Be("Partial Product");
            result.Calories100g.Should().Be(150.0);
            result.Protein100g.Should().Be(-1);
            result.Fat100g.Should().Be(-1);
            result.Carbs100g.Should().Be(-1);
        }

        [Fact]
        public async Task GetProductByBarcodeAsync_HttpError_ReturnsNull()
        {
            var httpClient = CreateHttpClient("", HttpStatusCode.NotFound);
            var service = new OpenFoodFactsService(httpClient);

            var result = await service.GetProductByBarcodeAsync("5901234123457");

            result.Should().BeNull();
        }

    }
}
