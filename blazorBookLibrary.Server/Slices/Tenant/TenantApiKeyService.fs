namespace BookLibrary.Services

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Core
open Sharpino.CommandHandler
open Sharpino.Storage
open Sharpino.EventBroker
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open BookLibrary.Utils
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open blazorBookLibrary.Shared.Infrastructure.Services

type TenantApiKeyService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userViewerAsync: AggregateViewerAsync2<User>,
        configuration: IConfiguration,
        mailNotificator: IMailNotificator,
        notificationService: INotificationService,
        logger: ILogger<ITenantApiKeyService>
    ) =

    new(eventStore: IEventStore<string>, messageSenders: MessageSenders, tenantViewerAsync: AggregateViewerAsync2<Tenant>, configuration: IConfiguration) =
        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        TenantApiKeyService(
            eventStore,
            messageSenders,
            tenantViewerAsync,
            userViewerAsync,
            configuration,
            Unchecked.defaultof<IMailNotificator>,
            Unchecked.defaultof<INotificationService>,
            Unchecked.defaultof<ILogger<ITenantApiKeyService>>
        )

    new(secretsReader: SecretsReader, configuration: IConfiguration) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        TenantApiKeyService(
            eventStore,
            messageSenders,
            tenantViewerAsync,
            userViewerAsync,
            configuration,
            Unchecked.defaultof<IMailNotificator>,
            Unchecked.defaultof<INotificationService>,
            Unchecked.defaultof<ILogger<ITenantApiKeyService>>
        )

    new
        (
            secretsReader: SecretsReader,
            configuration: IConfiguration,
            mailNotificator: IMailNotificator,
            notificationService: INotificationService,
            logger: ILogger<ITenantApiKeyService>
        ) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        TenantApiKeyService(
            eventStore,
            messageSenders,
            tenantViewerAsync,
            userViewerAsync,
            configuration,
            mailNotificator,
            notificationService,
            logger
        )

    member this.AddPartnerKey(context: UserContext, tenantId: TenantId, partnerApiKey: PartnerApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let cmd = TenantCommand.AddPartnerKey partnerApiKey
            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct
        }

    member this.AddPartnerKey (context: UserContext, tenantId: TenantId, partnerId: UserId, label: string, now: DateTime, ?expiresAt: DateTime, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let newKey = CatalogApiKey.New()
            let partnerKey =
                {
                    Key = newKey
                    PartnerId = partnerId
                    Label = label
                    CreatedAt = now
                    RevokedAt = None
                    ExpiresAt = expiresAt
                }

            let cmd = TenantCommand.AddPartnerKey partnerKey
            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct
        }

    member this.RevokeCatalogKey(context: UserContext, tenantId: TenantId, catalogApiKey: CatalogApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let now = DateTime.UtcNow
            let targetKeyOpt = tenant.PartnerApiKeys |> List.tryFind (fun k -> k.Key = catalogApiKey)
            let cmd = TenantCommand.RevokeCatalogKey(catalogApiKey, now)
            let! result =
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct

            match targetKeyOpt with
            | Some targetKey ->
                let isManager = tenant.IsManager targetKey.PartnerId || tenant.IsOwner targetKey.PartnerId
                if isManager then
                    // 1. In-App Notification
                    if not (isNull (box notificationService)) then
                        let inAppNotif =
                            Notification.New(
                                targetKey.PartnerId,
                                $"API Key Revoked - {tenant.Name.Value}",
                                $"Your partner API key '{targetKey.Label}' for library '{tenant.Name.Value}' has been revoked.",
                                "/tenants"
                            )
                        let! _ =
                            task {
                                try
                                    let! _ =
                                        notificationService.CreateNotificationAsync(
                                            UserContext.Authenticated(targetKey.PartnerId, [ Role.Admin ]),
                                            inAppNotif,
                                            ?ct = ct
                                        )
                                    return Ok ()
                                with ex ->
                                    if not (isNull (box logger)) then
                                        logger.LogWarning("In-app notification delivery failed for key revocation: {Message}", ex.Message)
                                    return Ok ()
                            }
                        ()

                    // 2. Email Notification
                    if not (isNull (box mailNotificator)) && not (isNull (box userViewerAsync)) then
                        let! userOpt =
                            task {
                                try
                                    let! userRes = userViewerAsync ct targetKey.PartnerId.Value
                                    match userRes with
                                    | Ok (_, user) -> return Ok (Some user)
                                    | Error _ -> return Ok None
                                with ex ->
                                    if not (isNull (box logger)) then
                                        logger.LogWarning("Failed to retrieve user for key revocation notification: {Message}", ex.Message)
                                    return Ok None
                            }
                        match userOpt with
                        | Some user ->
                            let senderAddress =
                                configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")
                            let senderName =
                                configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")
                            let recipientEmail = user.AppUserInfo.Email
                            if not (String.IsNullOrWhiteSpace recipientEmail) then
                                let subject = $"API Key Revoked - {tenant.Name.Value}"
                                let body =
                                    $"<p>Dear {user.AppUserInfo.UserName},</p>" +
                                    $"<p>Your partner API key <strong>{targetKey.Label}</strong> for the library <strong>{tenant.Name.Value}</strong> has been revoked.</p>" +
                                    $"<p>Key: <code>{targetKey.Key.Value}</code></p>" +
                                    $"<p>If you believe this was done in error, please contact your administrator.</p>"
                                let! _ =
                                    task {
                                        try
                                            do! mailNotificator.SendEmailAsync(senderAddress, senderName, recipientEmail, subject, body)
                                            return Ok ()
                                        with ex ->
                                            if not (isNull (box logger)) then
                                                logger.LogWarning("Email delivery failed for key revocation: {Message}", ex.Message)
                                            return Ok ()
                                    }
                                ()
                        | None -> ()
            | None -> ()

            return result
        }

    member this.RemovePartnerKey(context: UserContext, tenantId: TenantId, catalogApiKey: CatalogApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let keyToRemoveOpt = tenant.PartnerApiKeys |> List.tryFind (fun k -> k.Key = catalogApiKey)
            match keyToRemoveOpt with
            | None -> return! Error $"Catalog key {catalogApiKey.Value} not found"
            | Some partnerKey ->
                let cmd = TenantCommand.RemovePartnerKey partnerKey
                return!
                    runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                        tenantId.Value
                        eventStore
                        messageSenders
                        context.Metadata
                        cmd
                        ct
        }

    member this.ValidateCatalogKey(tenantId: TenantId, catalogApiKey: CatalogApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let configuredKeyOpt =
                let cfg = configuration.["Catalog:ApiKey"]
                if String.IsNullOrWhiteSpace cfg then None
                else Some (CatalogApiKey cfg)
            return! tenant.ValidateCatalogKey(catalogApiKey, ?configuredKey = configuredKeyOpt)
        }

    member this.GetPartnerKeys(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            return tenant.PartnerApiKeys
        }

    interface ITenantApiKeyService with
        member this.AddPartnerKeyAsync(context, tenantId, partnerApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.AddPartnerKey(context, tenantId, partnerApiKey, ct)

        member this.AddPartnerKeyAsync(context, tenantId, partnerId, label, now, ?expiresAt, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.AddPartnerKey(context, tenantId, partnerId, label, now, ?expiresAt = expiresAt, ct = ct)

        member this.RevokeCatalogKeyAsync(context, tenantId, catalogApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.RevokeCatalogKey(context, tenantId, catalogApiKey, ct)

        member this.RemovePartnerKeyAsync(context, tenantId, catalogApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.RemovePartnerKey(context, tenantId, catalogApiKey, ct)

        member this.ValidateCatalogKeyAsync(tenantId, catalogApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.ValidateCatalogKey(tenantId, catalogApiKey, ct)

        member this.GetPartnerKeysAsync(context, tenantId, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.GetPartnerKeys(context, tenantId, ct)
