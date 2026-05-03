
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open blazorBookLibrary.Data

type ITagService = 
    abstract member GetTagsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetBookTypeTagsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetAuthorTypeTagsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetGeneralTypeTagsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member GetPersonTypeTagsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<list<Tag>, string>>
    abstract member AddTagAsync: Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveTagAsync: Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member ReplaceTagAsync: Tag * Tag * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member EnsureTagsRepoCreatedAsync : [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>