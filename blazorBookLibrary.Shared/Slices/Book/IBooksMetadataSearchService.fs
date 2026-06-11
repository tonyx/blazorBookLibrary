namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons

type GoogleBookMetadata =
    { Title: string
      Authors: List<string>
      Categories: List<string>
      Year: int option
      Isbn: string option
      Description: string option }

type IBooksMetadataSearchService =
    abstract member LookupByIsbnAsync:
        context: UserContext * isbn: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<GoogleBookMetadata option, string>>

    abstract member LookupByTitleAsync:
        context: UserContext * title: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<GoogleBookMetadata option, string>>

    abstract member LookupMultipleByTitleAsync:
        context: UserContext * title: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<GoogleBookMetadata list, string>>

    abstract member LookupCoverImageByIsbnAsync:
        context: UserContext *
        isbn: Isbn *
        [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string option, string>>

    abstract member LookupGoogleApiCoverImageByIsbnAsync:
        context: UserContext * isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string option, string>>

    abstract member LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync:
        context: UserContext *
        isbn: Isbn *
        [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string option, string>>

    abstract member LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync:
        context: UserContext *
        title: string *
        [<Optional; DefaultParameterValue(null)>] ?author: string *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string option, string>>
