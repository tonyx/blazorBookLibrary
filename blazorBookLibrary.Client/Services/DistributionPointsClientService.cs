
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class DistributionPointsClientService : IDistributionPointService
{
    private readonly HttpClient _httpClient;

    public DistributionPointsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> CreateDistributionPointAsync(Commons.UserContext context, DistributionPoint distributionPoint, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/DistributionPoints", distributionPoint, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<DistributionPoint, string>> GetDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<DistributionPoint>(response);
    }

    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> GetAllDistributionPointsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/DistributionPoints", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> GetAllDistributionPointsManagedByUser(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/managed-by-user/{userId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }


    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> GetAllDistributionPointsOfATenantAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/tenant/{tenantId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> FindDistributionPointsAsync(Commons.UserContext context, Commons.Name name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/find/{Uri.EscapeDataString(name.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> GetAllBooksOfADistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId distributionPointId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/{distributionPointId.Value}/books", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<bool, string>> IsRemovableAsync(Commons.UserContext context, Commons.DistributionPointId distributionPointId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/{distributionPointId.Value}/is-removable", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<bool>(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/DistributionPoints/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AddReferenceUser(Commons.UserContext context, Commons.DistributionPointId distributionPointId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/DistributionPoints/{distributionPointId.Value}/reference-user/{userId.Value}", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveReferenceUser(Commons.UserContext context, Commons.DistributionPointId distributionPointId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/DistributionPoints/{distributionPointId.Value}/reference-user/{userId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
