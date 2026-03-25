
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
    interface Event<Author> with
        member this.Process (author: Author) : Result<Author, string> =
            match this with
            | Renamed (name, dateTime) ->
                author.Rename name dateTime
            | IsniUpdated (isni, dateTime) ->
                author.UpdateIsni isni dateTime
            | BookAdded bookId ->
                author.AddBook bookId
            | BookRemoved bookId ->
                author.RemoveBook bookId
            | Sealed dateTime ->
                author.Seal dateTime
            | Unsealed dateTime ->
                author.Unseal dateTime

    static member Deserialize (x: string): Result<AuthorEvent, string> =
        try
            JsonSerializer.Deserialize<AuthorEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)