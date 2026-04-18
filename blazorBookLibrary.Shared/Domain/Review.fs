
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Review =
    {
        ReviewId: ReviewId
        BookId: BookId
        UserId: UserId
        Comment: string
        Date: DateTime
        Hidden: bool
        Edited: bool
        ApprovalStatus: ApprovalStatus
    } with 
        static member New (bookId: BookId) (userId: UserId) (comment: string) (dateTime: DateTime) = 
            {   
                ReviewId = ReviewId.New();
                BookId = bookId;
                UserId = userId;
                Comment = comment;
                Date = dateTime;
                Hidden = false;
                Edited = false;
                ApprovalStatus = ApprovalStatus.Pending
            }

        member this.Edit (comment: string) = 
            { this with Comment = comment; Edited = true } |> Ok

        member this.Hide () = 
            { this with Hidden = true } |> Ok

        member this.Show () = 
            { this with Hidden = false } |> Ok

        member this.Approve (dateTime: DateTime) = 
            { this with ApprovalStatus = ApprovalStatus.Approved dateTime } |> Ok

        member this.Reject (dateTime: DateTime) = 
            { this with ApprovalStatus = ApprovalStatus.Rejected dateTime } |> Ok

        member this.Id = this.ReviewId.Value
        static member SnapshotsInterval = 50
        static member StorageName = "_Review"
        static member Version = "_01"

        member this.Serialize =
            (this, jsonOptions) |> JsonSerializer.Serialize

        static member Deserialize (json: string) = 
            try
                JsonSerializer.Deserialize<Review>(json, jsonOptions) |> Ok
            with
                ex -> Error (ex.Message)

