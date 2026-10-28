using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class UserTenantResolverClientService : IUserTenantResolverService
{
    private readonly HttpClient _httpClient;

    public UserTenantResolverClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Commons.TenantId, string>> GetTenantForUserAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/UserTenantResolver", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Commons.TenantId>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> ResolveSearchTenantsAsync(Commons.UserContext context, Commons.TenantScope scope, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/UserTenantResolver/search-tenants", context, scope);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tenant>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tenant>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tenant>, string>.NewError(result.ErrorValue);
    }
}
