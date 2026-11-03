
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using BookLibrary.Shared.Formats;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class DataExportClientService : IDataExportService
{
    private readonly HttpClient _httpClient;

    public DataExportClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<string, string>> ExportAllBooksAsync(Commons.UserContext context, Commons.ExportFormat format, FSharpOption<CancellationToken> ct)
    {
        var formatStr = format.Code;
        var response = await _httpClient.GetAsync($"api/DataExport/export/books?format={formatStr}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return FSharpResult<string, string>.NewOk(content);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            return FSharpResult<string, string>.NewError(error);
        }
    }

    public async Task<FSharpResult<ImportSummary, string>> ImportFromIsbns(Commons.UserContext context, FSharpList<Commons.Isbn> isbns, bool preventDuplicates, bool generateUnknownAuthors, bool generateEmbeddings, bool generateMissingDescriptions, IProgress<ImportProgress> progress, CancellationToken ct)
    {
        var isbnStrings = isbns.Select(i => i.Value).ToList();
        var url = $"api/DataExport/import/isbns?preventDuplicates={preventDuplicates}&generateUnknownAuthors={generateUnknownAuthors}&generateEmbeddings={generateEmbeddings}&generateMissingDescriptions={generateMissingDescriptions}";
        var response = await _httpClient.PostAsJsonAsync(url, isbnStrings, ServiceClientHelper.JsonOptions, ct);
        return await ServiceClientHelper.HandleResponse<ImportSummary>(response);
    }

    public async Task<FSharpResult<CatalogImportResult, string>> ImportFromFormatAsync(
        Commons.UserContext context,
        string payload,
        Commons.ExportFormat format,
        bool preventDuplicates,
        bool generateUnknownAuthors,
        bool generateEmbeddings,
        bool generateMissingDescriptions,
        bool createMissingPublishers,
        bool enrichMissingMetadataViaIsbn,
        IProgress<ImportProgress> progress,
        CancellationToken ct)
    {
        var formatStr = format.Code;
        var url = $"api/DataExport/import/format?format={formatStr}&preventDuplicates={preventDuplicates}&generateUnknownAuthors={generateUnknownAuthors}&generateEmbeddings={generateEmbeddings}&generateMissingDescriptions={generateMissingDescriptions}&createMissingPublishers={createMissingPublishers}&enrichMissingMetadataViaIsbn={enrichMissingMetadataViaIsbn}";
        var content = new StringContent(payload, System.Text.Encoding.UTF8, format.ContentType);
        var response = await _httpClient.PostAsync(url, content, ct);
        return await ServiceClientHelper.HandleResponse<CatalogImportResult>(response);
    }
}
