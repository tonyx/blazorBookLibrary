namespace BookLibrary.Shared.Services

open System.Threading.Tasks

open System.Threading
open System.Runtime.InteropServices

type AuthorMetadata = {
    Name: string
    Isni: Option<string>
}

type IAuthorsSearchService =
    abstract member LookupByNameAsync : name: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorMetadata, string>>
    abstract member LookupImageUrlByNameAndThumbSizeAsync: name: string * [<Optional; DefaultParameterValue(null)>] ?pitThumbSize: int * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string, string>>