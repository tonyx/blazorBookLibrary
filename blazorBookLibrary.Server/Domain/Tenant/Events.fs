namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type TenantEvent =
    | Deactivated 
    | Activated
    | ScheduledForDeletion of DateTime
    interface Event<Tenant> with
        member this.Process (tenant: Tenant) : Result<Tenant, string> =
            match this with
            | Deactivated ->
                tenant.Deactivate
            | Activated ->
                tenant.Activate
            | ScheduledForDeletion date ->
                tenant.ScheduleForDeletion date

    static member Deserialize (x: string): Result<TenantEvent, string> =
        try
            JsonSerializer.Deserialize<TenantEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
