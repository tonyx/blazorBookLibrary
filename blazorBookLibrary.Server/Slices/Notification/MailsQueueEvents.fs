
namespace BookLibrary.MessagesScheduler

open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type MailQueueEvent =
    | MailQueueItemAdded of MailQueueItem
    | MailQueueItemRemoved of MailQueueItemId
    | MailQueueItemRetryCountIncremented of MailQueueItemId
    interface Event<MailQueue> with
        member this.Process (state: MailQueue): Result<MailQueue, string> = 
            match this with
            | MailQueueItemAdded item -> 
                state.Add(item) 
            | MailQueueItemRemoved item ->
                state.Remove(item) 
            | MailQueueItemRetryCountIncremented item -> 
                state.IncrementRetryCount(item)

    static member Deserialize (x: string): Result<MailQueueEvent, string> =
        try
            JsonSerializer.Deserialize<MailQueueEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)





