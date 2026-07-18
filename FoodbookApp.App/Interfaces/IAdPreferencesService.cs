namespace FoodbookApp.Interfaces;

public interface IAdPreferencesService
{
    bool GetSupportDeveloper();
    void SetSupportDeveloper(bool isEnabled);

    int GetRecipeManualSaveCounter();
    void SetRecipeManualSaveCounter(int value);

    int GetIngredientManualSaveCounter();
    void SetIngredientManualSaveCounter(int value);
    int GetPlanManualSaveCounter();
    void SetPlanManualSaveCounter(int value);

    DateTime? GetLastInterstitialShownUtc();
    void SetLastInterstitialShownUtc(DateTime utcValue);
}
