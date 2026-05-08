using blazorBookLibrary.Shared.Security;
using System.Threading.Tasks;

namespace blazorBookLibrary.Client.Services;

/// <summary>
/// A client-side implementation of IBotScoreService.
/// Since bot detection is primarily a server-side concern for security,
/// this client implementation provides safe defaults.
/// </summary>
public class BotScoreClientService : IBotScoreService
{
    public Task<double> GetBotScoreAsync(string token)
    {
        // Return 1.0 (indicating a high probability of being human)
        // In a real implementation, this might call a reCAPTCHA client-side API
        return Task.FromResult(1.0);
    }

    public Task ApplyBotDelayAsync(double score)
    {
        // On the client, we typically don't want to artificialy delay the UI
        // unless it's part of a specific UX requirement.
        return Task.CompletedTask;
    }
}
