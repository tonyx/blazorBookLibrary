
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class ReviewsClientService : IReviewService
{
    private readonly HttpClient _httpClient;

    public ReviewsClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Review, string>> GetReviewAsync(Commons.UserContext context, Commons.ReviewId commentId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reviews/{commentId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Review>(response);
    }

    public async Task<FSharpResult<FSharpList<Review>, string>> GetAllReviewsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Reviews", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Review>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Review>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Review>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Review>, string>> GetPendingReviewsAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Reviews/pending", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Review>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Review>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Review>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<Unit, string>> AddReviewAsync(Commons.UserContext context, Review review, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Reviews", review, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> EditReviewAsync(Commons.UserContext context, Commons.ReviewId reviewId, string editedComment, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Reviews/{reviewId.Value}/edit", editedComment, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ApproveAsync(Commons.UserContext context, Commons.ReviewId reviewId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Reviews/{reviewId.Value}/approve", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> RejectAsync(Commons.UserContext context, Commons.ReviewId reviewId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Reviews/{reviewId.Value}/reject", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> ShowAsync(Commons.UserContext context, Commons.ReviewId reviewId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Reviews/{reviewId.Value}/show", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> HideAsync(Commons.UserContext context, Commons.ReviewId reviewId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsync($"api/Reviews/{reviewId.Value}/hide", null, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>> GetReviewsOfBookAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reviews/book/{bookId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tuple<Commons.AppUserInfo, Review>>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>> GetApprovedVisibleReviewsOfBookAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reviews/book/{bookId.Value}/visible", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tuple<Commons.AppUserInfo, Review>>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tuple<Commons.AppUserInfo, Review>>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Tuple<Book, Review>>, string>> GetReviewsOfUserAsync(Commons.UserContext context, Commons.UserId userId, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Reviews/user/{userId.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Tuple<Book, Review>>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Tuple<Book, Review>>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Tuple<Book, Review>>, string>.NewError(result.ErrorValue);
    }
}
