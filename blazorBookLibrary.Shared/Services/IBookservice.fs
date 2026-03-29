namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IBookService =
    abstract member AddBookAsync : book: Book * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAuthorToBookAsync : authorId: AuthorId * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAuthorFromBookAsync : authorId: AuthorId * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveBookAsync : bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetBookAsync : id: BookId * ?ct: CancellationToken -> Task<Result<Book, string>>
    abstract member GetBookDetailsAsync : bookId: BookId * ?ct: CancellationToken -> Task<Result<BookDetails, string>>
    abstract member GetBooksDetailsAsync: List<BookId> * ?ct: CancellationToken -> Task<Result<List<BookDetails>, string>>
    abstract member GetAllAsync : ?ct: CancellationToken -> Task<Result<Book list, string>>
    abstract member SearchByTitleAsync : title: Title * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAsync : isbn: Isbn * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndIsbnAsync : title: Title * isbn: Isbn * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member ChangeMainCategoryAsync : category: Category * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAdditionalCategoryAsync : category: Category * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAdditionalCategoryAsync : category: Category * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateTitleAsync: title: Title * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UnsealAsync : bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SealAsync : bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member SearchByYearAsync: year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndYearAsync: title: Title * year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAndYearAsync: isbn: Isbn * year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndIsbnAndYearAsync: title: Title * isbn: Isbn * year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByCategoriesAsync: categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnOrTitleAsync: isbn: Isbn * title: Title * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndCategoriesAsync: title: Title * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByYearAndCategoriesAsync: year: YearSearch * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndYearAndCategoriesAsync: title: Title * year: YearSearch * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorAsync: authorId: AuthorId * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAsync: authors: List<AuthorId> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAsync: title: Title * authors: List<AuthorId> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAsync: authors: List<AuthorId> * year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAsync: title: Title * authors: List<AuthorId> * year: YearSearch * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndCategoriesAsync: authors: List<AuthorId> * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndCategoriesAsync: title: Title * authors: List<AuthorId> * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAndCategoriesAsync: authors: List<AuthorId> * year: YearSearch * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAndCategoriesAsync: title: Title * authors: List<AuthorId> * year: YearSearch * categories: List<Category> * ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    

