using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using static BookLibrary.Shared.Commons;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class VectorDbClientService : IVectorDbService
{
    private readonly HttpClient _httpClient;

    public VectorDbClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private record EmbeddingResponse(string model, float[] vector, Guid bookId);
    private record EmbeddingScoreResponse(string model, float[] vector, Guid bookId, double score);
    private record IdBookIdResponse(Guid id, Guid bookId);

    public async Task<FSharpResult<Unit, string>> StoreEmbeddingAsync(EmbeddingDataId id, BookId bookId, EmbeddingData embedding, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            id = id.Value,
            bookId = bookId.Value,
            model = embedding.Model,
            vector = embedding.Vector
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/store", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Tuple<EmbeddingData, BookId>, string>> ReadEmbeddingAsync(EmbeddingDataId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/VectorDb/read/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<EmbeddingResponse>(response);
        if (result.IsOk)
        {
            var data = result.ResultValue;
            var embedding = new EmbeddingData(data.model, data.vector);
            return FSharpResult<Tuple<EmbeddingData, BookId>, string>.NewOk(Tuple.Create(embedding, BookId.NewBookId(data.bookId)));
        }
        return FSharpResult<Tuple<EmbeddingData, BookId>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> UpdateEmbeddingAsync(EmbeddingDataId id, EmbeddingData embedding, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            id = id.Value,
            model = embedding.Model,
            vector = embedding.Vector
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/update", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveEmbeddingAsync(EmbeddingDataId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/VectorDb/remove/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveEmbeddingsAsync(IEnumerable<EmbeddingDataId> ids, FSharpOption<CancellationToken> ct)
    {
        var request = ids.Select(i => i.Value).ToList();
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/remove-multiple", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>> SearchSimilarEmbeddingsAsync(EmbeddingData embedding, int limit, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            vector = embedding.Vector,
            model = embedding.Model,
            limit = limit
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/search", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<EmbeddingResponse>>(response);
        if (result.IsOk)
        {
            var list = result.ResultValue.Select(d => Tuple.Create(new EmbeddingData(d.model, d.vector), BookId.NewBookId(d.bookId)));
            return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>.NewOk(list);
        }
        return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>> SearchSimilarEmbeddingsWithScoreAsync(EmbeddingData embedding, int limit, FSharpOption<double> threshold, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            vector = embedding.Vector,
            model = embedding.Model,
            limit = limit,
            threshold = threshold != null && FSharpOption<double>.get_IsSome(threshold) ? (double?)threshold.Value : null
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/search-with-score", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<EmbeddingScoreResponse>>(response);
        if (result.IsOk)
        {
            var list = result.ResultValue.Select(d => Tuple.Create(new EmbeddingData(d.model, d.vector), BookId.NewBookId(d.bookId), d.score));
            return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>.NewOk(list);
        }
        return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>> SearchSimilarEmbeddingsFilteringByBookIdsAsync(EmbeddingData embedding, FSharpList<BookId> bookIds, int limit, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            vector = embedding.Vector,
            model = embedding.Model,
            bookIds = bookIds.Select(b => b.Value).ToList(),
            limit = limit
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/search-filtered", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<EmbeddingResponse>>(response);
        if (result.IsOk)
        {
            var list = result.ResultValue.Select(d => Tuple.Create(new EmbeddingData(d.model, d.vector), BookId.NewBookId(d.bookId)));
            return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>.NewOk(list);
        }
        return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>> SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync(EmbeddingData embedding, FSharpList<BookId> bookIds, int limit, FSharpOption<double> threshold, FSharpOption<CancellationToken> ct)
    {
        var request = new
        {
            vector = embedding.Vector,
            model = embedding.Model,
            bookIds = bookIds.Select(b => b.Value).ToList(),
            limit = limit,
            threshold = threshold != null && FSharpOption<double>.get_IsSome(threshold) ? (double?)threshold.Value : null
        };
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/search-with-score-filtered", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<EmbeddingScoreResponse>>(response);
        if (result.IsOk)
        {
            var list = result.ResultValue.Select(d => Tuple.Create(new EmbeddingData(d.model, d.vector), BookId.NewBookId(d.bookId), d.score));
            return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>.NewOk(list);
        }
        return FSharpResult<IEnumerable<Tuple<EmbeddingData, BookId, double>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<IEnumerable<Tuple<EmbeddingDataId, BookId>>, string>> ReadAllEmbeddingIdsWithBookIdsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/VectorDb/all-ids", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<IdBookIdResponse>>(response);
        if (result.IsOk)
        {
            var list = result.ResultValue.Select(d => Tuple.Create(EmbeddingDataId.NewEmbeddingDataId(d.id), BookId.NewBookId(d.bookId)));
            return FSharpResult<IEnumerable<Tuple<EmbeddingDataId, BookId>>, string>.NewOk(list);
        }
        return FSharpResult<IEnumerable<Tuple<EmbeddingDataId, BookId>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<EmbeddingDataId>, string>> EnquiryForMissingEmbeddingsAsync(FSharpList<EmbeddingDataId> ids, FSharpOption<CancellationToken> ct)
    {
        var request = ids.Select(i => i.Value).ToList();
        var response = await _httpClient.PostAsJsonAsync("api/VectorDb/enquiry-missing", request, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Guid>>(response);
        if (result.IsOk)
        {
            var list = ListModule.OfSeq(result.ResultValue.Select(EmbeddingDataId.NewEmbeddingDataId));
            return FSharpResult<FSharpList<EmbeddingDataId>, string>.NewOk(list);
        }
        return FSharpResult<FSharpList<EmbeddingDataId>, string>.NewError(result.ErrorValue);
    }
}
