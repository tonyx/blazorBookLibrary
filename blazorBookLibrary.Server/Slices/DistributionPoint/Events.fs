
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type DistributionPointEvent =
    | Renamed of NonEmptyName
    | InfoUpdated of Info
    | ReferenceUserAdded of UserId
    | ReferenceUserRemoved of UserId
    interface Event<DistributionPoint> with
        member this.Process (distributionPoint: DistributionPoint) : Result<DistributionPoint, string> =
            match this with
            | Renamed name ->
                distributionPoint.Rename name
            | InfoUpdated info ->
                distributionPoint.UpdateInfo info
            | ReferenceUserAdded userId ->
                distributionPoint.AddReferenceUser userId
            | ReferenceUserRemoved userId ->
                distributionPoint.RemoveReferenceUser userId

    static member Deserialize (x: string): Result<DistributionPointEvent, string> =
        try
            JsonSerializer.Deserialize<DistributionPointEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
