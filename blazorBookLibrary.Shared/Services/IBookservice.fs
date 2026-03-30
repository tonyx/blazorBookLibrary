namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks

open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type BookSearchCriteria = delegate of Book -> bool

type IBookService =
    abstract member AddBookAsync : book: Book * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAuthorToBookAsync : authorId: AuthorId * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAuthorFromBookAsync : authorId: AuthorId * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveBookAsync : bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetBookAsync : id: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Book, string>>
    abstract member GetBookDetailsAsync : bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<BookDetails, string>>
    abstract member GetBooksDetailsAsync: List<BookId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<BookDetails>, string>>
    abstract member RemoveImageUrlAsync: bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SetImageUrlAsync: bookId: BookId * imageUrl: Uri * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member GetAllAsync : [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Book list, string>>
    abstract member SearchByTitleAsync : title: Title * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAsync : isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAndIsbnAsync : title: Title * isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member ChangeMainCategoryAsync : category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAdditionalCategoryAsync : category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAdditionalCategoryAsync : category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateTitleAsync: title: Title * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateIsbnAsync: isbn: Isbn * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UnsealAsync : bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SealAsync : bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member SearchByYearAsync: year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAndYearAsync: title: Title * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAndYearAsync: isbn: Isbn * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndIsbnAndYearAsync: title: Title * isbn: Isbn * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByCategoriesAsync: categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByIsbnOrTitleAsync: isbn: Isbn * title: Title * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAndCategoriesAsync: title: Title * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByYearAndCategoriesAsync: year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndYearAndCategoriesAsync: title: Title * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorAsync: authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAsync: authors: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAsync: title: Title * authors: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAsync: authors: List<AuthorId> * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAsync: title: Title * authors: List<AuthorId> * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndCategoriesAsync: authors: List<AuthorId> * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndCategoriesAsync: title: Title * authors: List<AuthorId> * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAndCategoriesAsync: authors: List<AuthorId> * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAndCategoriesAsync: title: Title * authors: List<AuthorId> * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    

