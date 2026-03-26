namespace BookLibrary.Shared.Services

open System.Threading.Tasks
open System.Collections.Generic

type GoogleBookMetadata = {
    Title: string
    Authors: List<string>
    Categories: List<string>
    Year: int option
    Isbn: string option
}

type IGoogleBooksService =
    abstract member LookupByIsbnAsync : isbn: string -> Task<Result<GoogleBookMetadata option, string>>
    abstract member LookupByTitleAsync : title: string -> Task<Result<GoogleBookMetadata option, string>>
