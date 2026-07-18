namespace FoodbookApp.Interfaces;

public interface IAdCounterService
{
    Task RecordManualRecipeSaveAsync();
    Task RecordManualIngredientSaveAsync();
    Task RecordManualPlanSaveAsync();
}
