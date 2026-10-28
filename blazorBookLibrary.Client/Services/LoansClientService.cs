
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class LoansClientService : ILoanService
{
    private readonly HttpClient _httpClient;

    public LoansClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> AddLoanAsync(Commons.UserContext context, Loan loan, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/Loans", context, loan);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Loan, string>> GetLoanAsync(Commons.UserContext context, Commons.LoanId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Loans/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Loan>(response);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetLoansAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Loans", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetUnarchivedLoansAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Loans/unarchived", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> ReleaseLoanAsync(Commons.UserContext context, Commons.LoanId loanId, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Loans/release/{loanId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> TransformReservationIntoLoanAsync(Commons.UserContext context, Commons.ReservationId reservationId, Commons.ReservationCode providedReservationCode, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Loans/transform-reservation/{reservationId.Value}", context, providedReservationCode.Value);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> TransformReservationIntoLoanByPinAsync(Commons.UserContext context, Commons.ReservationId reservationId, string pin, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Loans/transform-reservation-by-pin/{reservationId.Value}", context, pin);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> TransformReservationIntoLoanByManagerAsync(Commons.UserContext context, Commons.ReservationId reservationId, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Loans/transform-reservation-by-manager/{reservationId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetHistoryLoansOfUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Loans/history/{userId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetLoansOfUserInATenantAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Loans/tenant/{tenantId.Value}/user/{userId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> RemoveLoanAsync(Commons.UserContext context, Commons.LoanId loanId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Delete, $"api/Loans/{loanId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ArchiveLoanAsync(Commons.UserContext context, Commons.LoanId loanId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, $"api/Loans/archive/{loanId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
