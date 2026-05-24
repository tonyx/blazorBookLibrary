namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type NotificationEvent =
    | ReadMarked
    interface Event<Notification> with
        member this.Process (notification: Notification) : Result<Notification, string> =
            match this with
            | ReadMarked ->
                notification.MarkAsRead ()

    static member Deserialize (x: string): Result<NotificationEvent, string> =
        try
            JsonSerializer.Deserialize<NotificationEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
