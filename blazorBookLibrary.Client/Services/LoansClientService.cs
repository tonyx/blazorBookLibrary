
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
        var response = await _httpClient.PostAsJsonAsync("api/Loans", loan, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Loan, string>> GetLoanAsync(Commons.UserContext context, Commons.LoanId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Loans/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Loan>(response);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetLoansAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Loans", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetUnarchivedLoansAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Loans/unarchived", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> ReleaseLoanAsync(Commons.UserContext context, Commons.LoanId loanId, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Loans/release/{loanId.Value}", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> TransformReservationIntoLoanAsync(Commons.UserContext context, Commons.ReservationId reservationId, Commons.ReservationCode providedReservationCode, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Loans/transform-reservation/{reservationId.Value}", providedReservationCode.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> TransformReservationIntoLoanByPinAsync(Commons.UserContext context, Commons.ReservationId reservationId, string pin, DateTime date, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Loans/transform-reservation-by-pin/{reservationId.Value}", pin, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }


    public async Task<FSharpResult<FSharpList<Loan>, string>> GetHistoryLoansOfUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Loans/history/{userId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Loan>, string>> GetLoansOfUserInATenantAsync(Commons.UserContext context, Commons.TenantId tenantId, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Loans/tenant/{tenantId.Value}/user/{userId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Loan>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Loan>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Loan>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> RemoveLoanAsync(Commons.UserContext context, Commons.LoanId loanId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.DeleteAsync($"api/Loans/{loanId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ArchiveLoanAsync(Commons.UserContext context, Commons.LoanId loanId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Loans/archive/{loanId.Value}", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }
}
