
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Globalization

type CommentCommand = 
    | Edit of string * DateTime
    | Hide of DateTime
    | Show of DateTime
    | Approve of DateTime
    | Reject of DateTime

    interface AggregateCommand<BookLibrary.Domain.Review, ReviewEvent> with
        member this.Execute (comment: BookLibrary.Domain.Review) = 
            match this with
            | Edit (commentValue, dateTime) -> 
                comment.Edit (commentValue)
                |> Result.map (fun x -> (x, [ReviewEvent.Edited (commentValue, dateTime)]))
            | Hide dateTime -> 
                comment.Hide()
                |> Result.map (fun x -> (x, [ReviewEvent.Hidden dateTime]))
            | Show dateTime -> 
                comment.Show()
                |> Result.map (fun x -> (x, [ReviewEvent.Shown dateTime]))
            | Approve dateTime -> 
                comment.Approve dateTime
                |> Result.map (fun x -> (x, [ReviewEvent.Approved dateTime]))
            | Reject dateTime -> 
                comment.Reject dateTime
                |> Result.map (fun x -> (x, [ReviewEvent.Rejected dateTime]))

        member this.Undoer = None

