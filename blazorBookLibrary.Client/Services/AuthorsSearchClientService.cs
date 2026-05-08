
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class AuthorsSearchClientService : IAuthorsSearchService
{
    private readonly HttpClient _httpClient;

    public AuthorsSearchClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<AuthorMetadata, string>> LookupByNameAsync(Commons.UserContext context, string name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/AuthorsSearch/lookup?name={Uri.EscapeDataString(name)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<AuthorMetadata>(response);
    }

    public async Task<FSharpResult<string, string>> LookupImageUrlByNameAndThumbSizeAsync(Commons.UserContext context, string name, FSharpOption<int> pitThumbSize, FSharpOption<CancellationToken> ct)
    {
        var thumbSizeParam = FSharpOption<int>.get_IsSome(pitThumbSize) ? $"&thumbSize={pitThumbSize.Value}" : "";
        var response = await _httpClient.GetAsync($"api/AuthorsSearch/image?name={Uri.EscapeDataString(name)}{thumbSizeParam}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<string>(response);
    }

    public async Task<FSharpResult<FSharpList<string>, string>> LookupBioByNameAsync(Commons.UserContext context, string name, FSharpOption<Commons.ShortLang> lang, FSharpOption<CancellationToken> ct)
    {
        var langParam = FSharpOption<Commons.ShortLang>.get_IsSome(lang) ? $"&lang={lang.Value.Value}" : "";
        var response = await _httpClient.GetAsync($"api/AuthorsSearch/bio?name={Uri.EscapeDataString(name)}{langParam}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<string>>(response);
        return result.IsOk ? FSharpResult<FSharpList<string>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<string>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<string, string>> LookupWikipediaUriByNameAsync(Commons.UserContext context, string name, FSharpOption<Commons.ShortLang> lang, FSharpOption<CancellationToken> ct)
    {
        var langParam = FSharpOption<Commons.ShortLang>.get_IsSome(lang) ? $"&lang={lang.Value.Value}" : "";
        var response = await _httpClient.GetAsync($"api/AuthorsSearch/wikipedia?name={Uri.EscapeDataString(name)}{langParam}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<string>(response);
    }
}
