using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using static BookLibrary.Shared.Details;
using static BookLibrary.Shared.Commons;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;
using System.Collections.Generic;

namespace blazorBookLibrary.Client.Services;

public class DetailsClientService : IDetailsService
{
    private readonly HttpClient _httpClient;

    public DetailsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<BookDetails, string>> GetBookDetailsAsync(Commons.UserContext context, BookId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/book/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<BookDetails>(response);
    }

    public async Task<FSharpResult<LoanDetails, string>> GetLoanDetailsAsync(Commons.UserContext context, LoanId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/loan/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<LoanDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<LoanDetails>, string>> GetAllLoansDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Details/loans", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<LoanDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<LoanDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<LoanDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<ReservationDetails, string>> GetReservationDetailsAsync(Commons.UserContext context, ReservationId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/reservation/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<ReservationDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<ReservationDetails>, string>> GetAllPendingReservationsDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Details/reservations/pending", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReservationDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReservationDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReservationDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<UserDetails, string>> GetUserDetailsAsync(Commons.UserContext context, UserId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/user/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<UserDetails>(response);
    }

    public async Task<FSharpResult<AuthorDetails, string>> GetAuthorDetailsAsync(Commons.UserContext context, AuthorId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/author/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<AuthorDetails>(response);
    }

    public async Task<FSharpResult<ReviewDetails, string>> GetReviewDetailsAsync(Commons.UserContext context, ReviewId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/review/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<ReviewDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<ReviewDetails>, string>> GetAllReviewsDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Details/reviews", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReviewDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReviewDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReviewDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<ReviewDetails>, string>> GetApprovedVisibleReviewsOfBookAsync(Commons.UserContext context, BookId bookId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/reviews/book/{bookId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReviewDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReviewDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReviewDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<TenantDetails, string>> GetTenantDetailsAsync(Commons.UserContext context, TenantId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Details/tenant/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<TenantDetails>(response);
    }
}
