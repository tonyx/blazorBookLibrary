
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class TenantClientService : ITenantService
{
    private readonly HttpClient _httpClient;

    public TenantClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<FSharpResult<Unit, string>> EnsureDefaultTenantExistsAsync(Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        throw new NotSupportedException("EnsureDefaultTenantExistsAsync is not supported on the client.");
    }

    public async Task<FSharpResult<Unit, string>> CreateTenantAsync(Commons.UserContext context, Tenant tenant, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/Tenant", context, tenant);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Tenant, string>> GetTenantAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Tenant/{tenantId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Tenant>(response);
    }
}
