
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open System.Collections.Generic


type SearchTitleYearRequest = { Title: string; Year: YearSearch }
type SearchIsbnYearRequest = { Isbn: string; Year: YearSearch }
type SearchTitleIsbnYearRequest = { Title: string; Isbn: string; Year: YearSearch }
type SearchTitleCategoriesRequest = { Title: string; Categories: List<string> }
type SearchYearCategoriesRequest = { Year: YearSearch; Categories: List<string> }
type SearchTitleYearCategoriesRequest = { Title: string; Year: YearSearch; Categories: List<string> }
type SearchTitleAuthorsRequest = { Title: string; Authors: List<Guid> }
type SearchAuthorsYearRequest = { Authors: List<Guid>; Year: YearSearch }
type SearchTitleAuthorsYearRequest = { Title: string; Authors: List<Guid>; Year: YearSearch }
type SearchAuthorsCategoriesRequest = { Authors: List<Guid>; Categories: List<string> }
type SearchTitleAuthorsCategoriesRequest = { Title: string; Authors: List<Guid>; Categories: List<string> }
type SearchAuthorsYearCategoriesRequest = { Authors: List<Guid>; Year: YearSearch; Categories: List<string> }
type SearchTitleAuthorsYearCategoriesRequest = { Title: string; Authors: List<Guid>; Year: YearSearch; Categories: List<string> }
type BulkEditRequest = { BookIds: List<Guid>; EditCriteria: BulkBookEdit }

