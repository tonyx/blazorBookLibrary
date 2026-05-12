namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open Sharpino
open System

type Tenant = {
    TenantId: TenantId
    TentantName: TenantName
    Address: string
    TenantState: TenantState
}
with
    static member New (tenantName: TenantName, address: string) = {
        TenantId = TenantId.New()
        TentantName = tenantName
        Address = address
        TenantState = TenantState.Active
    }
    static member Default= {
        TenantId = TenantId.Default
        TentantName = TenantName.New "Default" |> Result.get
        Address = ""
        TenantState = TenantState.Active
    }
    member this.Deactivate  =
        match this.TenantState with
        | Active -> { this with TenantState = TenantState.Deactivated } |> Ok
        | _ -> Error "Tenant is already deactivated"

    member this.Activate  =
        match this.TenantState with
        | Deactivated -> { this with TenantState = TenantState.Active } |> Ok
        | _ -> Error "Tenant is already activated"

    member this.ScheduleForDeletion (date: System.DateTime) =
        match this.TenantState with
        | Deactivated -> { this with TenantState = TenantState.ScheduledForDeletion date } |> Ok
        | _ -> Error "Tenant must be deactivated to be scheduled for deletion"

    member this.Id = this.TenantId.Value
    static member SnapshotsInterval = 100
    static member StorageName = "_Tenant"
    static member Version = "_01"

    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
        
    static member Deserialize (data: string) = 
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant> |> Ok
        with
            | ex -> Error(ex.Message)

