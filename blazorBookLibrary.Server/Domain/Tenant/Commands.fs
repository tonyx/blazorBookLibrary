namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type TenantCommand =
    | Deactivate
    | Activate
    | ScheduleForDeletion of DateTime

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

        member this.Undoer = None
