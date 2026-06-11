namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type GetLoanNotificationRequest = {
    BookTitle: string
    TenantName: string
    DistributionPoint: string
    LoanDate: DateTime
    DueDate: DateTime
    ShortLang: string
}

type GetReleaseLoanNotificationRequest = {
    UserName: string
    BookTitle: string
    LoanedAt: DateTime
    ReturnedAt: DateTime
    TenantName: string
    DpName: string
    ShortLang: string
}

type GetReservationNotificationRequest = {
    BookTitle: string
    Code: string
    TenantName: string
    DistributionPoint: string
    ShortLang: string
}

type IInAppMessagesRetriever =
    abstract member GetLoanNotificationInAppAsync:
        bookTitle: Title *
        tenantName: TenantName *
        distributionPoint: string *
        loanDate: DateTime *
        dueDate: DateTime *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReleaseLoanNotificationInAppAsync:
        userName: string *
        bookTitle: Title *
        loanedAt: DateTime *
        returnedAt: DateTime *
        tenantName: TenantName *
        dpName: NonEmptyName *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReservationNotificationInAppAsync:
        bookTitle: Title *
        code: ReservationCode *
        tenantName: TenantName *
        distributionPoint: string *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetPatronInvitationInAppAsync:
        shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>
