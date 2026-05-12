
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type ITagService = 
    abstract member GetTagsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetBookTypeTagsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetAuthorTypeTagsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetGeneralTypeTagsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetPersonTypeTagsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member AddTagAsync: UserContext * Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveTagAsync: UserContext * Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member ReplaceTagAsync: UserContext * Tag * Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member EnsureTagsRepoCreatedAsync : [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>