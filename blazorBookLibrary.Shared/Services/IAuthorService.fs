
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IAuthorService =
    abstract member AddAuthorAsync : author: Author * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member AddAuthorsAsync: authors: List<Author> * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorAsync : id: AuthorId * ?ct: CancellationToken -> Task<Result<Author, string>>
    abstract member GetAuthorsAsync : ids: List<AuthorId> * ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member RenameAsync : authorId: AuthorId * name: Name * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveAsync : authorId: AuthorId * ?ct: CancellationToken -> TaskResult<unit, string>

    abstract member UpdateIsniAsync : authorId: AuthorId * isni: Isni * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member SealAsync : authorId: AuthorId * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UnsealAsync : authorId: AuthorId * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAllAsync: ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByNameAsync: name: Name * ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAsync: strisni: Isni * ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAndNameAsync: isni: Isni * name: Name * ?ct: CancellationToken -> Task<Result<List<Author>, string>>
