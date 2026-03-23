
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Commons
open System.Text.Json

type AuthorEvent =
    | NameUpdated of Name * DateTime
    | IsniUpdated of Isni * DateTime
    | BookAdded of BookId
    | BookRemoved of BookId
    | AuthorSealed of DateTime
    | AuthorUnsealed of DateTime
    interface Event<Author> with
        member this.Process (author: Author) : Result<Author, string> =
            match this with
            | NameUpdated (name, dateTime) ->
                author.UpdateName name dateTime
            | IsniUpdated (isni, dateTime) ->
                author.UpdateIsni isni dateTime
            | BookAdded bookId ->
                author.AddBook bookId
            | BookRemoved bookId ->
                author.RemoveBook bookId
            | AuthorSealed dateTime ->
                author.Seal dateTime
            | AuthorUnsealed dateTime ->
                author.Unseal dateTime

    static member Deserialize (x: string): Result<AuthorEvent, string> =
        try
            JsonSerializer.Deserialize<AuthorEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)