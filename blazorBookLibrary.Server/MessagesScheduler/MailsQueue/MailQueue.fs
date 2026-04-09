
namespace BookLibrary.MessagesScheduler
open Mailjet.Client.TransactionalEmails
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type MailQueueItem = 
    {
        MailQueueItemId: MailQueueItemId
        TransactionalEmail: TransactionalEmail
        RetryCount: int

        // EmailFrom: EmailFrom
        // NameFrom: NameFrom
        // EmailTo: EmailTo
        // Subject: Subject
        // Body: Body
    }
    with
        static member New(transactionalEmail: TransactionalEmail) = 
            { 
                MailQueueItemId = MailQueueItemId.New(); 
                TransactionalEmail = transactionalEmail; 
                RetryCount = 0 }

type MailQueue = 
    { 
        MailQueueId: MailQueueId
        MailQueueItems: List<MailQueueItem>
    }

    with
        static member New() = { MailQueueId = MailQueue.UniqueInstanceId; MailQueueItems = [] }
        member this.Add(mailQueueItem: MailQueueItem) = 
            { 
                this with
                    MailQueueItems = mailQueueItem :: this.MailQueueItems 
            }
            |> Ok
        member this.Remove(mailQueueItemId: MailQueueItemId) = 
            { 
                this with
                    MailQueueItems = this.MailQueueItems |> List.filter (fun x -> x.MailQueueItemId <> mailQueueItemId) 
            }
            |> Ok
        member this.IncrementRetryCount(mailQueueItemId: MailQueueItemId) = 
            { 
                this with
                    MailQueueItems = 
                        this.MailQueueItems 
                        |> List.map (fun x -> if x.MailQueueItemId = mailQueueItemId then { x with RetryCount = x.RetryCount + 1 } else x) 
            }
            |> Ok

        static member UniqueInstanceId = MailQueueId (Guid.Parse("4a1bd31e-41cf-412b-91d9-26f777a588a7"))

        member this.Id = this.MailQueueId.Value
        static member SnapshotsInterval = 50 // not used anymore as it is in config file so it will disappear next refactoring
        static member StorageName = "_MailQueue"
        static member Version = "_01"
        member this.Serialize =
            (this, jsonOptions) |> JsonSerializer.Serialize
        static member Deserialize(json: string) = 
            try
                JsonSerializer.Deserialize<MailQueue>(json, jsonOptions) |> Ok
            with
                ex -> Error ex.Message

    

