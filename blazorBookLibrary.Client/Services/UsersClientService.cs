
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class UsersClientService : IUserService
{
    private readonly HttpClient _httpClient;

    public UsersClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> CreateUserAsync(Commons.UserContext context, User user, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Users", user, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<User, string>> GetUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Users/{userId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<User>(response);
    }

    public async Task<FSharpResult<BookLibrary.Shared.Details.UserDetails, string>> GetUserDetailsAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Users/{userId.Value}/details", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<BookLibrary.Shared.Details.UserDetails>(response);
    }

    public async Task<FSharpResult<Unit, string>> SetFiscalCodeAsync(Commons.UserContext context, Commons.UserId userId, Commons.FiscalCode fiscalCode, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Users/{userId.Value}/fiscal-code", fiscalCode.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SetNameAsync(Commons.UserContext context, Commons.UserId userId, string name, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Users/{userId.Value}/name", name, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SetSurnameAsync(Commons.UserContext context, Commons.UserId userId, string surname, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Users/{userId.Value}/surname", surname, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SetPhoneNumberAsync(Commons.UserContext context, Commons.UserId userId, Commons.PhoneNumber phoneNumber, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Users/{userId.Value}/phone", phoneNumber.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> SetIsPhysicallyIdentifiedAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Users/{userId.Value}/physically-identified", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> UnSetIsPhysicallyIdentifiedAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/Users/{userId.Value}/physically-identified", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> GhostUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Users/{userId.Value}/ghost", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public Task<FSharpResult<User, string>> GetUser(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct) => GetUserAsync(context, userId, ct);

    public async Task<FSharpResult<Unit, string>> SetAppUserInfoAsync(Commons.UserContext context, Commons.UserId userId, Commons.AppUserInfo appUserInfo, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Users/{userId.Value}/app-user-info", appUserInfo, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<DistributionPoint>, string>> GetDistributionPointsManagedByUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Users/{userId.Value}/managed-distribution-points", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<DistributionPoint>>(response);
        return result.IsOk ? FSharpResult<FSharpList<DistributionPoint>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<DistributionPoint>, string>.NewError(result.ErrorValue);
    }
}