[<ApiController>]
[<Route("api/[controller]")>]
type BooksController(bookService: IBookService) =
    inherit ControllerBase()


    [<HttpGet("{id}")>]
    member this.GetBook(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.GetBookAsync(context, BookId id)
            match result with
            | Ok book -> return this.Ok(book) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddBook(book: Book) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.AddBookAsync(context, book)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("bulk")>]
    member this.AddBooks(books: List<Book>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsBooks = List.ofSeq books
            let! result = bookService.AddBooksAsync(context, fsBooks)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet>]
    member this.GetAllBooks() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.GetAllAsync(context)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("get-multiple")>]
    member this.GetBooks([<FromBody>] ids: List<Guid>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsBookIds = List.ofSeq (ids |> Seq.map BookId)
            let! result = bookService.GetBooksAsync(context, fsBookIds)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/title/{title}")>]
    member this.SearchByTitle(title: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByTitleAsync(context, Title.New title)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/isbn/{isbn}")>]
    member this.SearchByIsbn(isbn: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByIsbnAsync(context, Isbn isbn)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}")>]
    member this.RemoveBook(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveBookAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/authors/{authorId}")>]
    member this.AddAuthorToBook(id: Guid, authorId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.AddAuthorToBookAsync(context, AuthorId authorId, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/authors/{authorId}")>]
    member this.RemoveAuthorFromBook(id: Guid, authorId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveAuthorFromBookAsync(context, AuthorId authorId, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/image")>]
    member this.RemoveImageUrl(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveImageUrlAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/image")>]
    member this.SetImageUrl(id: Guid, [<FromBody>] imageUrl: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SetImageUrlAsync(context, BookId id, Uri imageUrl)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/availability")>]
    member this.SetAvailability(id: Guid, [<FromBody>] availability: Availability) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SetAvailabilityAsync(context, availability, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/tags")>]
    member this.AddTagToBook(id: Guid, [<FromBody>] tag: Tag) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.AddTagToBookAsync(context, tag, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/tags/remove")>]
    member this.RemoveTagFromBook(id: Guid, [<FromBody>] tag: Tag) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveTagFromBookAsync(context, tag, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("bulk-edit")>]
    member this.BulkEdit([<FromBody>] request: BulkEditRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let bookIds = request.BookIds |> List.ofSeq |> List.map BookId
            let! result = bookService.BulkEditAsync(context, bookIds, request.EditCriteria)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/main-category")>]
    member this.ChangeMainCategory(id: Guid, [<FromBody>] category: Category) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.ChangeMainCategoryAsync(context, category, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/additional-categories")>]
    member this.AddAdditionalCategory(id: Guid, [<FromBody>] category: Category) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.AddAdditionalCategoryAsync(context, category, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/additional-categories/remove")>]
    member this.RemoveAdditionalCategory(id: Guid, [<FromBody>] category: Category) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveAdditionalCategoryAsync(context, category, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/title")>]
    member this.UpdateTitle(id: Guid, [<FromBody>] title: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UpdateTitleAsync(context, Title.New title, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/description")>]
    member this.UpdateDescription(id: Guid, [<FromBody>] description: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UpdateDescriptionAsync(context, description, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/description")>]
    member this.RemoveDescription(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveDescriptionAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/embedding")>]
    member this.EmbedDescription(id: Guid, [<FromBody>] embeddingId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.EmbedDescriptionAsync(context, BookId id, EmbeddingDataId embeddingId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/embedding")>]
    member this.RemoveEmbedding(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.RemoveEmbeddingAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("bulk-remove-embeddings")>]
    member this.ForceBulkRemoveEmbeddings([<FromBody>] ids: List<Guid>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let bookIds = ids |> List.ofSeq |> List.map BookId
            let! result = bookService.ForceBulkRemoveEmbeddingsAsync(context, bookIds)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/isbn")>]
    member this.UpdateIsbn(id: Guid, [<FromBody>] isbn: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UpdateIsbnAsync(context, Isbn isbn, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/unseal")>]
    member this.Unseal(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UnsealAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/seal")>]
    member this.Seal(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SealAsync(context, BookId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}/loaned-at-least-once/{userId}")>]
    member this.LoanedByUserAtLeastOnce(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.LoanedByUserAtLeastOnceAsync(context, BookId id, UserId userId)
            match result with
            | Ok res -> return this.Ok(res) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/distribution-point/{dpId}/{userId}")>]
    member this.SetDistributionPoint(id: Guid, dpId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SetDistributionPointAsync(context, DistributionPointId dpId, BookId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/distribution-point/{dpId}/{userId}")>]
    member this.UnSetDistributionPoint(id: Guid, dpId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UnSetDistributionPointAsync(context, DistributionPointId dpId, BookId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("distribution-point/{dpId}/{userId}")>]
    member this.UnsetAllBookRelatedToDP(dpId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.UnsetAllBookRelatedToDPAsync(context, DistributionPointId dpId, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("move-distribution-point/{fromDpId}/{toDpId}/{userId}")>]
    member this.MoveFromDpToAnotherDP(fromDpId: Guid, toDpId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.MoveFromDpToAnotherDPAsync(context, DistributionPointId fromDpId, DistributionPointId toDpId, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/title-isbn")>]
    member this.SearchByTitleAndIsbn(title: string, isbn: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByTitleAndIsbnAsync(context, Title.New title, Isbn isbn)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/year")>]
    member this.SearchByYear([<FromBody>] year: YearSearch) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByYearAsync(context, year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-year")>]
    member this.SearchByTitleAndYear([<FromBody>] request: SearchTitleYearRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByTitleAndYearAsync(context, Title.New request.Title, request.Year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/isbn-year")>]
    member this.SearchByIsbnAndYear([<FromBody>] request: SearchIsbnYearRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByIsbnAndYearAsync(context, Isbn request.Isbn, request.Year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-isbn-year")>]
    member this.SearchByTitleAndIsbnAndYear([<FromBody>] request: SearchTitleIsbnYearRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByTitleAndIsbnAndYearAsync(context, Title.New request.Title, Isbn request.Isbn, request.Year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/categories")>]
    member this.SearchByCategories([<FromBody>] categories: List<string>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsCategories = categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByCategoriesAsync(context, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/isbn-or-title")>]
    member this.SearchByIsbnOrTitle(isbn: string, title: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByIsbnOrTitleAsync(context, Isbn isbn, Title.New title)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-categories")>]
    member this.SearchByTitleAndCategories([<FromBody>] request: SearchTitleCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByTitleAndCategoriesAsync(context, Title.New request.Title, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/year-categories")>]
    member this.SearchByYearAndCategories([<FromBody>] request: SearchYearCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByYearAndCategoriesAsync(context, request.Year, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-year-categories")>]
    member this.SearchByTitleAndYearAndCategories([<FromBody>] request: SearchTitleYearCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByTitleAndYearAndCategoriesAsync(context, Title.New request.Title, request.Year, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/author/{authorId}")>]
    member this.SearchByAuthor(authorId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = bookService.SearchByAuthorAsync(context, AuthorId authorId)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/authors")>]
    member this.SearchByAuthors([<FromBody>] authors: List<Guid>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = authors |> List.ofSeq |> List.map AuthorId
            let! result = bookService.SearchByAuthorsAsync(context, fsAuthors)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-authors")>]
    member this.SearchByTitleAndAuthors([<FromBody>] request: SearchTitleAuthorsRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let! result = bookService.SearchByTitleAndAuthorsAsync(context, Title.New request.Title, fsAuthors)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/authors-year")>]
    member this.SearchByAuthorsAndYear([<FromBody>] request: SearchAuthorsYearRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let! result = bookService.SearchByAuthorsAndYearAsync(context, fsAuthors, request.Year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-authors-year")>]
    member this.SearchByTitleAndAuthorsAndYear([<FromBody>] request: SearchTitleAuthorsYearRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let! result = bookService.SearchByTitleAndAuthorsAndYearAsync(context, Title.New request.Title, fsAuthors, request.Year)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/authors-categories")>]
    member this.SearchByAuthorsAndCategories([<FromBody>] request: SearchAuthorsCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByAuthorsAndCategoriesAsync(context, fsAuthors, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-authors-categories")>]
    member this.SearchByTitleAndAuthorsAndCategories([<FromBody>] request: SearchTitleAuthorsCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByTitleAndAuthorsAndCategoriesAsync(context, Title.New request.Title, fsAuthors, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/authors-year-categories")>]
    member this.SearchByAuthorsAndYearAndCategories([<FromBody>] request: SearchAuthorsYearCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByAuthorsAndYearAndCategoriesAsync(context, fsAuthors, request.Year, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search/title-authors-year-categories")>]
    member this.SearchByTitleAndAuthorsAndYearAndCategories([<FromBody>] request: SearchTitleAuthorsYearCategoriesRequest) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let fsAuthors = request.Authors |> List.ofSeq |> List.map AuthorId
            let fsCategories = request.Categories |> List.ofSeq |> List.map Category.New
            let! result = bookService.SearchByTitleAndAuthorsAndYearAndCategoriesAsync(context, Title.New request.Title, fsAuthors, request.Year, fsCategories)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

