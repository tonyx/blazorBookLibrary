namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type TenantCommand =
    | Deactivate
    | Activate
    | ScheduleForDeletion of DateTime
    | AddTag of Tag
    | RemoveTag of Tag
    | ReplaceTag of (Tag * Tag)
    
    interface AggregateCommand<Tenant, TenantEvent> with
        member this.Execute (tenant: Tenant) =
            match this with
            | Deactivate ->
                tenant.Deactivate
                |> Result.map (fun t -> (t, [TenantEvent.Deactivated]))
            | Activate ->
                tenant.Activate
                |> Result.map (fun t -> (t, [TenantEvent.Activated]))
            | ScheduleForDeletion date ->
                tenant.ScheduleForDeletion date
                |> Result.map (fun t -> (t, [TenantEvent.ScheduledForDeletion date]))
            | AddTag tag ->
                tenant.AddTag tag
                |> Result.map (fun t -> (t, [TenantEvent.TagAdded tag]))
            | RemoveTag tag ->
                tenant.RemoveTag tag
                |> Result.map (fun t -> (t, [TenantEvent.TagRemoved tag]))
            | ReplaceTag (oldTag, newTag) ->
                tenant.ReplaceTag (oldTag, newTag)
                |> Result.map (fun t -> (t, [TenantEvent.TagReplaced (oldTag, newTag)]))

        member this.Undoer = None
