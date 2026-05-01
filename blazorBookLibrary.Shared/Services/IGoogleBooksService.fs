namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons

type GoogleBookMetadata = {
    Title: string
    Authors: List<string>
    Categories: List<string>
    Year: int option
    Isbn: string option
    Description: string option
}

type IGoogleBooksService =
    abstract member LookupByIsbnAsync : isbn: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<GoogleBookMetadata option, string>>
    abstract member LookupByTitleAsync : title: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<GoogleBookMetadata option, string>>
    abstract member LookupMultipleByTitleAsync : title: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<GoogleBookMetadata list, string>>
    abstract member LookupCoverImageByIsbnAsync : isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string option, string>>
    abstract member LookupGoogleApiCoverImageByIsbnAsync : isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string option, string>>
    abstract member LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync : isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string option, string>>
    abstract member LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync : title: string * [<Optional; DefaultParameterValue(null)>] ?author: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string option, string>>
