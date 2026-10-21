using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using static BookLibrary.Shared.Commons;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class TenantApiKeyClientService : ITenantApiKeyService
{
    private readonly HttpClient _httpClient;

    public TenantApiKeyClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> AddPartnerKeyAsync(
        Commons.UserContext context,
        Commons.TenantId tenantId,
        PartnerApiKey partnerApiKey,
        FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/TenantApiKey/{tenantId.Value}", context, partnerApiKey);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AddPartnerKeyAsync(
        Commons.UserContext context,
        Commons.TenantId tenantId,
        Commons.UserId partnerId,
        string label,
        DateTime now,
        FSharpOption<DateTime> expiresAt,
        FSharpOption<CancellationToken> ct)
    {
        var payload = new
        {
            PartnerId = partnerId.Value,
            Label = label,
            Now = (DateTime?)now,
            ExpiresAt = (expiresAt != null && FSharpOption<DateTime>.get_IsSome(expiresAt)) ? (DateTime?)expiresAt.Value : null
        };
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/TenantApiKey/{tenantId.Value}/generate", context, payload);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RevokeCatalogKeyAsync(
        Commons.UserContext context,
        Commons.TenantId tenantId,
        CatalogApiKey catalogApiKey,
        FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/TenantApiKey/{tenantId.Value}/{catalogApiKey.Value}/revoke", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemovePartnerKeyAsync(
        Commons.UserContext context,
        Commons.TenantId tenantId,
        CatalogApiKey catalogApiKey,
        FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/TenantApiKey/{tenantId.Value}/{catalogApiKey.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ValidateCatalogKeyAsync(
        Commons.TenantId tenantId,
        CatalogApiKey catalogApiKey,
        FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/TenantApiKey/{tenantId.Value}/validate/{catalogApiKey.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<PartnerApiKey>, string>> GetPartnerKeysAsync(
        Commons.UserContext context,
        Commons.TenantId tenantId,
        FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/TenantApiKey/{tenantId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<PartnerApiKey>>(response);
    }
}
