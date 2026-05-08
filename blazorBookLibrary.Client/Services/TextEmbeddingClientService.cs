
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using blazorBookLibrary.Client.Services;

namespace blazorBookLibrary.Client.Services;

public class TextEmbeddingClientService : ITextEmbeddingService
{
    private readonly HttpClient _httpClient;

    public TextEmbeddingClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Commons.EmbeddingData, string>> GetEmbeddingAsync(Commons.UserContext context, string text, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/TextEmbedding/embedding", text, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Commons.EmbeddingData>(response);
    }

    public async Task<FSharpResult<string, string>> GetMatchExplanationAsync(Commons.UserContext context, string query, string itemText, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/TextEmbedding/explain-match", new { query, itemText }, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<string>(response);
    }

    public async Task<FSharpResult<string, string>> GetBookDescriptionAsync(Commons.UserContext context, PartialBookDataMatch bookData, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/TextEmbedding/generate-description", bookData, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<string>(response);
    }

    public async Task<FSharpResult<PartialBookDataMatch, string>> GetPartialBookMatchByCoverImage(Commons.UserContext context, string base64Image, string mimeType, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/TextEmbedding/identify-from-cover", new { base64Image, mimeType }, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<PartialBookDataMatch>(response);
    }
}
