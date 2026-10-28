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
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/book/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<BookDetails>(response);
    }

    public async Task<FSharpResult<LoanDetails, string>> GetLoanDetailsAsync(Commons.UserContext context, LoanId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/loan/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<LoanDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<LoanDetails>, string>> GetAllLoansDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Details/loans", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<LoanDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<LoanDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<LoanDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<ReservationDetails, string>> GetReservationDetailsAsync(Commons.UserContext context, ReservationId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/reservation/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<ReservationDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<ReservationDetails>, string>> GetAllPendingReservationsDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Details/reservations/pending", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReservationDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReservationDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReservationDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<UserDetails, string>> GetUserDetailsAsync(Commons.UserContext context, UserId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/user/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<UserDetails>(response);
    }

    public async Task<FSharpResult<AuthorDetails, string>> GetAuthorDetailsAsync(Commons.UserContext context, AuthorId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/author/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<AuthorDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<AuthorDetails>, string>> GetAuthorsDetailsAsync(Commons.UserContext context, FSharpList<AuthorId> ids, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Post, "api/Details/authors", context, ids.Select(i => i.Value).ToList());
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<AuthorDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<AuthorDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<AuthorDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<ReviewDetails, string>> GetReviewDetailsAsync(Commons.UserContext context, ReviewId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/review/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<ReviewDetails>(response);
    }

    public async Task<FSharpResult<FSharpList<ReviewDetails>, string>> GetAllReviewsDetailsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, "api/Details/reviews", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReviewDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReviewDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReviewDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<ReviewDetails>, string>> GetApprovedVisibleReviewsOfBookAsync(Commons.UserContext context, BookId bookId, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/reviews/book/{bookId.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<ReviewDetails>>(response);
        return result.IsOk ? FSharpResult<FSharpList<ReviewDetails>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<ReviewDetails>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<TenantDetails, string>> GetTenantDetailsAsync(Commons.UserContext context, TenantId id, FSharpOption<CancellationToken> ct)
    {
        var request = ServiceClientHelper.CreateRequest(HttpMethod.Get, $"api/Details/tenant/{id.Value}", context);
        var response = await _httpClient.SendAsync(request, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<TenantDetails>(response);
    }
}
