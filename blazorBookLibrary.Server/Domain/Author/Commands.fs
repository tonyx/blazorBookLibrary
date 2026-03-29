
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type AuthorCommand =
    | Rename of Name * DateTime
    | UpdateIsni of Isni * DateTime
    | AddBook of BookId
    | RemoveBook of BookId
    | Seal of DateTime
    | Unseal of DateTime
    | UpdateImageUrl of Uri * DateTime
    | RemoveImageUrl of DateTime
    interface AggregateCommand<Author, AuthorEvent> with
        member this.Execute (author: Author) =
            match this with
            | Rename (name, dateTime) ->
                author.Rename name dateTime
                |> Result.map (fun a -> (a, [Renamed(name, dateTime)]))
            | UpdateIsni (isni, dateTime) ->
                author.UpdateIsni isni dateTime
                |> Result.map (fun a -> (a, [IsniUpdated(isni, dateTime)]))
            | UpdateImageUrl (imageUrl, dateTime) ->
                author.UpdateImageUrl imageUrl dateTime
                |> Result.map (fun a -> (a, [ImageUrlUpdated(imageUrl, dateTime)]))
            | RemoveImageUrl dateTime ->
                author.RemoveImageUrl dateTime
                |> Result.map (fun a -> (a, [ImageUrlRemoved(dateTime)]))
            | AddBook bookId ->
                author.AddBook bookId
                |> Result.map (fun a -> (a, [BookAdded(bookId)]))
            | RemoveBook bookId ->
                author.RemoveBook bookId
                |> Result.map (fun a -> (a, [BookRemoved(bookId)]))
            | Seal dateTime ->
                author.Seal dateTime
                |> Result.map (fun a -> (a, [Sealed(dateTime)]))
            | Unseal dateTime ->
                author.Unseal dateTime
                |> Result.map (fun a -> (a, [Unsealed(dateTime)]))

        member this.Undoer = None
