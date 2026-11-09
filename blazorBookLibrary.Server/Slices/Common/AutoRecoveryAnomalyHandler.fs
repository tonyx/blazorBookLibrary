namespace BookLibrary.Services

open System
open Microsoft.Extensions.Logging
open Sharpino
open Sharpino.Core
open Sharpino.Storage
open Sharpino.Sanitizer
open BookLibrary.Domain
open BookLibrary.MessagesScheduler

type AutoRecoveryAnomalyHandler
    (
        eventStore: IEventStore<string>,
        logger: ILogger<AutoRecoveryAnomalyHandler>
    ) =

    interface IEvolveAnomalyHandler with
        member this.OnEvolveAnomaly(ctx: AnomalyContext) =
            match ctx.AggregateId with
            | Some aggId ->
                logger.LogWarning(
                    "Evolve anomaly detected on aggregate {Type} (id: {Id}, event: {EventId}): {Error}. Attempting automated NoOp patch recovery...",
                    ctx.AggregateType.Name,
                    aggId,
                    ctx.EventId,
                    ctx.ErrorMessage
                )

                let recoveryResult =
                    match ctx.AggregateType with
                    | t when t = typeof<Book> ->
                        sanitizeAggregateStream<Book, BookEvent, string> eventStore aggId BookEvent.NoOp None
                    | t when t = typeof<Author> ->
                        sanitizeAggregateStream<Author, AuthorEvent, string> eventStore aggId AuthorEvent.NoOp None
                    | t when t = typeof<Editor> ->
                        sanitizeAggregateStream<Editor, EditorEvent, string> eventStore aggId EditorEvent.NoOp None
                    | t when t = typeof<DistributionPoint> ->
                        sanitizeAggregateStream<DistributionPoint, DistributionPointEvent, string> eventStore aggId DistributionPointEvent.NoOp None
                    | t when t = typeof<Loan> ->
                        sanitizeAggregateStream<Loan, LoanEvent, string> eventStore aggId LoanEvent.NoOp None
                    | t when t = typeof<Reservation> ->
                        sanitizeAggregateStream<Reservation, ReservationEvent, string> eventStore aggId ReservationEvent.NoOp None
                    | t when t = typeof<Review> ->
                        sanitizeAggregateStream<Review, ReviewEvent, string> eventStore aggId ReviewEvent.NoOp None
                    | t when t = typeof<User> ->
                        sanitizeAggregateStream<User, UserEvent, string> eventStore aggId UserEvent.NoOp None
                    | t when t = typeof<Tenant> ->
                        sanitizeAggregateStream<Tenant, TenantEvent, string> eventStore aggId TenantEvent.NoOp None
                    | t when t = typeof<MailQueue> ->
                        sanitizeAggregateStream<MailQueue, MailQueueEvent, string> eventStore aggId MailQueueEvent.NoOp None
                    | _ ->
                        Error $"Unrecognized or unsupported aggregate type for automated recovery: {ctx.AggregateType.FullName}"

                match recoveryResult with
                | Ok report when not report.IsClean ->
                    logger.LogInformation(
                        "Automated recovery succeeded for {Type} {Id}. Applied actions: {Actions}",
                        ctx.AggregateType.Name,
                        aggId,
                        report.Actions
                    )
                | Ok _ ->
                    logger.LogInformation(
                        "Stream for {Type} {Id} evaluated as clean during recovery check.",
                        ctx.AggregateType.Name,
                        aggId
                    )
                | Error err ->
                    logger.LogError(
                        "Automated recovery failed for {Type} {Id}: {Error}",
                        ctx.AggregateType.Name,
                        aggId,
                        err
                    )
            | None ->
                logger.LogWarning(
                    "Context anomaly detected for {Type} (event: {EventId}): {Error}. Context stream recovery without aggregate ID is not automatically executed.",
                    ctx.AggregateType.Name,
                    ctx.EventId,
                    ctx.ErrorMessage
                )

