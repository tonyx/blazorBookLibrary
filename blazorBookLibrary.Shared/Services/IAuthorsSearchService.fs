namespace BookLibrary.Shared.Services

open System.Threading.Tasks

open System.Threading
open System.Runtime.InteropServices

open BookLibrary.Shared.Commons

type AuthorMetadata = {
    Name: string
    Isni: Option<string>
}

type IAuthorsSearchService =
    abstract member LookupByNameAsync : name: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorMetadata, string>>
    abstract member LookupImageUrlByNameAndThumbSizeAsync: name: string * [<Optional; DefaultParameterValue(null)>] ?pitThumbSize: int * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string, string>>
    abstract member LookupBioByNameAsync: name: string * [<Optional; DefaultParameterValue(null)>] ?lang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<string>, string>>
    abstract member LookupWikipediaUriByNameAsync: name: string * [<Optional; DefaultParameterValue(null)>] ?lang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string, string>>