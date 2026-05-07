namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IReservationService =
    abstract member AddReservationAsync : context: UserContext * reservation: Reservation * shortLang:ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationAsync : context: UserContext * id: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Reservation, string>>
    abstract member RemoveReservationAsync : context: UserContext * reservationId: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member GetReservationsAsync : context: UserContext * ids: List<ReservationId> * ?ct: CancellationToken -> Task<Result<List<Reservation>, string>>
    abstract member GetReservationDetailsAsync : context: UserContext * id: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<ReservationDetails, string>>
    abstract member GetAllPendingReservationsDetailsAsync : context: UserContext * ?ct: CancellationToken -> Task<Result<List<ReservationDetails>, string>>
    abstract member RemoveExpiredReservationsAsync : context: UserContext * ?ct: CancellationToken -> TaskResult<unit, string>
