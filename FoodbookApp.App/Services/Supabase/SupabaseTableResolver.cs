namespace FoodbookApp.Services.Supabase;

public static class SupabaseTableResolver
{
    public const bool TEST_ENDPOINTS = false;

    public static string Resolve(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return tableName;

        return TEST_ENDPOINTS && !tableName.EndsWith("_test", StringComparison.OrdinalIgnoreCase)
            ? $"{tableName}_test"
            : tableName;
    }
}
