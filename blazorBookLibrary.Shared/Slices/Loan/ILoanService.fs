namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type ILoanService =
    abstract member AddLoanAsync:
        context: UserContext * loan: Loan * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member GetLoanAsync:
        context: UserContext * id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<Loan, string>>

    abstract member GetLoansAsync:
        context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<Loan>, string>>

    abstract member GetLoansOfUserInATenantAsync:
        context: UserContext *
        tenantId: TenantId *
        userId: UserId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<Loan>, string>>

    abstract member GetUnarchivedLoansAsync:
        context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<Loan>, string>>

    abstract member ReleaseLoanAsync:
        context: UserContext *
        loanId: LoanId *
        date: DateTime *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member TransformReservationIntoLoanAsync:
        context: UserContext *
        reservationId: ReservationId *
        providedReservationCode: ReservationCode *
        date: DateTime *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member TransformReservationIntoLoanByPinAsync:
        context: UserContext *
        reservationId: ReservationId *
        pin: string *
        date: DateTime *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member GetHistoryLoansOfUserAsync:
        context: UserContext * userId: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<Loan>, string>>

    abstract member RemoveLoanAsync:
        context: UserContext * loanId: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member ArchiveLoanAsync:
        context: UserContext * loanId: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>
