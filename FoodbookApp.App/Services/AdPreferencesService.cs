using FoodbookApp.Interfaces;

namespace FoodbookApp.Services;

public sealed class AdPreferencesService : IAdPreferencesService
{
    private const string SupportDeveloperKey = "SupportDeveloper";
    private const string RecipeManualSaveCounterKey = "RecipeManualSaveCounter";
    private const string IngredientManualSaveCounterKey = "IngredientManualSaveCounter";
    private const string LastInterstitialShownUtcKey = "LastInterstitialShownUtc";

    private readonly IAdPreferencesStorage _storage;

    public AdPreferencesService(IAdPreferencesStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public bool GetSupportDeveloper() => _storage.GetBool(SupportDeveloperKey, true);

    public void SetSupportDeveloper(bool isEnabled) => _storage.SetBool(SupportDeveloperKey, isEnabled);

    public int GetRecipeManualSaveCounter() => Math.Max(0, _storage.GetInt(RecipeManualSaveCounterKey, 0));

    public void SetRecipeManualSaveCounter(int value) => _storage.SetInt(RecipeManualSaveCounterKey, Math.Max(0, value));

    public int GetIngredientManualSaveCounter() => Math.Max(0, _storage.GetInt(IngredientManualSaveCounterKey, 0));

    public void SetIngredientManualSaveCounter(int value) => _storage.SetInt(IngredientManualSaveCounterKey, Math.Max(0, value));

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
