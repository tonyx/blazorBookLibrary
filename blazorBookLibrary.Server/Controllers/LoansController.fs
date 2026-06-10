
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type LoansController(loanService: ILoanService, reservationService: IReservationService, hubContext: Microsoft.AspNetCore.SignalR.IHubContext<BookLibrary.Hubs.LibraryHub>) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetLoans() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoansAsync(context)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("unarchived")>]
    member this.GetUnarchivedLoans() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetUnarchivedLoansAsync(context)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("history/{userId}")>]
    member this.GetHistory(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetHistoryLoansOfUserAsync(context, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddLoan(loan: Loan) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.AddLoanAsync(context, loan)
            match result with
            | Ok _ -> 
                do! hubContext.Clients.Group($"Tenant_{loan.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| loan.TenantId.Value :> obj; loan.BookId.Value :> obj |])
                do! hubContext.Clients.Group($"Book_{loan.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| loan.TenantId.Value :> obj; loan.BookId.Value :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoanAsync(context, LoanId id)
            match result with
            | Ok loan -> return this.Ok(loan) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost("release/{id}")>]
    member this.ReleaseLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! loanDetails = 
                task {
                    match! loanService.GetLoanAsync(context, LoanId id) with
                    | Ok l -> return Some l
                    | _ -> return None
                }
            let! result = loanService.ReleaseLoanAsync(context, LoanId id, DateTime.UtcNow)
            match result with
            | Ok () -> 
                match loanDetails with
                | Some l -> 
                    do! hubContext.Clients.Group($"Tenant_{l.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                    do! hubContext.Clients.Group($"Book_{l.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                | None -> 
                    do! hubContext.Clients.All.SendCoreAsync("BookAvailabilityChanged", [| Guid.Empty :> obj; Guid.Empty :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("transform-reservation/{reservationId}")>]
    member this.TransformReservation(reservationId: Guid, [<FromBody>] reservationCode: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! resDetails = 
                task {
                    match! reservationService.GetReservationAsync(context, ReservationId reservationId) with
                    | Ok r -> return Some r
                    | _ -> return None
                }
            let! result = loanService.TransformReservationIntoLoanAsync(context, ReservationId reservationId, ReservationCode reservationCode, DateTime.UtcNow)
            match result with
            | Ok _ -> 
                match resDetails with
                | Some r ->
                    do! hubContext.Clients.Group($"Tenant_{r.TenantId.Value}").SendCoreAsync("PendingReservationsChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj; r.UserId.Value :> obj |])
                    do! hubContext.Clients.Group($"User_{r.UserId.Value}").SendCoreAsync("PendingReservationsChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj; r.UserId.Value :> obj |])
                    do! hubContext.Clients.Group($"Tenant_{r.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj |])
                    do! hubContext.Clients.Group($"Book_{r.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj |])
                | None ->
                    do! hubContext.Clients.All.SendCoreAsync("PendingReservationsChanged", [| Guid.Empty :> obj; Guid.Empty :> obj; Guid.Empty :> obj |])
                    do! hubContext.Clients.All.SendCoreAsync("BookAvailabilityChanged", [| Guid.Empty :> obj; Guid.Empty :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("transform-reservation-by-pin/{reservationId}")>]
    member this.TransformReservationByPin(reservationId: Guid, [<FromBody>] pin: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! resDetails = 
                task {
                    match! reservationService.GetReservationAsync(context, ReservationId reservationId) with
                    | Ok r -> return Some r
                    | _ -> return None
                }
            let! result = loanService.TransformReservationIntoLoanByPinAsync(context, ReservationId reservationId, pin, DateTime.UtcNow)
            match result with
            | Ok _ -> 
                match resDetails with
                | Some r ->
                    do! hubContext.Clients.Group($"Tenant_{r.TenantId.Value}").SendCoreAsync("PendingReservationsChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj; r.UserId.Value :> obj |])
                    do! hubContext.Clients.Group($"User_{r.UserId.Value}").SendCoreAsync("PendingReservationsChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj; r.UserId.Value :> obj |])
                    do! hubContext.Clients.Group($"Tenant_{r.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj |])
                    do! hubContext.Clients.Group($"Book_{r.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| r.TenantId.Value :> obj; r.BookId.Value :> obj |])
                | None ->
                    do! hubContext.Clients.All.SendCoreAsync("PendingReservationsChanged", [| Guid.Empty :> obj; Guid.Empty :> obj; Guid.Empty :> obj |])
                    do! hubContext.Clients.All.SendCoreAsync("BookAvailabilityChanged", [| Guid.Empty :> obj; Guid.Empty :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }


    [<HttpDelete("{id}")>]
    member this.RemoveLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! loanDetails = 
                task {
                    match! loanService.GetLoanAsync(context, LoanId id) with
                    | Ok l -> return Some l
                    | _ -> return None
                }
            let! result = loanService.RemoveLoanAsync(context, LoanId id)
            match result with
            | Ok () -> 
                match loanDetails with
                | Some l -> 
                    do! hubContext.Clients.Group($"Tenant_{l.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                    do! hubContext.Clients.Group($"Book_{l.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                | None -> 
                    do! hubContext.Clients.All.SendCoreAsync("BookAvailabilityChanged", [| Guid.Empty :> obj; Guid.Empty :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("archive/{id}")>]
    member this.ArchiveLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! loanDetails = 
                task {
                    match! loanService.GetLoanAsync(context, LoanId id) with
                    | Ok l -> return Some l
                    | _ -> return None
                }
            let! result = loanService.ArchiveLoanAsync(context, LoanId id)
            match result with
            | Ok () -> 
                match loanDetails with
                | Some l -> 
                    do! hubContext.Clients.Group($"Tenant_{l.TenantId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                    do! hubContext.Clients.Group($"Book_{l.BookId.Value}").SendCoreAsync("BookAvailabilityChanged", [| l.TenantId.Value :> obj; l.BookId.Value :> obj |])
                | None -> 
                    do! hubContext.Clients.All.SendCoreAsync("BookAvailabilityChanged", [| Guid.Empty :> obj; Guid.Empty :> obj |])
                return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
    [<HttpGet("tenant/{tenantId}/user/{userId}")>]
    member this.GetLoansOfUserInTenant(tenantId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoansOfUserInATenantAsync(context, TenantId tenantId, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
