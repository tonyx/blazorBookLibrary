using System.Linq;
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;

namespace blazorBookLibrary.Client.Services;

public class EmbeddingOrchestrationClientService : IEmbeddingOrchestrationService
{
    private readonly HttpClient _httpClient;

    public EmbeddingOrchestrationClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> CreateEmbeddingForBookAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/EmbeddingOrchestration/create-embedding", bookId.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> CreateEmbeddingsForBooksIfMissingAsync(Commons.UserContext context, Microsoft.FSharp.Collections.FSharpList<Commons.BookId> bookIds, FSharpOption<CancellationToken> ct)
    {
        var rawIds = bookIds.Select(x => x.Value).ToList();
        var response = await _httpClient.PostAsJsonAsync("api/EmbeddingOrchestration/create-embeddings-if-missing", rawIds, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}

