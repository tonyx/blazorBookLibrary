namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open System

type IMailBodyRetriever =
    abstract member GetLoanNotificationTextMailAsync:
        bookTitle: Title *
        tenantName: TenantName *
        distributionPoint: string *
        loanDate: DateTime *
        dueDate: DateTime *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetLoanNotificationSubject:
        bookTitle: Title *
        dueDate: DateTime *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReleaseLoanNotificationTextMailAsync:
        userName: string *
        bookTitle: Title *
        loanedAt: DateTime *
        returnedAt: DateTime *
        tenantName: TenantName *
        dpName: NonEmptyName *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReleaseLoanNotificationSubject:
        bookTitle: Title * shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReservationNotificationTextMailAsync:
        bookTitle: Title *
        code: ReservationCode *
        tenantName: TenantName *
        distributionPoint: string *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetReservationNotificationSubject:
        shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetPatronInvitationTextMailAsync:
        shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetPatronInvitationSubject:
        tenantName: TenantName *
        userName: string *
        shortLang: ShortLang *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>

    abstract member GetAgreementTextMailAsync:
        shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<string, string>>
