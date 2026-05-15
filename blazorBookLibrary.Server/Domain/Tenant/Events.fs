namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type TenantEvent =
    | Deactivated 
    | Activated
    | ScheduledForDeletion of DateTime
    | TagAdded of Tag
    | TagRemoved of Tag
    | TagReplaced of (Tag * Tag)
    interface Event<Tenant> with
        member this.Process (tenant: Tenant) : Result<Tenant, string> =
            match this with
            | Deactivated ->
                tenant.Deactivate
            | Activated ->
                tenant.Activate
            | ScheduledForDeletion date ->
                tenant.ScheduleForDeletion date
            | TagAdded tag ->
                tenant.AddTag tag
            | TagRemoved tag ->
                tenant.RemoveTag tag
            | TagReplaced (oldTag, newTag) ->
                tenant.ReplaceTag (oldTag, newTag)

    static member Deserialize (x: string): Result<TenantEvent, string> =
        try
            JsonSerializer.Deserialize<TenantEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
