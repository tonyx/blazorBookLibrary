using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace blazorBookLibrary.Client.Services;

public class TechnicalInfoClientService : ITechnicalInfoService
{
    private readonly HttpClient _httpClient;

    public TechnicalInfoClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetTechnicalInfoMarkdownAsync(string? lang = null, CancellationToken cancellationToken = default)
    {
        var resolvedLang = !string.IsNullOrWhiteSpace(lang)
            ? lang.Trim().ToLowerInvariant()
            : CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant();

        if (resolvedLang != "it")
        {
            resolvedLang = "en";
        }

        var response = await _httpClient.GetAsync($"api/TechnicalInfo?lang={Uri.EscapeDataString(resolvedLang)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
