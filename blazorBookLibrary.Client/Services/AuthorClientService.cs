
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class AuthorClientService : IAuthorService
{
    private readonly HttpClient _httpClient;

    public AuthorClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> AddAuthorAsync(Commons.UserContext context, Author author, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Authors", author, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AddAuthorsAsync(Commons.UserContext context, FSharpList<Author> authors, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Authors/bulk", authors.ToList(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Author, string>> GetAuthorAsync(Commons.UserContext context, Commons.AuthorId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Author>(response);
    }

    public async Task<FSharpResult<FSharpList<Author>, string>> GetAuthorsAsync(Commons.UserContext context, FSharpList<Commons.AuthorId> ids, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Authors/get-multiple", ids.Select(i => i.Value).ToList(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Author>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Author>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Author>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> GetAuthorBooksAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/{authorId.Value}/books", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> RenameAsync(Commons.UserContext context, Commons.AuthorId authorId, Commons.Name name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Authors/{authorId.Value}/rename", name.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/Authors/{authorId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<BookLibrary.Shared.Details.AuthorDetails, string>> GetAuthorDetailsAsync(Commons.UserContext context, Commons.AuthorId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/{id.Value}/details", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<BookLibrary.Shared.Details.AuthorDetails>(response);
    }

    public async Task<FSharpResult<Unit, string>> UpdateImageUrlAsync(Commons.UserContext context, Commons.AuthorId authorId, Uri imageUrl, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Authors/{authorId.Value}/image-url", imageUrl.ToString(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveImageUrlAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/Authors/{authorId.Value}/image-url", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UpdateIsniAsync(Commons.UserContext context, Commons.AuthorId authorId, Commons.Isni isni, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Authors/{authorId.Value}/isni", isni.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UpdateBioAsync(Commons.UserContext context, Commons.AuthorId authorId, string bio, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Authors/{authorId.Value}/bio", bio, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UpdateWikipediaUriAsync(Commons.UserContext context, Commons.AuthorId authorId, Uri wikipediaUri, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Authors/{authorId.Value}/wikipedia-uri", wikipediaUri.ToString(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SealAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Authors/{authorId.Value}/seal", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UnsealAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Authors/{authorId.Value}/unseal", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<Author>, string>> GetAllAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Authors", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Author>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Author>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Author>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Author>, string>> SearchByNameAsync(Commons.UserContext context, Commons.Name name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/search/name/{Uri.EscapeDataString(name.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Author>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Author>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Author>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Author>, string>> SearchByIsniAsync(Commons.UserContext context, Commons.Isni strisni, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/search/isni/{Uri.EscapeDataString(strisni.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Author>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Author>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Author>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Author>, string>> SearchByIsniAndNameAsync(Commons.UserContext context, Commons.Isni isni, Commons.Name name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Authors/search/isni/{Uri.EscapeDataString(isni.Value)}/name/{Uri.EscapeDataString(name.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Author>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Author>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Author>, string>.NewError(result.ErrorValue);
    }
}
