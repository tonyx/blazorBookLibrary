using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class TagsClientService : ITagService
{
    private readonly HttpClient _httpClient;

    public TagsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<FSharpList<Tag>, string>> GetTagsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Tags", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tag>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tag>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tag>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tag>, string>> GetBookTypeTagsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Tags/books", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tag>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tag>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tag>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tag>, string>> GetAuthorTypeTagsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Tags/authors", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tag>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tag>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tag>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tag>, string>> GetGeneralTypeTagsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Tags/general", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tag>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tag>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tag>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tag>, string>> GetPersonTypeTagsAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Tags/person", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tag>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tag>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tag>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> AddTagAsync(Commons.UserContext context, Tag tag, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Tags", tag, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveTagAsync(Commons.UserContext context, Tag tag, FSharpOption<CancellationToken> ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "api/Tags")
        {
            Content = JsonContent.Create(tag, typeof(Tag), options: ServiceClientHelper.JsonOptions)
        };
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ReplaceTagAsync(Commons.UserContext context, Tag oldTag, Tag newTag, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Tags/replace", new { oldTag, newTag }, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> EnsureTagsRepoCreatedAsync(FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync("api/Tags/ensure-repo", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
