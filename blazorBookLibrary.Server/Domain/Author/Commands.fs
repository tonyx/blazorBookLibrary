
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Commons

type AuthorCommand =
    | UpdateName of Name * DateTime
    | UpdateIsni of Isni * DateTime
    | AddBook of BookId
    | RemoveBook of BookId
    | Seal of DateTime
    | Unseal of DateTime
    interface AggregateCommand<Author, AuthorEvent> with
        member this.Execute (author: Author) =
            match this with
            | UpdateName (name, dateTime) ->
                author.UpdateName name dateTime
                |> Result.map (fun a -> (a, [NameUpdated(name, dateTime)]))
            | UpdateIsni (isni, dateTime) ->
                author.UpdateIsni isni dateTime
                |> Result.map (fun a -> (a, [IsniUpdated(isni, dateTime)]))
            | AddBook bookId ->
                author.AddBook bookId
                |> Result.map (fun a -> (a, [BookAdded(bookId)]))
            | RemoveBook bookId ->
                author.RemoveBook bookId
                |> Result.map (fun a -> (a, [BookRemoved(bookId)]))
            | Seal dateTime ->
                author.Seal dateTime
                |> Result.map (fun a -> (a, [AuthorSealed(dateTime)]))
            | Unseal dateTime ->
                author.Unseal dateTime
                |> Result.map (fun a -> (a, [AuthorUnsealed(dateTime)]))

        member this.Undoer = None
