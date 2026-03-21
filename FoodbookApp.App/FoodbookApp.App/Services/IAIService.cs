using System.Threading;
using System.Threading.Tasks;

namespace FoodbookApp.Services;

/// <summary>
/// Interfejs odpowiedzialny za komunikacjê z zewnêtrznym API sztucznej inteligencji.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Pobiera odpowiedŸ od modelu jêzykowego na podstawie dostarczonego promptu.
    /// </summary>
    /// <param name="systemPrompt">Prompt systemowy steruj¹cy zachowaniem modelu</param>
    /// <param name="userPrompt">W³aœciwe zapytanie u¿ytkownika</param>
    /// <param name="cancellationToken">Token anulowania zadania</param>
    /// <returns>Tekst odpowiedzi od modelu AI</returns>
    Task<string> GetAIResponseAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
