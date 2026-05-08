
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class ReservationsClientService : IReservationService
{
    private readonly HttpClient _httpClient;

    public ReservationsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> AddReservationAsync(Commons.UserContext context, Reservation reservation, Commons.ShortLang shortLang, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Reservations?lang={shortLang.Value}", reservation, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Reservation, string>> GetReservationAsync(Commons.UserContext context, Commons.ReservationId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reservations/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Reservation>(response);
    }

    public async Task<FSharpResult<Unit, string>> RemoveReservationAsync(Commons.UserContext context, Commons.ReservationId reservationId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/Reservations/{reservationId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<Reservation>, string>> GetReservationsAsync(Commons.UserContext context, FSharpList<Commons.ReservationId> ids, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Reservations/get-multiple", ids.Select(i => i.Value).ToList(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Reservation>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Reservation>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Reservation>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<BookLibrary.Shared.Details.ReservationDetails, string>> GetReservationDetailsAsync(Commons.UserContext context, Commons.ReservationId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reservations/{id.Value}/details", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<BookLibrary.Shared.Details.ReservationDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<BookLibrary.Shared.Details.ReservationDetails>, string>> GetAllPendingReservationsDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Reservations/pending/details", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<BookLibrary.Shared.Details.ReservationDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<BookLibrary.Shared.Details.ReservationDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<BookLibrary.Shared.Details.ReservationDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> RemoveExpiredReservationsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync("api/Reservations/expired/remove", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
