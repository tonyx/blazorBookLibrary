namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Definitions
open Sharpino.Core
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IBookService =
    abstract member AddBookAsync : book: Book * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAuthorToBookAsync : authorId: AuthorId * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAuthorFromBookAsync : authorId: AuthorId * bookId: BookId * ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetBookAsync : id: BookId * ?ct: CancellationToken -> Task<Result<Book, string>>
    abstract member GetBookDetailsAsync : bookId: BookId * ?ct: CancellationToken -> Task<Result<BookDetails, string>>
    
