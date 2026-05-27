namespace BookLibrary.Domain

open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization

type DistributionPoint =
    { TenantId: TenantId
      DistributionPointId: DistributionPointId
      Name: NonEmptyName
      Info: Info
      ReferenceUsers: List<UserId> }

    static member New(tenantId: TenantId, name: NonEmptyName, info: Info) =
        { TenantId = tenantId
          DistributionPointId = DistributionPointId.New()
          Name = name
          Info = info
          ReferenceUsers = [] }

    static member New(tenantId: TenantId, name: NonEmptyName, info: Info, referenceUser: UserId) =
        { TenantId = tenantId
          DistributionPointId = DistributionPointId.New()
          Name = name
          Info = info
          ReferenceUsers = [ referenceUser ] }

    member this.AddReferenceUser(referenceUser: UserId) =
        if this.ReferenceUsers |> List.contains referenceUser then
            Error "Reference user already exists"
        else
            { this with
                ReferenceUsers = referenceUser :: this.ReferenceUsers }
            |> Ok

    member this.RemoveReferenceUser(referenceUser: UserId) =
        if this.ReferenceUsers |> List.contains referenceUser |> not then
            Error "Reference user does not exist"
        else
            { this with
                ReferenceUsers = this.ReferenceUsers |> List.filter (fun x -> x <> referenceUser) }
            |> Ok

    member this.Rename(name: NonEmptyName) = { this with Name = name } |> Ok

    member this.UpdateInfo(info: Info) = { this with Info = info } |> Ok


    member this.Id = this.DistributionPointId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_DistributionPoint"
    static member Version = "_01"
    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<DistributionPoint> |> Ok
        with ex ->
            sprintf "Failed to deserialize distribution point: %s" ex.Message |> Error
