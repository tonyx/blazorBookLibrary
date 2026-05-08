
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class GoogleBooksClientService : IGoogleBooksService
{
    private readonly HttpClient _httpClient;

    public GoogleBooksClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<FSharpOption<GoogleBookMetadata>, string>> LookupByIsbnAsync(Commons.UserContext context, string isbn, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/GoogleBooks/lookup/isbn/{isbn}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpOption<GoogleBookMetadata>>(response);
    }

    public async Task<FSharpResult<FSharpOption<GoogleBookMetadata>, string>> LookupByTitleAsync(Commons.UserContext context, string title, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/GoogleBooks/lookup/title/{Uri.EscapeDataString(title)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpOption<GoogleBookMetadata>>(response);
    }

    public async Task<FSharpResult<FSharpList<GoogleBookMetadata>, string>> LookupMultipleByTitleAsync(Commons.UserContext context, string title, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/GoogleBooks/lookup/multiple/title/{Uri.EscapeDataString(title)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<GoogleBookMetadata>>(response);
        return result.IsOk ? FSharpResult<FSharpList<GoogleBookMetadata>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<GoogleBookMetadata>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpOption<string>, string>> LookupCoverImageByIsbnAsync(Commons.UserContext context, Commons.Isbn isbn, FSharpOption<Commons.ThumbRoughSize> thumbRoughSize, FSharpOption<CancellationToken> ct)
    {
        return await LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(context, isbn, thumbRoughSize, ct);
    }

    public async Task<FSharpResult<FSharpOption<string>, string>> LookupGoogleApiCoverImageByIsbnAsync(Commons.UserContext context, Commons.Isbn isbn, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/GoogleBooks/cover/isbn/{isbn.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpOption<string>>(response);
    }

    public async Task<FSharpResult<FSharpOption<string>, string>> LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(Commons.UserContext context, Commons.Isbn isbn, FSharpOption<Commons.ThumbRoughSize> thumbRoughSize, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/GoogleBooks/cover/isbn/{isbn.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpOption<string>>(response);
    }

    public async Task<FSharpResult<FSharpOption<string>, string>> LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(Commons.UserContext context, string title, FSharpOption<string> author, FSharpOption<CancellationToken> ct)
    {
        var authorParam = FSharpOption<string>.get_IsSome(author) ? $"&author={Uri.EscapeDataString(author.Value)}" : "";
        var response = await _httpClient.GetAsync($"api/GoogleBooks/cover/title/{Uri.EscapeDataString(title)}{authorParam}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpOption<string>>(response);
    }
}
