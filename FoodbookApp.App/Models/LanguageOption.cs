namespace Foodbook.Models;

/// <summary>
/// Represents a language option in the setup wizard
/// </summary>
public class LanguageOption
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"{DisplayName} ({NativeName})";
    }
}