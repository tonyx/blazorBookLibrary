

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared;

namespace blazorBookLibrary.Client.Services;

public static class ServiceClientHelper
{
    public static readonly JsonSerializerOptions JsonOptions = BookLibrary.Shared.Commons.jsonOptions;

    public static event Action<AiRateLimitNotification>? OnRateLimited;

    private static async Task<string> ProcessErrorResponse(HttpResponseMessage response)
    {
        var error = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var isGuest = true;
            var retryAfterSeconds = 60;
            var friendlyMessage = "Rate limit reached. Please wait before retrying.";

            if (response.Headers.RetryAfter?.Delta.HasValue == true)
            {
                retryAfterSeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                try
                {
                    using var doc = JsonDocument.Parse(error);
                    if (doc.RootElement.TryGetProperty("isGuest", out var isGuestProp))
                    {
                        isGuest = isGuestProp.GetBoolean();
                    }
                    if (doc.RootElement.TryGetProperty("retryAfterSeconds", out var retryProp))
                    {
                        retryAfterSeconds = retryProp.GetInt32();
                    }
                    if (doc.RootElement.TryGetProperty("error", out var errorProp) && errorProp.GetString() is { } msg)
                    {
                        friendlyMessage = msg;
                    }
                }
                catch
                {
                    // Fall back to defaults if not JSON
                }
            }

            try
            {
                OnRateLimited?.Invoke(new AiRateLimitNotification(isGuest, retryAfterSeconds, friendlyMessage));
            }
            catch
            {
                // Prevent listener errors from failing HTTP response handling
            }

            return friendlyMessage;
        }

        return string.IsNullOrWhiteSpace(error) ? response.ReasonPhrase ?? "Unknown error" : error;
    }

    public static async Task<FSharpResult<T, string>> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return FSharpResult<T, string>.NewOk(default!);
            }

            if (typeof(T) == typeof(string))
            {
                return (FSharpResult<T, string>)(object)FSharpResult<string, string>.NewOk(content);
            }

            try
            {
                var data = JsonSerializer.Deserialize<T>(content, JsonOptions);
                return FSharpResult<T, string>.NewOk(data!);
            }
            catch (JsonException)
            {
                // Fallback for FSharpOption<string> if it's a raw string instead of a JSON-quoted string
                if (typeof(T) == typeof(FSharpOption<string>))
                {
                    return (FSharpResult<T, string>)(object)FSharpResult<FSharpOption<string>, string>.NewOk(FSharpOption<string>.Some(content));
                }
                throw;
            }
        }
        else
        {
            var error = await ProcessErrorResponse(response);
            return FSharpResult<T, string>.NewError(error);
        }
    }

    public static async Task<FSharpResult<Unit, string>> HandleUnitResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return FSharpResult<Unit, string>.NewOk(null!);
        }
        else
        {
            var error = await ProcessErrorResponse(response);
            return FSharpResult<Unit, string>.NewError(error);
        }
    }

    public static T GetValue<T>(FSharpOption<T> option, T defaultValue) => 
        (option != null && FSharpOption<T>.get_IsSome(option)) ? option.Value : defaultValue;
    
    public static HttpRequestMessage CreateRequest(HttpMethod method, string uri, Commons.UserContext context)
    {
        var request = new HttpRequestMessage(method, uri);
        if (context != null && context.TenantId != null && FSharpOption<Commons.TenantId>.get_IsSome(context.TenantId))
        {
            request.Headers.Add("X-Tenant-Id", context.TenantId.Value.Value.ToString());
        }
        return request;
    }

    public static HttpRequestMessage CreateRequest<T>(HttpMethod method, string uri, Commons.UserContext context, T content)
    {
        var request = CreateRequest(method, uri, context);
        request.Content = JsonContent.Create(content, options: JsonOptions);
        return request;
    }

}

