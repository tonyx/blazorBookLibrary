namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Definitions
open Sharpino.Core
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IReservationService =
    abstract member AddReservationAsync : reservation: Reservation * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationAsync : id: ReservationId * ?ct: CancellationToken -> Task<Result<Reservation, string>>
    abstract member RemoveReservationAsync : reservationId: ReservationId * ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationsAsync : ids: List<ReservationId> * ?ct: CancellationToken -> Task<Result<List<Reservation>, string>>
