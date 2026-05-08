
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;

namespace blazorBookLibrary.Client.Services;

public class AdminClientService : IAdminServices
{
    private readonly HttpClient _httpClient;

    public AdminClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> PurgeVectorsReferringDroppedBooksAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync("api/Admin/vectors/purge", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AdjustBookStatesReferringMissingEmbeddingsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync("api/Admin/books/adjust-states", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AssignUserToDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId id, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Admin/distribution-points/{id.Value}/assign-user/{userId.Value}", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UnassignUserFromDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId id, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Admin/distribution-points/{id.Value}/unassign-user/{userId.Value}", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UpdateDistributionPointInfoAsync(Commons.UserContext context, Commons.DistributionPointId id, Commons.Info info, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Admin/distribution-points/{id.Value}/info", info, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RenameDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId id, Commons.NonEmptyName newName, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Admin/distribution-points/{id.Value}/rename", newName.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
