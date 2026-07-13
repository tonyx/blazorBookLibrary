
namespace BookLibrary.Domain
open System.Text.Json
open BookLibrary.Shared.Commons
open System

type Review =
    {
        TenantId: TenantId
        ReviewId: ReviewId
        BookId: BookId
        UserId: UserId
        Comment: string
        Date: DateTime
        Hidden: bool
        Edited: bool
        ApprovalStatus: ApprovalStatus
    } with 
        static member New (tenantId: TenantId) (bookId: BookId) (userId: UserId) (comment: string) (dateTime: DateTime) = 
            {   
                TenantId = tenantId
                ReviewId = ReviewId.New();
                BookId = bookId;
                UserId = userId;
                Comment = comment;
                Date = dateTime;
                Hidden = false;
                Edited = false;
                ApprovalStatus = ApprovalStatus.Pending
            }
        static member NewHidden (tenantId: TenantId) (bookId: BookId) (userId: UserId) (comment: string) (dateTime: DateTime) = 
            {   
                TenantId = tenantId
                ReviewId = ReviewId.New();
                BookId = bookId;
                UserId = userId;
                Comment = comment;
                Date = dateTime;
                Hidden = true;
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

        static member Deserialize (data: string) = 
            try
                JsonSerializer.Deserialize<Review>(data, jsonOptions) |> Ok
            with
                ex -> 
                    sprintf "Failed to deserialize review: %s" ex.Message |> Error

