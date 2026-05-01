
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open blazorBookLibrary.Data

type LookupBookCandidate = 
    {
        StrIsbn: Option<string>
        Title: Option<string>
        Authors: List<string>
    }

type PartialBookDataMatch = 
    | TitleAndAuthor of title: string * authors: List<string>
    | TitleAndIsbn of title: string * isbn: string
    | AuthorAndIsbn of isbn: string * authors: List<string>
    | All of isbn: string * title: string * authors: List<string>
    member this.IsValidIsbn =
        match this with
        | TitleAndIsbn (_, isbn) -> Isbn.IsValid isbn
        | AuthorAndIsbn (isbn, _) -> Isbn.IsValid isbn
        | All (isbn, _, _) -> Isbn.IsValid isbn
        | _ -> false
    member this.ValidIsbn =
        if this.IsValidIsbn then
            match this with
            | TitleAndIsbn (_, isbn) -> isbn |> Some
            | AuthorAndIsbn (isbn, _) -> isbn |> Some 
            | All (isbn, _, _) -> isbn |> Some
            | _ -> None
        else 
            None
    member this.IsValidTitle =
        match this with
        | TitleAndAuthor (title, _) when not (title.Trim().Equals("")) -> true
        | TitleAndIsbn (title, _) when not (title.Trim().Equals("")) -> true
        | All (_, title, _) when not (title.Trim().Equals("")) -> true
        | _ -> false
    member this.ValidTitle =
        if this.IsValidTitle then
            match this with
            | TitleAndAuthor (title, _) -> title |> Some
            | TitleAndIsbn (title, _) -> title |> Some
            | All (_, title, _) -> title |> Some
            | _ -> None
        else
            None

type ITextEmbeddingService = 
    abstract member GetEmbeddingAsync: text: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<EmbeddingData,string>>
    abstract member GetMatchExplanationAsync: query: string * itemText: string * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string,string>>
    abstract member GetPartialBookMatchByCoverImage: base64Image: string -> mimeType: string -> Task<Result<PartialBookDataMatch, string>>