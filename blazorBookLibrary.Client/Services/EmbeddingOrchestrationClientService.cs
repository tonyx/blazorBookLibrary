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
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/EmbeddingOrchestration/create-embedding", context, bookId.Value);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> CreateEmbeddingsForBooksIfMissingAsync(Commons.UserContext context, Microsoft.FSharp.Collections.FSharpList<Commons.BookId> bookIds, FSharpOption<CancellationToken> ct)
    {
        var rawIds = bookIds.Select(x => x.Value).ToList();
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/EmbeddingOrchestration/create-embeddings-if-missing", context, rawIds);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> CreateEmbeddingForPaperAsync(Commons.UserContext context, Commons.BookId bookId, Commons.PaperId paperId, string text, FSharpOption<bool> storeDescription, FSharpOption<CancellationToken> ct)
    {
        bool? storeDesc = storeDescription != null && FSharpOption<bool>.get_IsSome(storeDescription) ? storeDescription.Value : (bool?)null;
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/EmbeddingOrchestration/create-paper-embedding", context, new { bookId = bookId.Value, paperId = paperId.Value, text, storeDescription = storeDesc });
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveEmbeddingForPaperAsync(Commons.UserContext context, Commons.BookId bookId, Commons.PaperId paperId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/EmbeddingOrchestration/remove-paper-embedding", context, new { bookId = bookId.Value, paperId = paperId.Value });
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}


