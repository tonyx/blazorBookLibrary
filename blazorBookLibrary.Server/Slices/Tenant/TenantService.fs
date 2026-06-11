namespace BookLibrary.Services

open System.Threading
open System
open Sharpino
open Sharpino.CommandHandler
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open Sharpino.Cache
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open blazorBookLibrary.Shared.Infrastructure.Services
open Microsoft.AspNetCore.Http

type TenantService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        configuration: IConfiguration,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userViewerAsync: AggregateViewerAsync2<User>,
        mailNotificator: IMailNotificator,
        mailBodyRetriever: IMailBodyRetriever,
        bookService: IBookService,
        authorService: IAuthorService,
        userTenantResolverService: IUserTenantResolverService,
        notificationService: INotificationService,
        logger: ILogger<ITenantService>,
        httpContextAccessor: IHttpContextAccessor
    ) =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    let checkIsGlobalAdminOrTenantManagerOrSelf (context: UserContext) (ct: CancellationToken) (userId: UserId) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrSelf tenant context userId
        }

    new
        (
            secretsReader: SecretsReader,
            configuration: IConfiguration,
            mailNotificator: IMailNotificator,
            mailBodyRetriever: IMailBodyRetriever,
            bookService: IBookService,
            authorService: IAuthorService,
            userTenantResolverService: IUserTenantResolverService,
            logger: ILogger<ITenantService>
        ) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString

        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, BookLibrary.Domain.TenantEvent, string> eventStore

        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, BookLibrary.Domain.UserEvent, string> eventStore

        TenantService(
            eventStore,
            messageSenders,
            configuration,
            tenantViewerAsync,
            userViewerAsync,
            mailNotificator,
            mailBodyRetriever,
            bookService,
            authorService,
            userTenantResolverService,
            Unchecked.defaultof<INotificationService>,
            logger,
            null
        )

    new
        (
            secretsReader: SecretsReader,
            configuration: IConfiguration,
            mailNotificator: IMailNotificator,
            mailBodyRetriever: IMailBodyRetriever,
            logger: ILogger<ITenantService>,
            bookService: IBookService,
            authorService: IAuthorService,
            userTenantResolverService: IUserTenantResolverService,
            notificationService: INotificationService,
            httpContextAccessor: IHttpContextAccessor
        ) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString

        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, BookLibrary.Domain.TenantEvent, string> eventStore

        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, BookLibrary.Domain.UserEvent, string> eventStore

        TenantService(
            eventStore,
            messageSenders,
            configuration,
            tenantViewerAsync,
            userViewerAsync,
            mailNotificator,
            mailBodyRetriever,
            bookService,
            authorService,
            userTenantResolverService,
            notificationService,
            logger,
            httpContextAccessor
        )

    member private this.DefaultTenantIdExists(?ct: CancellationToken) =
        task {
            let! exists = tenantViewerAsync ct TenantId.Default.Value
            return exists.IsOk
        }

    member this.EnsureDefaultTenantExists(userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! defaultTenantExists = this.DefaultTenantIdExists(?ct = ct)

            if defaultTenantExists then
                return! Ok()
            else
                let initialInstance =
                    Tenant.NewDefault(userId, TenantName.New "Default" |> Result.get, "")

                let! result = runInitAsync<Tenant, TenantEvent, string> eventStore messageSenders initialInstance ct
                return result
        }

    member this.CreateTenant(context: UserContext, tenant: Tenant, ?ct: CancellationToken) =
        taskResult {
            let! ownedTenants = this.GetMyOwnedTenants(context, ?ct = ct)
            let maxTenants = configuration.GetValue<int>("BooksLibrary:MaxTenantsPerUser", 3)
            let ctVal = ct |> Option.defaultValue CancellationToken.None

            do!
                ownedTenants |> List.length <= maxTenants
                |> Result.ofBool "User has reached the maximum number of tenants"

            do!
                ownedTenants
                |> List.exists (fun (t: Tenant) -> t.Name = tenant.Name)
                |> not
                |> Result.ofBool $"Tenant name {tenant.Name} already exists"

            do! checkIsGlobalAdminOrTenantManagerOrSelf context ctVal tenant.OwnerId

            let! result = runInitAsync<Tenant, TenantEvent, string> eventStore messageSenders tenant ct
            return result
        }

    member this.GetTenant(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! tenant = tenantViewerAsync ct tenantId.Value |> TaskResult.map snd

            let allowed =
                tenant.TenantVisibility.IsPublic || this.IsMemberOrAdmin(context, tenant)

            if allowed then
                return tenant
            else
                return! Error "Access denied to private tenant"
        }

    member private this.IsOnwerOrAdmin(context: UserContext, tenant: Tenant) =
        match context with
        | UserContext.Anonymous -> false
        | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | _ -> false

    member private this.IsOnwerOrAdminOrTenantManager (context: UserContext, tenant: Tenant) =
        match context with
        | UserContext.Anonymous -> false
        | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | UserContext.Authenticated(userId, _) when tenant.IsManager(userId) -> true
        | _ -> false

    member private this.IsMemberOrAdmin(context: UserContext, tenant: Tenant) =
        match context with
        | UserContext.Anonymous -> false
        | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | UserContext.Authenticated(userId, _) when tenant.Patrons |> List.exists (fun (u, _) -> u = userId) -> true
        | _ -> false

    member private this.IsInvitedOrAdmin(context: UserContext, tenant: Tenant) =
        match context with
        | UserContext.Anonymous -> false
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | UserContext.Authenticated(userId, _) when tenant.InvitedPatrons |> List.exists (fun (u, _) -> u = userId) ->
            true
        | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
        | _ -> false

    member private this.IsAdmin(context: UserContext) =
        match context with
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | _ -> false

    member this.GetUserRole(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! tenant = this.GetTenant(context, tenantId, ?ct = ct)

            match tenant.GetUserRole userId with
            | Some role -> return role
            | None -> return! Error "User is not a patron of this tenant"
        }

    member this.AddPatron
        (context: UserContext, tenantId: TenantId, userId: UserId, role: PatronRole, ?ct: CancellationToken)
        =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let command = TenantCommand.AddPatron(userId, role)
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.DemotePatron(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            let command = TenantCommand.DemotePatron userId
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.PromotePatron(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let command = TenantCommand.PromotePatron userId
            let ctVal = ct |> Option.defaultValue CancellationToken.None

            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.RemovePatron(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            let command = TenantCommand.RemovePatron userId
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.InvitePatron(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =

        // todo: use a smart way to detect failed reads from config
        let senderAddress =
            configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")

        let senderName =
            configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")

        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let! (_, user) = userViewerAsync ct userId.Value

            do!
                this.IsOnwerOrAdminOrTenantManager(context, tenant) 
                |> Result.ofBool "Access denied: only owner or admin can invite patrons"

            let shortLang = ShortLang.New(Globalization.CultureInfo.CurrentCulture.Name)

            let! emailSubject =
                mailBodyRetriever.GetPatronInvitationSubject(
                    tenant.Name,
                    user.AppUserInfo.UserName,
                    shortLang,
                    ?ct = ct
                )

            let! emailBody = mailBodyRetriever.GetPatronInvitationTextMailAsync(shortLang, ?ct = ct)

            let patronInvitationCode = PatronInvitationCode.New()

            let baseUrl =
                if isNull httpContextAccessor then
                    Utils.getFallbackUrl ()
                else
                    match httpContextAccessor.HttpContext with
                    | null -> Utils.getFallbackUrl ()
                    | ctx ->
                        let request = ctx.Request
                        $"{request.Scheme}://{request.Host}{request.PathBase}"

            let confirmationLink =
                $"{baseUrl}/Account/AcceptInvitation?tenantId={tenantId.Value}&code={patronInvitationCode.Value}"

            let command = TenantCommand.InvitePatron(userId, patronInvitationCode)

            let substitutedSubject =
                emailSubject.Replace("{tenantName}", tenant.Name.Value).Replace("{userName}", user.AppUserInfo.UserName)

            let joinPinInfo =
                match tenant.CurrentJoinPin with
                | Some pin ->
                    if shortLang.Value.StartsWith("it", StringComparison.OrdinalIgnoreCase) then
                        $"<p>In alternativa, se preferisci unirti manualmente, puoi andare nella sezione \"Biblioteche\" e inserire questo PIN a 6 cifre: <strong>{pin}</strong></p>"
                    else
                        $"<p>Alternatively, if you prefer to join manually, you can navigate to \"Libraries\" and enter this 6-digit PIN: <strong>{pin}</strong></p>"
                | None -> ""

            let substitutedBody =
                emailBody
                    .Replace("{tenantName}", tenant.Name.Value)
                    .Replace("{userName}", user.AppUserInfo.UserName)
                    .Replace("{urlToClick}", confirmationLink)
                    .Replace("{joinPinInfo}", joinPinInfo)

            let! result =
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct

            // Create in-app notification for the user
            let inAppNotif =
                Notification.New(
                    userId,
                    $"Invitation to Join {tenant.Name.Value}",
                    $"You have been invited to join the library '{tenant.Name.Value}' as a patron.",
                    ($"/Account/AcceptInvitation?tenantId={tenantId.Value}&code={patronInvitationCode.Value}")
                )

            if not (System.Object.ReferenceEquals(notificationService, null)) then
                let! _ =
                    notificationService.CreateNotificationAsync(
                        UserContext.Authenticated(tenant.OwnerId, [ Role.Admin ]),
                        inAppNotif,
                        ?ct = ct
                    )

                ()

            do!
                task {
                    try
                        do!
                            mailNotificator.SendEmailAsync(
                                senderAddress,
                                senderName,
                                user.AppUserInfo.Email,
                                substitutedSubject,
                                substitutedBody
                            )
                    with ex ->
                        logger.LogWarning(
                            "Email delivery failed for invitation (in-app notification was successfully delivered): {Message}",
                            ex.Message
                        )
                }

            return ()
        }

    member this.ConvertInvitedPatronToPatron
        (context: UserContext, tenantId: TenantId, patronInvitationCode: PatronInvitationCode, ?ct: CancellationToken)
        =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value

            if this.IsInvitedOrAdmin(context, tenant) then
                let command = TenantCommand.ConvertInvitedPatronToPatron patronInvitationCode

                return!
                    runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                        tenantId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        ct
            else
                return! Error "Access denied: only owner or admin can convert invited patrons to patrons"
        }

    member this.RevokePatronInvitation
        (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken)
        =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let command = TenantCommand.RevokePatronInvitation userId
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.GetAllPublicTenants(context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None
        let filter = fun (tenant: Tenant) -> tenant.TenantVisibility.IsPublic

        taskResult {
            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            return tenants |>> snd
        }

    member this.GetAllowedTenants(context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult {
            do!
                match context with
                | UserContext.Anonymous -> Error "Access denied: only authenticated users can get tenants"
                | UserContext.Authenticated _ -> Ok()

            let userId = context.UserId.Value

            let filter =
                fun (tenant: Tenant) ->
                    context.IsInRole Role.Admin
                    || tenant.TenantVisibility.IsPublic
                    || tenant.OwnerId = userId
                    || tenant.Patrons |> List.exists (fun (u, _) -> u = userId)

            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            return tenants |>> snd
        }

    member this.GetMyTenants(context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult {
            do!
                match context with
                | UserContext.Anonymous -> Error "Access denied: only authenticated users can get tenants"
                | UserContext.Authenticated _ -> Ok()

            let userId = context.UserId.Value

            let filter =
                fun (tenant: Tenant) ->
                    tenant.OwnerId = context.UserId.Value
                    || tenant.Patrons |> List.exists (fun (u, _) -> u = userId)

            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            return tenants |>> snd
        }

    member this.GetMyOwnedTenants (context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult {
            do!
                match context with
                | UserContext.Anonymous -> Error "Access denied: only authenticated users can get tenants"
                | UserContext.Authenticated _ -> Ok()

            let userId = context.UserId.Value
            let filter = fun (tenant: Tenant) -> tenant.OwnerId = context.UserId.Value

            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            return tenants |>> snd
        }

    member this.SetPublic (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.SetPublic

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.SetPrivate(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let command = TenantCommand.SetPrivate
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }


    member this.SetReservationNotificationPreferenceAsync(context: UserContext, tenantId: TenantId, notificationPreference: NotificationPreference, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.SetReservationNotificationPreference notificationPreference

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.SetLoanNotificationPreferenceAsync(context: UserContext, tenantId: TenantId, notificationPreference: NotificationPreference, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.SetLoanNotificationPreference notificationPreference

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.RequestPublicAsync(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.RequestPublic

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.SuspendPatron
        (context: UserContext, tenantId: TenantId, userId: UserId, reason: string, ?ct: CancellationToken)
        =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.SuspendPatron(userId, reason)

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.ReAdmittPatron(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.ReadmittPatron userId

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.AddTagAsync
        (
            context: UserContext,
            tenantId: TenantId,
            tag: Tag,
            ?ct: CancellationToken
        ) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! (_, tenant) = tenantViewerAsync (ctValue |> Some) tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.AddTag(tag)

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.RemoveTagAsync
        (
            context: UserContext,
            tenantId: TenantId,
            tag: Tag,
            ?ct: CancellationToken
        ) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! (_, tenant) = tenantViewerAsync (ctValue |> Some) tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let command = TenantCommand.RemoveTag(tag)

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }   

    member this.DeleteTenant(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! (_, tenant) = tenantViewerAsync (ctValue |> Some) tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            // Strict backend safety checks querying event streams directly
            let! books =
                StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string>
                    (fun b -> b.TenantId = tenantId)
                    eventStore
                    (Some ctValue)

            do!
                books.IsEmpty
                |> Result.ofBool $"Tenant has {books.Length} books. Cannot delete."

            let! authors =
                StateView.getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string>
                    (fun a -> a.TenantId = tenantId)
                    eventStore
                    (Some ctValue)

            do!
                authors.IsEmpty
                |> Result.ofBool $"Tenant has {authors.Length} authors. Cannot delete."

            let! dps =
                StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                    (fun dp -> dp.TenantId = tenantId)
                    eventStore
                    (Some ctValue)

            do!
                dps.IsEmpty
                |> Result.ofBool $"Tenant has {dps.Length} distribution points. Cannot delete."

            do!
                tenant.Patrons.IsEmpty
                |> Result.ofBool $"Tenant has {tenant.Patrons.Length} patrons. Cannot delete."

            let! result =
                runDeleteAsync<Tenant, TenantEvent, string>
                    eventStore
                    messageSenders
                    tenantId.Value
                    (fun _ -> true)
                    (ctValue |> Some)

            return result
        }

    member this.GenerateJoinPin(context: UserContext, tenantId: TenantId, pin: string, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let command = TenantCommand.GenerateJoinPin2 pin
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct
        }

    member this.SubmitJoinRequest(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value

            do!
                match context with
                | UserContext.Authenticated(uId, _) when uId = userId || context.IsInRole Role.Admin -> Ok()
                | _ -> Error "Access denied: cannot submit a join request for another user"

            let command = TenantCommand.SubmitJoinRequest2 userId

            let! result =
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct

            // Add in-app notification to the Owner of the Tenant:
            let! (_, user) = userViewerAsync ct userId.Value

            let inAppNotif =
                Notification.New(
                    tenant.OwnerId,
                    "New Join Request",
                    $"User {user.AppUserInfo.UserName} has requested to join your library '{tenant.Name.Value}'.",
                    $"/tenants/{tenantId.Value}/approvals?requesterId={userId.Value}"
                )

            if not (System.Object.ReferenceEquals(notificationService, null)) then
                let! _ =
                    notificationService.CreateNotificationAsync(
                        UserContext.Authenticated(userId, [ Role.Admin ]),
                        inAppNotif,
                        ?ct = ct
                    )

                ()

            return result
        }

    member this.ApproveJoinRequest(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let command = TenantCommand.ApproveJoinRequest2 userId

            let! result =
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct

            // Add in-app notification to the approved user:
            let inAppNotif =
                Notification.New(
                    userId,
                    "Join Request Approved",
                    $"Your request to join the library '{tenant.Name.Value}' has been approved!",
                    "/tenants"
                )

            if not (System.Object.ReferenceEquals(notificationService, null)) then
                let! _ = notificationService.CreateNotificationAsync(context, inAppNotif, ?ct = ct)
                ()

            return result
        }

    member this.RejectJoinRequest(context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let ctVal = ct |> Option.defaultValue CancellationToken.None

            let command = TenantCommand.RejectJoinRequest2 userId

            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let! result =
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    ct

            // Add in-app notification to the rejected user:
            let inAppNotif =
                Notification.New(
                    userId,
                    "Join Request Rejected",
                    $"Your request to join the library '{tenant.Name.Value}' was not approved.",
                    "/tenants"
                )

            if not (System.Object.ReferenceEquals(notificationService, null)) then
                let! _ = notificationService.CreateNotificationAsync(context, inAppNotif, ?ct = ct)
                ()

            return result
        }

    member this.FindTenantByJoinPin(pin: string, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult {
            let filter =
                fun (tenant: Tenant) ->
                    match tenant.CurrentJoinPin with
                    | Some p -> p = pin
                    | None -> false

            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            match tenants with
            | [] -> return! Error $"No library found with PIN {pin}"
            | [ (_, tenant) ] -> return tenant
            | ((_, tenant) :: _) -> return tenant
        }

    member this.GetTenantsRequstingPublicAsync(context: UserContext, ?ct: CancellationToken) =
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult {
            do!
                match context with
                | UserContext.Anonymous -> Error "not allowed"
                | UserContext.Authenticated(_, roles) when (roles |> List.contains Role.Admin) -> Ok()
                | _ -> Error "not allowed"

            let filter =
                fun (tenant: Tenant) -> tenant.TenantVisibility = TenantVisibility.RequestedPublic

            let! tenants =
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> filter eventStore (ct |> Some)

            match tenants with
            | [] -> return []
            | (_, tenant) :: _ -> return List.map snd tenants
        }

    interface ITenantService with
        member this.EnsureDefaultTenantExistsAsync(userId: UserId, ?ct: CancellationToken) =
            this.EnsureDefaultTenantExists(userId, ?ct = ct)

        member this.CreateTenantAsync(context: UserContext, tenant: Tenant, ?ct: CancellationToken) =
            this.CreateTenant(context, tenant, ?ct = ct)

        member this.GetTenantAsync(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
            this.GetTenant(context, tenantId, ?ct = ct)

        member this.GetTenantsRequstingPublicAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetTenantsRequstingPublicAsync(context, ?ct = ct)

        member this.AddPatronAsync(context, tenantId, userId, role, ?ct) =
            this.AddPatron(context, tenantId, userId, role, ?ct = ct)

        member this.DemotePatronAsync(context, tenantId, userId, ?ct) =
            this.DemotePatron(context, tenantId, userId, ?ct = ct)

        member this.PromotePatronAsync(context, tenantId, userId, ?ct) =
            this.PromotePatron(context, tenantId, userId, ?ct = ct)

        member this.RemovePatronAsync(context, tenantId, userId, ?ct) =
            this.RemovePatron(context, tenantId, userId, ?ct = ct)

        member this.InvitePatronAsync(context, tenantId, userId, ?ct) =
            this.InvitePatron(context, tenantId, userId, ?ct = ct)

        member this.ConvertInvitedPatronToPatronAsync(context, tenantId, patronInvitationCode, ?ct) =
            this.ConvertInvitedPatronToPatron(context, tenantId, patronInvitationCode, ?ct = ct)

        member this.RevokePatronInvitation(context, tenantId, userId, ?ct) =
            this.RevokePatronInvitation(context, tenantId, userId, ?ct = ct)

        member this.SuspendPatron(context, tenantId, userId, reason, ?ct) =
            this.SuspendPatron(context, tenantId, userId, reason, ?ct = ct)

        member this.ReAdmittPatron(context, tenantId, userId, ?ct) =
            this.ReAdmittPatron(context, tenantId, userId, ?ct = ct)

        member this.GetUserRoleAsync(context, tenantId, userId, ?ct) =
            this.GetUserRole(context, tenantId, userId, ?ct = ct)

        member this.GetAllPublicTenantsAsync(context, ?ct) =
            this.GetAllPublicTenants(context, ?ct = ct)

        member this.GetAllowedTenantsAsync(context, ?ct) =
            this.GetAllowedTenants(context, ?ct = ct)

        member this.GetMyTenantsAsync(context, ?ct) = this.GetMyTenants(context, ?ct = ct)

        member this.GetMyOwnedTenantsAsync(context, ?ct) =
            this.GetMyOwnedTenants(context, ?ct = ct)

        member this.SetPublicAsync(context, tenantId, ?ct) =
            this.SetPublic(context, tenantId, ?ct = ct)

        member this.SetPrivateAsync(context, tenantId, ?ct) =
            this.SetPrivate(context, tenantId, ?ct = ct)

        member this.SetReservationNotificationPreferenceAsync(context, tenantId, notificationPreference, ?ct) =
            this.SetReservationNotificationPreferenceAsync(context, tenantId, notificationPreference, ?ct = ct)

        member this.SetLoanNotificationPreferenceAsync(context, tenantId, notificationPreference, ?ct) =
            this.SetLoanNotificationPreferenceAsync(context, tenantId, notificationPreference, ?ct = ct)

        member this.RequestPublicAsync(context, tenantId, ?ct) =
            this.RequestPublicAsync(context, tenantId, ?ct = ct)

        member this.DeleteTenantAsync(context, tenantId, ?ct) =
            this.DeleteTenant(context, tenantId, ?ct = ct)

        member this.AddTagAsync(context, tenantId, tag, ?ct) =
            this.AddTagAsync(context, tenantId, tag, ?ct = ct)

        member this.RemoveTagAsync(context, tenantId, tag, ?ct) =
            this.RemoveTagAsync(context, tenantId, tag, ?ct = ct)

        member this.GenerateJoinPinAsync(context, tenantId, pin, ?ct) =
            this.GenerateJoinPin(context, tenantId, pin, ?ct = ct)

        member this.SubmitJoinRequestAsync(context, tenantId, userId, ?ct) =
            this.SubmitJoinRequest(context, tenantId, userId, ?ct = ct)

        member this.ApproveJoinRequestAsync(context, tenantId, userId, ?ct) =
            this.ApproveJoinRequest(context, tenantId, userId, ?ct = ct)

        member this.RejectJoinRequestAsync(context, tenantId, userId, ?ct) =
            this.RejectJoinRequest(context, tenantId, userId, ?ct = ct)

        member this.FindTenantByJoinPinAsync(pin, ?ct) = this.FindTenantByJoinPin(pin, ?ct = ct)
