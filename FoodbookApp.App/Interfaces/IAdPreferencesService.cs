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
    int GetFoodbookManualSaveCounter();
    void SetFoodbookManualSaveCounter(int value);
    int GetShoppingListManualSaveCounter();
    void SetShoppingListManualSaveCounter(int value);

    DateTime? GetLastInterstitialShownUtc();
    void SetLastInterstitialShownUtc(DateTime utcValue);
}
