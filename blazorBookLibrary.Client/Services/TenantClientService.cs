
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using static BookLibrary.Shared.Commons;
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

    public async Task<FSharpResult<Unit, string>> AddPatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, PatronRole role, FSharpOption<CancellationToken> ct)
    {
        var patron = new { UserId = userId.Value, Role = role };
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Tenant/{tenantId.Value}/patrons", context, patron);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> DemotePatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/demote", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> PromotePatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/promote", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemovePatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> InvitePatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/invite", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RevokePatronInvitation(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/invite", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SuspendPatron(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, string reason, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/suspend?reason={System.Uri.EscapeDataString(reason)}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ReAdmittPatron(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/readmit", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ConvertInvitedPatronToPatronAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.PatronInvitationCode patronInvitationCode, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/patrons/convert?invitationCode={System.Uri.EscapeDataString(patronInvitationCode.Value.ToString())}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<PatronRole, string>> GetUserRoleAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Tenant/{tenantId.Value}/patrons/{userId.Value}/role", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<PatronRole>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> GetAllPublicTenantsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Tenant/public", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Tenant>>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> GetAllowedTenantsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Tenant/allowed", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Tenant>>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> GetMyTenantsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Tenant/my", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Tenant>>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> GetMyOwnedTenantsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Tenant/owned", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Tenant>>(response);
    }

    public async Task<FSharpResult<FSharpList<Tenant>, string>> GetTenantsRequstingPublicAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Tenant/requesting-public", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Tenant>>(response);
    }

    public async Task<FSharpResult<Unit, string>> SetPublicAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/public", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RequestPublicAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/request-public", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SetPrivateAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/private", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }



    public async Task<FSharpResult<Unit, string>> DeleteTenantAsync(Commons.UserContext context, Commons.TenantId tenantId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/Tenant/{tenantId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AddTagAsync(Commons.UserContext context, Commons.TenantId tenantId, Tag tag, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Tenant/{tenantId.Value}/tags", context, tag);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveTagAsync(Commons.UserContext context, Commons.TenantId tenantId, Tag tag, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/Tenant/{tenantId.Value}/tags", context, tag);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> GenerateJoinPinAsync(Commons.UserContext context, Commons.TenantId tenantId, string pin, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Tenant/{tenantId.Value}/join-pin?pin={System.Uri.EscapeDataString(pin)}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SubmitJoinRequestAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Tenant/{tenantId.Value}/join-requests/{userId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ApproveJoinRequestAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/join-requests/{userId.Value}/approve", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RejectJoinRequestAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Tenant/{tenantId.Value}/join-requests/{userId.Value}/reject", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Tenant, string>> FindTenantByJoinPinAsync(string pin, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Tenant/by-pin/{System.Uri.EscapeDataString(pin)}", Commons.UserContext.Anonymous);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Tenant>(response);
    }
}
