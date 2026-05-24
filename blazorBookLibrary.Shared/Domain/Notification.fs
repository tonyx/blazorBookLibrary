namespace BookLibrary.Domain

open System
open System.Text.Json
open BookLibrary.Shared.Commons

type NotificationId =
    | NotificationId of Guid
    with
        static member New () = NotificationId (Guid.NewGuid())
        member this.Value =
            match this with
            | NotificationId v -> v

type Notification =
    { NotificationId: NotificationId
      UserId: UserId
      Title: string
      Content: string
      IsRead: bool
      CreatedAt: DateTime
      ActionUrl: string option }
    with
        static member New (userId: UserId, title: string, content: string, ?actionUrl: string) =
            { NotificationId = NotificationId.New()
              UserId = userId
              Title = title
              Content = content
              IsRead = false
              CreatedAt = DateTime.UtcNow
              ActionUrl = actionUrl }

        member this.MarkAsRead () =
            { this with IsRead = true } |> Ok

        member this.Id = this.NotificationId.Value
        static member SnapshotsInterval = 50
        static member StorageName = "_Notification"
        static member Version = "_01"
        member this.Serialize =
            (this, jsonOptions) |> JsonSerializer.Serialize

        static member Deserialize (data: string) =
            try
                JsonSerializer.Deserialize<Notification>(data, jsonOptions) |> Ok
            with
                ex ->
                    sprintf "Failed to deserialize notification: %s" ex.Message |> Error
