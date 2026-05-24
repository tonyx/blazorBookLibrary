using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class NotificationClientService : INotificationService
{
    private readonly HttpClient _httpClient;

    public NotificationClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<FSharpList<Notification>, string>> GetUnreadNotificationsForUserAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Notifications/unread", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Notification>>(response);
    }

    public async Task<FSharpResult<FSharpList<Notification>, string>> GetAllNotificationsForUserAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Notifications/all", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<FSharpList<Notification>>(response);
    }

    public async Task<FSharpResult<Unit, string>> MarkAsReadAsync(Commons.UserContext context, NotificationId notificationId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Put, $"api/Notifications/{notificationId.Value}/read", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> CreateNotificationAsync(Commons.UserContext context, Notification notification, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/Notifications", context, notification);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
