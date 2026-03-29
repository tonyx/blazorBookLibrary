
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IAuthorService =
    abstract member AddAuthorAsync : author: Author * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member AddAuthorsAsync: authors: List<Author> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorAsync : id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Author, string>>
    abstract member GetAuthorsAsync : ids: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member RenameAsync : authorId: AuthorId * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveAsync : authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorDetailsAsync : id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorDetails, string>>
    abstract member UpdateImageUrlAsync : authorId: AuthorId * imageUrl: Uri * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveImageUrlAsync : authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>

    abstract member UpdateIsniAsync : authorId: AuthorId * isni: Isni * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member SealAsync : authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UnsealAsync : authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAllAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByNameAsync: name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAsync: strisni: Isni * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAndNameAsync: isni: Isni * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
