namespace blazorBookLibrary.Tests.MockServices

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Shared.Services

type FakeReservationService() =
    interface IReservationService with
        member this.AddReservationAsync(reservation, shortLang, ct) =
            printfn "FakeReservationService: AddReservationAsync called for reservation %A (Lang: %A)" reservation shortLang
            Task.FromResult(Ok ())

        member this.GetReservationAsync(id, ct) =
            printfn "FakeReservationService: GetReservationAsync called for id %A" id
            Task.FromResult(Error "GetReservationAsync not fully implemented in FakeReservationService")

        member this.RemoveReservationAsync(reservationId, ct) =
            printfn "FakeReservationService: RemoveReservationAsync called for id %A" reservationId
            Task.FromResult(Ok ())

        member this.GetReservationsAsync(ids, ct) =
            printfn "FakeReservationService: GetReservationsAsync called for %d ids" ids.Length
            Task.FromResult(Ok [])

        member this.GetReservationDetailsAsync(id, ct) =
            printfn "FakeReservationService: GetReservationDetailsAsync called for id %A" id
            Task.FromResult(Error "GetReservationDetailsAsync not fully implemented in FakeReservationService")
