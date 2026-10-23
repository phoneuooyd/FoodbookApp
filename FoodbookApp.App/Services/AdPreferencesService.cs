using FoodbookApp.Interfaces;
using Foodbook.Services;

namespace FoodbookApp.Services;

public sealed class AdPreferencesService : IAdPreferencesService
{
    private const string SupportDeveloperKey = "SupportDeveloper";
    private const string RecipeManualSaveCounterKey = "RecipeManualSaveCounter";
    private const string IngredientManualSaveCounterKey = "IngredientManualSaveCounter";
    private const string PlanManualSaveCounterKey = "PlanManualSaveCounter";
    private const string FoodbookManualSaveCounterKey = "FoodbookManualSaveCounter";
    private const string ShoppingListManualSaveCounterKey = "ShoppingListManualSaveCounter";
    private const string LastInterstitialShownUtcKey = "LastInterstitialShownUtc";

    private readonly IAdPreferencesStorage _storage;

    public AdPreferencesService(IAdPreferencesStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public bool GetSupportDeveloper() => _storage.GetBool(SupportDeveloperKey, true);

    public void SetSupportDeveloper(bool isEnabled)
    {
        _storage.SetBool(SupportDeveloperKey, isEnabled);
        AppEvents.RaiseAdPreferencesChanged();
    }

    public int GetRecipeManualSaveCounter() => Math.Max(0, _storage.GetInt(RecipeManualSaveCounterKey, 0));

    public void SetRecipeManualSaveCounter(int value) => _storage.SetInt(RecipeManualSaveCounterKey, Math.Max(0, value));

    public int GetIngredientManualSaveCounter() => Math.Max(0, _storage.GetInt(IngredientManualSaveCounterKey, 0));

    public void SetIngredientManualSaveCounter(int value) => _storage.SetInt(IngredientManualSaveCounterKey, Math.Max(0, value));

    public int GetPlanManualSaveCounter() => Math.Max(0, _storage.GetInt(PlanManualSaveCounterKey, 0));

    public void SetPlanManualSaveCounter(int value) => _storage.SetInt(PlanManualSaveCounterKey, Math.Max(0, value));

    public int GetFoodbookManualSaveCounter() => Math.Max(0, _storage.GetInt(FoodbookManualSaveCounterKey, 0));

    public void SetFoodbookManualSaveCounter(int value) => _storage.SetInt(FoodbookManualSaveCounterKey, Math.Max(0, value));

    public int GetShoppingListManualSaveCounter() => Math.Max(0, _storage.GetInt(ShoppingListManualSaveCounterKey, 0));

    public void SetShoppingListManualSaveCounter(int value) => _storage.SetInt(ShoppingListManualSaveCounterKey, Math.Max(0, value));

    public DateTime? GetLastInterstitialShownUtc()
    {
        var raw = _storage.GetString(LastInterstitialShownUtcKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    public void SetLastInterstitialShownUtc(DateTime utcValue)
        => _storage.SetString(LastInterstitialShownUtcKey, utcValue.ToUniversalTime().ToString("O"));
}
