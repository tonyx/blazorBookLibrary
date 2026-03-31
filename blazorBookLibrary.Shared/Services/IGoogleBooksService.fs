namespace BookLibrary.Shared.Services

open System.Threading.Tasks
open System.Collections.Generic
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
    abstract member LookupByIsbnAsync : isbn: string -> Task<Result<GoogleBookMetadata option, string>>
    abstract member LookupByTitleAsync : title: string -> Task<Result<GoogleBookMetadata option, string>>
    abstract member LookupMultipleByTitleAsync : title: string -> Task<Result<GoogleBookMetadata list, string>>
    abstract member LookupCoverImageByIsbnAsync : isbn: Isbn * ?thumbRoughSize: ThumbRoughSize -> Task<Result<string option, string>>
    abstract member LookupGoogleApiCoverImageByIsbnAsync : isbn: Isbn -> Task<Result<string option, string>>
    abstract member LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync : isbn: Isbn * ?thumbRoughSize: ThumbRoughSize -> Task<Result<string option, string>>
    abstract member LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync : title: string * ?author: string -> Task<Result<string option, string>>
