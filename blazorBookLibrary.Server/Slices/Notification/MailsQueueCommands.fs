
namespace BookLibrary.MessagesScheduler

open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type MailQueueCommand =
    | AddMailQueueItem of MailQueueItem
    | RemoveMailQueueItem of MailQueueItemId
    | IncrementRetryCount of MailQueueItemId
    interface AggregateCommand<MailQueue, MailQueueEvent> with
        member this.Execute (state: MailQueue) =
            match this with
            | AddMailQueueItem item -> 
                state.Add(item) 
                |> Result.map (fun s -> (s, [MailQueueEvent.MailQueueItemAdded item]))
            | RemoveMailQueueItem mailQueueItemId -> 
                state.Remove(mailQueueItemId) 
                |> Result.map (fun x -> (x, [MailQueueEvent.MailQueueItemRemoved mailQueueItemId]))
            | IncrementRetryCount mailQueueItemId -> 
                state.IncrementRetryCount(mailQueueItemId) 
                |> Result.map (fun x -> (x, [MailQueueEvent.MailQueueItemRetryCountIncremented mailQueueItemId]))
        member this.Undoer =
            None