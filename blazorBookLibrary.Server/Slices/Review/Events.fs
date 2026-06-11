
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json
open System.Globalization

type ReviewEvent = 
    | Edited of string * DateTime
    | Hidden of DateTime
    | Shown of DateTime
    | Approved of DateTime
    | Rejected of DateTime

    interface Event<BookLibrary.Domain.Review> with
        member this.Process (comment: BookLibrary.Domain.Review) = 
            match this with
            | Edited (commentValue, _) -> 
                comment.Edit commentValue
            | Hidden _ -> 
                comment.Hide()
            | Shown _ -> 
                comment.Show()
            | Approved dateTime -> 
                comment.Approve dateTime
            | Rejected dateTime -> 
                comment.Reject dateTime
        
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)

    static member Deserialize (data: string) = 
        try
            JsonSerializer.Deserialize<ReviewEvent> (data, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
            
    