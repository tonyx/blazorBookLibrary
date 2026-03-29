namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IReservationService =
    abstract member AddReservationAsync : reservation: Reservation * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationAsync : id: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Reservation, string>>
    abstract member RemoveReservationAsync : reservationId: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationsAsync : ids: List<ReservationId> * ?ct: CancellationToken -> Task<Result<List<Reservation>, string>>
