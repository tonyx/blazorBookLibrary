namespace blazorBookLibrary.Client.Services;

public interface ITechnicalInfoService
{
    Task<string> GetTechnicalInfoMarkdownAsync(string? lang = null, CancellationToken cancellationToken = default);
}
