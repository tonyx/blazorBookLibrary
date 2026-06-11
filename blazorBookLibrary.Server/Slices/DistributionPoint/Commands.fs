
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type DistributionPointCommand =
    | Rename of NonEmptyName
    | UpdateInfo of Info
    | AddReferenceUser of UserId
    | RemoveReferenceUser of UserId
    interface AggregateCommand<DistributionPoint, DistributionPointEvent> with
        member this.Execute (distributionPoint: DistributionPoint) =
            match this with
            | Rename name ->
                distributionPoint.Rename name
                |> Result.map (fun a -> (a, [Renamed(name)]))
            | UpdateInfo info ->
                distributionPoint.UpdateInfo info
                |> Result.map (fun a -> (a, [InfoUpdated(info)]))
            | AddReferenceUser userId ->
                distributionPoint.AddReferenceUser userId
                |> Result.map (fun a -> (a, [ReferenceUserAdded(userId)]))
            | RemoveReferenceUser userId ->
                distributionPoint.RemoveReferenceUser userId
                |> Result.map (fun a -> (a, [ReferenceUserRemoved(userId)]))

        member this.Undoer = None
