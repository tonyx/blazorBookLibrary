namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type NotificationCommand =
    | MarkAsRead
    interface AggregateCommand<Notification, NotificationEvent> with
        member this.Execute (notification: Notification) =
            match this with
            | MarkAsRead ->
                notification.MarkAsRead ()
                |> Result.map (fun n -> (n, [NotificationEvent.ReadMarked]))
        member this.Undoer = None
