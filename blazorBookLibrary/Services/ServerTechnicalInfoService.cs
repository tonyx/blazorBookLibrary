using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using blazorBookLibrary.Client.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace blazorBookLibrary.Services;

public class ServerTechnicalInfoService : ITechnicalInfoService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ServerTechnicalInfoService> _logger;

    public ServerTechnicalInfoService(IWebHostEnvironment env, ILogger<ServerTechnicalInfoService> logger)
    {
        _env = env;
        _logger = logger;
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

        var candidatePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", resolvedLang, "biblionet_technical_info.md"),
            Path.Combine(AppContext.BaseDirectory, "Templates", resolvedLang, "biblionet_technical_info.md"),
            Path.Combine(_env.ContentRootPath, "Templates", resolvedLang, "biblionet_technical_info.md"),
            Path.Combine(_env.ContentRootPath, "..", "blazorBookLibrary.Server", "Templates", resolvedLang, "biblionet_technical_info.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", resolvedLang, "biblionet_technical_info.md"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "blazorBookLibrary.Server", "Templates", resolvedLang, "biblionet_technical_info.md")
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path, cancellationToken);
            }
        }

        _logger.LogError("Technical info markdown file for locale {Lang} could not be found in any candidate paths.", resolvedLang);
        throw new FileNotFoundException($"Technical info template for '{resolvedLang}' not found.");
    }
}
