
using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared; // Changed from .Commons
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class BookClientService : IBookService
{
    private readonly HttpClient _httpClient;

    public BookClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Unit, string>> AddBookAsync(Commons.UserContext context, Book book, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Books", book, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Unit, string>> AddBooksAsync(Commons.UserContext context, FSharpList<Book> books, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Books/bulk", books.ToList(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleUnitResponse(response);
    }

    public async Task<FSharpResult<Book, string>> GetBookAsync(Commons.UserContext context, Commons.BookId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Books/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Book>(response);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> GetBooksAsync(Commons.UserContext context, FSharpList<Commons.BookId> bookIds, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Books/get-multiple", bookIds.Select(i => i.Value).ToList(), ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> GetAllAsync(Commons.UserContext context, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Books", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAsync(Commons.UserContext context, Commons.Title title, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Books/search/title/{Uri.EscapeDataString(title.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public async Task<FSharpResult<FSharpList<Book>, string>> SearchByIsbnAsync(Commons.UserContext context, Commons.Isbn isbn, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Books/search/isbn/{Uri.EscapeDataString(isbn.Value)}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Book>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Book>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Book>, string>.NewError(result.ErrorValue);
    }

    public Task<FSharpResult<Unit, string>> AddAuthorToBookAsync(Commons.UserContext context, Commons.AuthorId authorId, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveAuthorFromBookAsync(Commons.UserContext context, Commons.AuthorId authorId, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveBookAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveImageUrlAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> SetImageUrlAsync(Commons.UserContext context, Commons.BookId bookId, Uri imageUrl, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> SetAvailabilityAsync(Commons.UserContext context, Commons.Availability availability, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> AddTagToBookAsync(Commons.UserContext context, Tag tag, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveTagFromBookAsync(Commons.UserContext context, Tag tag, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> BulkEditAsync(Commons.UserContext context, FSharpList<Commons.BookId> bookIds, BulkBookEdit editCriteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndIsbnAsync(Commons.UserContext context, Commons.Title title, Commons.Isbn isbn, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> ChangeMainCategoryAsync(Commons.UserContext context, Commons.Category category, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> AddAdditionalCategoryAsync(Commons.UserContext context, Commons.Category category, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveAdditionalCategoryAsync(Commons.UserContext context, Commons.Category category, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UpdateTitleAsync(Commons.UserContext context, Commons.Title title, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UpdateDescriptionAsync(Commons.UserContext context, string description, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveDescriptionAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> EmbedDescriptionAsync(Commons.UserContext context, Commons.BookId bookId, Commons.EmbeddingDataId p2, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> RemoveEmbeddingAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> ForceBulkRemoveEmbeddingsAsync(Commons.UserContext context, FSharpList<Commons.BookId> bookIds, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UpdateIsbnAsync(Commons.UserContext context, Commons.Isbn isbn, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UnsealAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> SealAsync(Commons.UserContext context, Commons.BookId bookId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByYearAsync(Commons.UserContext context, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndYearAsync(Commons.UserContext context, Commons.Title title, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByIsbnAndYearAsync(Commons.UserContext context, Commons.Isbn isbn, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndIsbnAndYearAsync(Commons.UserContext context, Commons.Title title, Commons.Isbn isbn, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByCategoriesAsync(Commons.UserContext context, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByIsbnOrTitleAsync(Commons.UserContext context, Commons.Isbn isbn, Commons.Title title, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndCategoriesAsync(Commons.UserContext context, Commons.Title title, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByYearAndCategoriesAsync(Commons.UserContext context, Commons.YearSearch year, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndYearAndCategoriesAsync(Commons.UserContext context, Commons.Title title, Commons.YearSearch year, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByAuthorAsync(Commons.UserContext context, Commons.AuthorId authorId, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByAuthorsAsync(Commons.UserContext context, FSharpList<Commons.AuthorId> authors, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndAuthorsAsync(Commons.UserContext context, Commons.Title title, FSharpList<Commons.AuthorId> authors, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByAuthorsAndYearAsync(Commons.UserContext context, FSharpList<Commons.AuthorId> authors, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndAuthorsAndYearAsync(Commons.UserContext context, Commons.Title title, FSharpList<Commons.AuthorId> authors, Commons.YearSearch year, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByAuthorsAndCategoriesAsync(Commons.UserContext context, FSharpList<Commons.AuthorId> authors, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndAuthorsAndCategoriesAsync(Commons.UserContext context, Commons.Title title, FSharpList<Commons.AuthorId> authors, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByAuthorsAndYearAndCategoriesAsync(Commons.UserContext context, FSharpList<Commons.AuthorId> authors, Commons.YearSearch year, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<FSharpList<Book>, string>> SearchByTitleAndAuthorsAndYearAndCategoriesAsync(Commons.UserContext context, Commons.Title title, FSharpList<Commons.AuthorId> authors, Commons.YearSearch year, FSharpList<Commons.Category> categories, FSharpOption<BookSearchCriteria> criteria, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<bool, string>> LoanedByUserAtLeastOnceAsync(Commons.UserContext context, Commons.BookId bookId, Commons.UserId userId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> SetDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId distributionPointId, Commons.BookId bookId, Commons.UserId userId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UnSetDistributionPointAsync(Commons.UserContext context, Commons.DistributionPointId distributionPointId, Commons.BookId bookId, Commons.UserId userId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> UnsetAllBookRelatedToDPAsync(Commons.UserContext context, Commons.DistributionPointId distributionPointId, Commons.UserId userId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
    public Task<FSharpResult<Unit, string>> MoveFromDpToAnotherDPAsync(Commons.UserContext context, Commons.DistributionPointId fromPoint, Commons.DistributionPointId toPoint, Commons.BookId bookId, Commons.UserId userId, FSharpOption<CancellationToken> ct) => throw new NotImplementedException();
}
