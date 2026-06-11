namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type AuthorEvent =
    | Renamed of Name * DateTime
    | IsniUpdated of Isni * DateTime
    | BookAdded of BookId
    | BookRemoved of BookId
    | Sealed of DateTime
    | Unsealed of DateTime
    | ImageUrlUpdated of Uri * DateTime
    | ImageUrlRemoved of DateTime
    | BioUpdated of string * DateTime
    | WikipediaUriUpdated of Uri * DateTime

    interface Event<Author> with
        member this.Process(author: Author) : Result<Author, string> =
            match this with
            | Renamed(name, dateTime) -> author.Rename name dateTime
            | IsniUpdated(isni, dateTime) -> author.UpdateIsni isni dateTime
            | BioUpdated(bio, dateTime) -> author.UpdateBio bio dateTime
            | WikipediaUriUpdated(wikipediaUri, dateTime) -> author.UpdateWikipediaUri wikipediaUri dateTime
            | ImageUrlUpdated(imageUrl, dateTime) -> author.UpdateImageUrl imageUrl dateTime
            | ImageUrlRemoved dateTime -> author.RemoveImageUrl dateTime
            | Sealed dateTime -> author.Seal dateTime
            | Unsealed dateTime -> author.Unseal dateTime
            | BookAdded bookId -> author |> Ok
            | BookRemoved bookId -> author |> Ok


    static member Deserialize(x: string) : Result<AuthorEvent, string> =
        try
            JsonSerializer.Deserialize<AuthorEvent>(x, jsonOptions) |> Ok
        with ex ->
            Error ex.Message

    member this.Serialize = JsonSerializer.Serialize(this, jsonOptions)
