namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open Sharpino
open System

type 
    Tenant = {
        OwnerId: UserId
        TenantId: TenantId
        TentantName: TenantName
        Address: string
        TenantState: TenantState
        Public: bool
        Tags: List<Tag>
    }

with
    static member New (userId: UserId, tenantName: TenantName, address: string, ?pub: bool) = {
        OwnerId = userId
        TenantId = TenantId.New()
        TentantName = tenantName
        Address = address
        TenantState = TenantState.Active
        Public = pub |> Option.defaultValue true
        Tags = []
    }
    static member NewDefault (userId: UserId, tenantName: TenantName, address: string) =
        { Tenant.New(userId, tenantName, address, true) with 
            TenantId = TenantId.Default }
    member this.Deactivate  =
        match this.TenantState with
        | Active -> { this with TenantState = TenantState.Deactivated } |> Ok
        | _ -> Error "Tenant is already deactivated"

    member this.AddTag (tag: Tag) =
        result
            {
                do! 
                    this.Tags |> List.exists (fun t -> t = tag)
                    |> not
                    |> Result.ofBool "Tag already exists"
                return { this with Tags = this.Tags @ [tag] }
            }

    member this.RemoveTag (tag: Tag) =
        result
            {
                do! 
                    this.Tags |> List.exists (fun t -> t = tag)
                    |> Result.ofBool "Tag does not exist"
                return { this with Tags = this.Tags |> List.filter (fun t -> t <> tag) }
            }

    member this.ReplaceTag (oldTag: Tag, newTag: Tag) =
        result
            {
                do! 
                    this.Tags |> List.exists (fun t -> t = oldTag)
                    |> Result.ofBool "Tag does not exist"
                return { this with Tags = this.Tags |> List.map (fun t -> if t = oldTag then newTag else t) }
            }

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
        // reminder: a proper computation expression could be used here.
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant> |> Ok
        with
            | ex -> 
                Error (ex.Message)
                    

