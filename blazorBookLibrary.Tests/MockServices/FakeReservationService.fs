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
        member this.AddReservationAsync(userContext, reservation, shortLang, ct) =
            printfn "FakeReservationService: AddReservationAsync called for reservation %A (Lang: %A)" reservation shortLang
            Task.FromResult(Ok ())

        member this.GetReservationAsync(userContext, id, ct) =
            printfn "FakeReservationService: GetReservationAsync called for id %A" id
            Task.FromResult(Error "GetReservationAsync not fully implemented in FakeReservationService")

        member this.RemoveReservationAsync(userContext, reservationId, ct) =
            printfn "FakeReservationService: RemoveReservationAsync called for id %A" reservationId
            Task.FromResult(Ok ())

        member this.CancelReservationAsync(userContext, reservationId, reason, ct) =
            printfn "FakeReservationService: CancelReservationAsync called for id %A (Reason: %A)" reservationId reason
            Task.FromResult(Ok ())

        member this.GetReservationsAsync(userContext, ids, ct) =
            printfn "FakeReservationService: GetReservationsAsync called for %d ids" ids.Length
            Task.FromResult(Ok [])

        member this.GetReservationDetailsAsync(userContext, id, ct) =
            printfn "FakeReservationService: GetReservationDetailsAsync called for id %A" id
            Task.FromResult(Error "GetReservationDetailsAsync not fully implemented in FakeReservationService")
        member this.RemoveExpiredReservationsAsync (userContext, ct) = 
            printfn "FakeReservationService: RemoveExpiredReservationsAsync called"
            Task.FromResult(Ok ())        
        member this.GetAllPendingReservationsDetailsAsync(userContext, ct) = 
            printfn "FakeReservationService: GetAllPendingReservationsDetailsAsync called"
            Task.FromResult(Ok [])
        member this.GetMyPendingReservationsAsync(userContext, ct) = 
            printfn "FakeReservationService: GetMyPendingReservationsAsync called"
            Task.FromResult(Ok [])
        member this.GeneratePickupPinAsync(userContext, id, ct) =
            printfn "FakeReservationService: GeneratePickupPinAsync called for id %A" id
            Task.FromResult(Ok ("123456", System.DateTime.UtcNow.AddMinutes(15.0)))

        member this.GetReservationsOfABookAsync(userContext, bookId, ct) =
            printfn "FakeReservationService: GetReservationsOfABookAsync called for book %A" bookId
            Task.FromResult(Ok [])

