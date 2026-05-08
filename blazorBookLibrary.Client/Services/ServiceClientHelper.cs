

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared;

namespace blazorBookLibrary.Client.Services;

public static class ServiceClientHelper
{
    public static readonly JsonSerializerOptions JsonOptions = BookLibrary.Shared.Commons.jsonOptions;

    public static async Task<FSharpResult<T, string>> HandleResponse<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return FSharpResult<T, string>.NewOk(data!);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            return FSharpResult<T, string>.NewError(string.IsNullOrWhiteSpace(error) ? response.ReasonPhrase ?? "Unknown error" : error);
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
            var error = await response.Content.ReadAsStringAsync();
            return FSharpResult<Unit, string>.NewError(string.IsNullOrWhiteSpace(error) ? response.ReasonPhrase ?? "Unknown error" : error);
        }
    }

    public static T GetValue<T>(FSharpOption<T> option, T defaultValue) => 
        (option != null && FSharpOption<T>.get_IsSome(option)) ? option.Value : defaultValue;

}

