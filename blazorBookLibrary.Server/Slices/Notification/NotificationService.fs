namespace BookLibrary.Services

open System.Threading
open System
open Sharpino
open Sharpino.CommandHandler
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.SignalR
open BookLibrary.Hubs

type NotificationService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        notificationViewerAsync: AggregateViewerAsync2<Notification>,
        userTenantResolverService: IUserTenantResolverService,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        logger: ILogger<INotificationService>,
        hubContext: IHubContext<LibraryHub>
    ) =

    new (secretsReader: SecretsReader, logger: ILogger<INotificationService>, userTenantResolverService: IUserTenantResolverService, hubContext: IHubContext<LibraryHub>) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        let notificationViewerAsync = getAggregateStorageFreshStateViewerAsync<Notification, BookLibrary.Domain.NotificationEvent, string> eventStore
        NotificationService(eventStore, messageSenders, notificationViewerAsync, userTenantResolverService, tenantViewerAsync, logger, hubContext)

    member this.GetUnreadNotificationsForUser (context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult {
            let! userId = 
                match context with
                | UserContext.Authenticated (uId, _) -> Ok uId
                | _ -> Error "Access denied: unauthenticated"
            let filter = fun (n: Notification) -> n.UserId = userId && not n.IsRead
            let! states = 
                StateView.getAllFilteredAggregateStatesAsync<Notification, NotificationEvent, string>
                    filter
                    eventStore
                    (ct |> Some)
            let resultList = states |>> snd |> List.sortByDescending (fun n -> n.CreatedAt)
            return resultList
        }

    member this.GetAllNotificationsForUser (context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult {
            let! userId = 
                match context with
                | UserContext.Authenticated (uId, _) -> Ok uId
                | _ -> Error "Access denied: unauthenticated"
            let filter = fun (n: Notification) -> n.UserId = userId
            let! states = 
                StateView.getAllFilteredAggregateStatesAsync<Notification, NotificationEvent, string>
                    filter
                    eventStore
                    (ct |> Some)
            let resultList = states |>> snd |> List.sortByDescending (fun n -> n.CreatedAt)
            return resultList
        }

    member this.MarkAsRead (context: UserContext, notificationId: NotificationId, ?ct: CancellationToken) =
        taskResult {

            let! (_, notification) = notificationViewerAsync ct notificationId.Value
            let! userId =
                match context with
                | UserContext.Authenticated (uId, _) -> Ok uId
                | _ -> Error "Access denied: unauthenticated"

            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct |> Option.defaultValue CancellationToken.None)
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value

            do! 
                match context, tenant with
                | UserContext.Authenticated (uId, _), _  when uId = notification.UserId || context.IsInRole Role.Admin -> Ok()
                | _, t when t.IsManager userId -> Ok ()
                | _ -> Error "Access denied: cannot modify another user's notifications"

            let command = NotificationCommand.MarkAsRead
            let! _ = 
                runAggregateCommandMdAsync<Notification, NotificationEvent, string>
                    notificationId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
            do! 
                task {
                    try
                        do! hubContext.Clients.All.SendAsync("NotificationChanged", notification.UserId.Value.ToString())
                    with ex ->
                        logger.LogError(ex, "Failed to broadcast NotificationChanged via SignalR")
                    return Ok()
                }
            return ()
        }

    member this.CreateNotification (context: UserContext, notification: Notification, ?ct: CancellationToken) =
        taskResult {
            // Permit system/admin context or the user themselves to push notifications

            let! userId = 
                match context with
                | UserContext.Authenticated (uId, _) -> Ok uId
                | _ -> Error "Access denied: cannot create notification for unauthenticated user"
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct |> Option.defaultValue CancellationToken.None)
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do!
                match context, tenant with
                | UserContext.Authenticated (uId, _), _ when uId = notification.UserId || context.IsInRole Role.Admin -> Ok()
                | _, t when t.IsManager userId -> Ok ()
                | _ -> Error "Access denied: unauthorized to create this notification"

            let! _ = 
                runInitAsync<Notification, NotificationEvent, string>
                    eventStore
                    messageSenders
                    notification
                    ct
            do! 
                task {
                    try
                        do! hubContext.Clients.All.SendAsync("NotificationChanged", notification.UserId.Value.ToString())
                    with ex ->
                        logger.LogError(ex, "Failed to broadcast NotificationChanged via SignalR")
                    return Ok()
                }
            return ()
        }

    interface INotificationService with
        member this.GetUnreadNotificationsForUserAsync (context, ?ct) =
            this.GetUnreadNotificationsForUser(context, ?ct = ct)
        member this.GetAllNotificationsForUserAsync (context, ?ct) =
            this.GetAllNotificationsForUser(context, ?ct = ct)
        member this.MarkAsReadAsync (context, notificationId, ?ct) =
            this.MarkAsRead(context, notificationId, ?ct = ct)
        member this.CreateNotificationAsync (context, notification, ?ct) =
            this.CreateNotification(context, notification, ?ct = ct)
