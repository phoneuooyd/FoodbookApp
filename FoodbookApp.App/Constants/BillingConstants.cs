namespace FoodbookApp.Constants;

public static class BillingConstants
{
    public const string PackageName = "com.ppapps.foodbookapp";

    public const string MonthlyProductId = "foodbook_premium_monthly";
    public const string YearlyProductId = "foodbook_premium_yearly";

    public static readonly Dictionary<string, Foodbook.Models.SubscriptionPlan> ProductPlanMap = new()
    {
        [MonthlyProductId] = Foodbook.Models.SubscriptionPlan.PremiumMonthly,
        [YearlyProductId] = Foodbook.Models.SubscriptionPlan.PremiumYearly,
    };

    public static readonly Dictionary<Foodbook.Models.SubscriptionPlan, string> PlanProductMap = new()
    {
        [Foodbook.Models.SubscriptionPlan.PremiumMonthly] = MonthlyProductId,
        [Foodbook.Models.SubscriptionPlan.PremiumYearly] = YearlyProductId,
    };

    public const string SupabaseEdgeFunctionUrl =
        "https://gscbdvezastxpyndkauh.supabase.co/functions/v1/verify-purchase";
}
