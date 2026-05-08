
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

    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> FindDistributionPointsAsync(Commons.UserContext context, Commons.Name name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/DistributionPoints/find/{Uri.EscapeDataString(name.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }
}
