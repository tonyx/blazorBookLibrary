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
}
