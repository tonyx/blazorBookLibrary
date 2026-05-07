
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
    abstract member AddAuthorAsync : context: UserContext * author: Author * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member AddAuthorsAsync: context: UserContext * authors: List<Author> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorAsync : context: UserContext * id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Author, string>>
    abstract member GetAuthorsAsync : context: UserContext * ids: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member RenameAsync : context: UserContext * authorId: AuthorId * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveAsync : context: UserContext * authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAuthorDetailsAsync : context: UserContext * id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorDetails, string>>
    abstract member UpdateImageUrlAsync : context: UserContext * authorId: AuthorId * imageUrl: Uri * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveImageUrlAsync : context: UserContext * authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>

    abstract member UpdateIsniAsync : context: UserContext * authorId: AuthorId * isni: Isni * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UpdateBioAsync : context: UserContext * authorId: AuthorId * bio: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UpdateWikipediaUriAsync : context: UserContext * authorId: AuthorId * wikipediaUri: Uri * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member SealAsync : context: UserContext * authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member UnsealAsync : context: UserContext * authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetAllAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByNameAsync: context: UserContext * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAsync: context: UserContext * strisni: Isni * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
    abstract member SearchByIsniAndNameAsync: context: UserContext * isni: Isni * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Author>, string>>
