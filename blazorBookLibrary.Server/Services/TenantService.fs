
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
        logger: ILogger<ITenantService>,
        httpContextAccessor: IHttpContextAccessor
    ) =
    new 
        (secretsReader: SecretsReader, 
        configuration: IConfiguration,
        mailNotificator: IMailNotificator,
        mailBodyRetriever: IMailBodyRetriever,
        logger: ILogger<ITenantService>) =
            let connectionString = secretsReader.GetBookLibraryConnectionString()
            let messageSenders = MessageSenders.NoSender
            let eventStore = PgStorage.PgEventStore connectionString
            let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, BookLibrary.Domain.TenantEvent, string> eventStore
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, BookLibrary.Domain.UserEvent, string> eventStore
            TenantService(eventStore, messageSenders, configuration, tenantViewerAsync, userViewerAsync, mailNotificator, mailBodyRetriever, logger, null)

    new 
        (secretsReader: SecretsReader, 
        configuration: IConfiguration,
        mailNotificator: IMailNotificator,
        mailBodyRetriever: IMailBodyRetriever,
        logger: ILogger<ITenantService>,
        httpContextAccessor: IHttpContextAccessor) =
            let connectionString = secretsReader.GetBookLibraryConnectionString()
            let messageSenders = MessageSenders.NoSender
            let eventStore = PgStorage.PgEventStore connectionString
            let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, BookLibrary.Domain.TenantEvent, string> eventStore
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, BookLibrary.Domain.UserEvent, string> eventStore
            TenantService(eventStore, messageSenders, configuration, tenantViewerAsync, userViewerAsync, mailNotificator, mailBodyRetriever, logger, httpContextAccessor)

        member private this.DefaultTenantIdExists (?ct: CancellationToken) =
            task {
                let! exists = tenantViewerAsync ct TenantId.Default.Value
                return exists.IsOk
            }

        member this.EnsureDefaultTenantExists (userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! defaultTenantExists = this.DefaultTenantIdExists(?ct = ct)
                if defaultTenantExists then
                    return! Ok()
                else
                    let initialInstance = Tenant.NewDefault(userId, TenantName.New "Default" |> Result.get, "")
                    let! result  =
                        runInitAsync<Tenant, TenantEvent, string>
                            eventStore
                            messageSenders
                            initialInstance
                            ct
                    return result
            }

        member this.CreateTenant (context: UserContext, tenant: Tenant, ?ct: CancellationToken) =
            taskResult {
                let! ownedTenants = this.GetMyOwnedTenants(context, ?ct = ct)
                let maxTenants = configuration.GetValue<int>("BooksLirary:MaxTenantsPerUser", 3)
                do!
                    ownedTenants |> List.length <= maxTenants |> Result.ofBool "User has reached the maximum number of tenants"

                do!
                    ownedTenants |> List.exists (fun (t: Tenant) -> t.TentantName = tenant.TentantName)
                    |> not
                    |> Result.ofBool $"Tenant name {tenant.TentantName} already exists"

                do!
                    match context, tenant with
                    | UserContext.Anonymous, _ -> Error "Anonymous users cannot create tenants"
                    | UserContext.Authenticated(userId, _), tenant when tenant.OwnerId <> userId -> Error "User is not the owner of the tenant"
                    | UserContext.Authenticated(userId, _), tenant when tenant.OwnerId = userId -> Ok()
                    | _, _ -> Error $"Invalid context: userId {context.UserId} is not owner of tenant {tenant.OwnerId}"

                let! result = 
                    runInitAsync<Tenant, TenantEvent, string>
                        eventStore
                        messageSenders
                        tenant
                        ct
                return result
            }

        member this.GetTenant (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
            taskResult {
                let! tenant = tenantViewerAsync ct tenantId.Value |> TaskResult.map snd
                let allowed = 
                    tenant.Public || this.IsMemberOrAdmin(context, tenant)

                if allowed then
                    return tenant
                else
                    return! Error "Access denied to private tenant"
            }

        member private this.IsOnwerOrAdmin (context: UserContext, tenant: Tenant) =
            match context with
            | UserContext.Anonymous -> false
            | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
            | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
            | _ -> false

        member private this.IsMemberOrAdmin (context: UserContext, tenant: Tenant) =
            match context with
            | UserContext.Anonymous -> false
            | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> true
            | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
            | UserContext.Authenticated(userId, _) when tenant.Patrons |> List.exists (fun (u, _) -> u = userId) -> true
            | _ -> false

        member private this.IsInvitedOrAdmin (context: UserContext, tenant: Tenant) =
            match context with
            | UserContext.Anonymous -> false
            | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
            | UserContext.Authenticated(userId, _) when tenant.InvitedPatrons |> List.exists (fun (u, _) -> u = userId) -> true
            | _ -> false

        member private this.IsAdmin (context: UserContext) =
            match context with
            | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
            | _ -> false

        member this.GetUserRole (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! tenant = this.GetTenant(context, tenantId, ?ct = ct)
                match tenant.GetUserRole userId with
                | Some role -> return role
                | None -> return! Error "User is not a patron of this tenant"
            }

        member this.AddPatron (context: UserContext, tenantId: TenantId, userId: UserId, role: PatronRole, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then
                    let command = TenantCommand.AddPatron (userId, role)
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can add patrons"
            }

        member this.DemotePatron (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then
                    let command = TenantCommand.DemotePatron userId
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can demote patrons"
            }

        member this.PromotePatron (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then

                    let command = TenantCommand.PromotePatron userId
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can promote patrons"
            }

        member this.RemovePatron (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then
                    let command = TenantCommand.RemovePatron userId
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can remove patrons"
            }

        member this.InvitePatron (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =

            // todo: use a smart way to detect failed reads from config
            let senderAddress = 
                configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")
            let senderName = 
                configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                let! (_, user) =
                    userViewerAsync ct userId.Value
                do!
                    this.IsOnwerOrAdmin(context, tenant) 
                    |> Result.ofBool "Access denied: only owner or admin can invite patrons" 

                let shortLang = ShortLang.New(Globalization.CultureInfo.CurrentCulture.Name)
                let! emailSubject = mailBodyRetriever.GetPatronInvitationSubject(shortLang, ?ct = ct)
                let! emailBody = mailBodyRetriever.GetPatronInvitationTextMailAsync(shortLang, ?ct = ct)

                let patronInvitationCode = PatronInvitationCode.New ()
                let baseUrl =
                    if isNull httpContextAccessor then 
                        Utils.getFallbackUrl ()
                    else
                        match httpContextAccessor.HttpContext with
                        | null -> Utils.getFallbackUrl ()
                        | ctx ->
                            let request = ctx.Request
                            $"{request.Scheme}://{request.Host}{request.PathBase}"
                let confirmationLink = $"{baseUrl}/Account/AcceptInvitation?tenantId={tenantId.Value}&code={patronInvitationCode.Value}"
                let command = TenantCommand.InvitePatron (userId, patronInvitationCode)

                let substitutedSubject = 
                    emailSubject
                        .Replace("{tenantName}", tenant.TentantName.Value)
                        .Replace("{userName}", user.AppUserInfo.UserName)

                let substitutedBody = 
                    emailBody
                        .Replace("{tenantName}", tenant.TentantName.Value)
                        .Replace("{userName}", user.AppUserInfo.UserName)
                        .Replace("{urlToClick}", confirmationLink)

                let! result =
                    runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                        tenantId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        ct
                do!
                    task {
                        do!
                            mailNotificator.SendEmailAsync(
                                senderAddress,
                                senderName,
                                user.AppUserInfo.Email,
                                substitutedSubject,
                                substitutedBody
                            )
                    }
                return ()
            }

        member this.ConvertInvitedPatronToPatron (context: UserContext, tenantId: TenantId, patronInvitationCode: PatronInvitationCode, ?ct: CancellationToken) =
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

        member this.RevokePatronInvitation (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then
                    let command = TenantCommand.RevokePatronInvitation userId
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can revoke patron invitations"
            }

        member this.GetAllPublicTenants (context: UserContext, ?ct: CancellationToken) =
            let ct = ct |> Option.defaultValue CancellationToken.None
            let filter = 
                fun (tenant: Tenant) -> tenant.Public
            taskResult {
                do!
                    match context with
                    | UserContext.Anonymous ->  Error "Access denied: only authenticated users can get tenants"
                    | UserContext.Authenticated _ -> Ok () 
                let! tenants =
                    StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string>
                        filter
                        eventStore
                        (ct |> Some)
                return tenants |>> snd
            }

        member this.GetAllowedTenants (context: UserContext, ?ct: CancellationToken) =
            let ct = ct |> Option.defaultValue CancellationToken.None
            taskResult {
                do!
                    match context with
                    | UserContext.Anonymous ->  Error "Access denied: only authenticated users can get tenants"
                    | UserContext.Authenticated _ -> Ok () 
                let userId = context.UserId.Value
                let filter = 
                    fun (tenant: Tenant) -> 
                        tenant.Public || 
                        tenant.OwnerId = userId || 
                        tenant.Patrons |> List.exists (fun (u, _) -> u = userId)
                let! tenants =
                    StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string>
                        filter
                        eventStore
                        (ct |> Some)
                return tenants |>> snd
            }

        member this.GetMyTenants (context: UserContext, ?ct: CancellationToken) =
            let ct = ct |> Option.defaultValue CancellationToken.None

            taskResult {
                do!
                    match context with
                    | UserContext.Anonymous ->  Error "Access denied: only authenticated users can get tenants"
                    | UserContext.Authenticated _ -> Ok () 
                let userId = context.UserId.Value
                let filter = 
                    fun (tenant: Tenant) -> 
                        tenant.OwnerId = context.UserId.Value || 
                        tenant.Patrons |> List.exists (fun (u, _) -> u = userId)
                let! tenants =
                    StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string>
                        filter
                        eventStore
                        (ct |> Some)
                return tenants |>> snd
            }

        member this.GetMyOwnedTenants (context: UserContext, ?ct: CancellationToken) =
            let ct = ct |> Option.defaultValue CancellationToken.None

            taskResult {
                do!
                    match context with
                    | UserContext.Anonymous ->  Error "Access denied: only authenticated users can get tenants"
                    | UserContext.Authenticated _ -> Ok () 
                let userId = context.UserId.Value
                let filter = 
                    fun (tenant: Tenant) -> 
                        tenant.OwnerId = context.UserId.Value
                let! tenants =
                    StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string>
                        filter
                        eventStore
                        (ct |> Some)
                return tenants |>> snd
            }

        member this.SetPublic (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsAdmin(context) then
                    let command = TenantCommand.SetPublic
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only admin can set public"
            }

        member this.SetPrivate (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
            taskResult {
                let! (_, tenant) = tenantViewerAsync ct tenantId.Value
                if this.IsOnwerOrAdmin(context, tenant) then
                    let command = TenantCommand.SetPrivate
                    return! 
                        runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                            tenantId.Value
                            eventStore
                            messageSenders
                            ""
                            command
                            ct
                else
                    return! Error "Access denied: only owner or admin can set private"
            }

        interface ITenantService with
            member this.EnsureDefaultTenantExistsAsync (userId: UserId,?ct: CancellationToken) =
                this.EnsureDefaultTenantExists(userId, ?ct = ct)
            member this.CreateTenantAsync (context: UserContext, tenant: Tenant, ?ct: CancellationToken) =
                this.CreateTenant(context, tenant, ?ct = ct)
            member this.GetTenantAsync (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
                this.GetTenant(context, tenantId, ?ct = ct)
            member this.AddPatronAsync (context, tenantId, userId, role, ?ct) =
                this.AddPatron(context, tenantId, userId, role, ?ct = ct)
            member this.DemotePatronAsync (context, tenantId, userId, ?ct) =
                this.DemotePatron(context, tenantId, userId, ?ct = ct)
            member this.PromotePatronAsync (context, tenantId, userId, ?ct) =
                this.PromotePatron(context, tenantId, userId, ?ct = ct)
            member this.RemovePatronAsync (context, tenantId, userId, ?ct) =
                this.RemovePatron(context, tenantId, userId, ?ct = ct)
            member this.InvitePatronAsync (context, tenantId, userId, ?ct) =
                this.InvitePatron(context, tenantId, userId, ?ct = ct)
            member this.ConvertInvitedPatronToPatronAsync (context, tenantId, patronInvitationCode, ?ct) =
                this.ConvertInvitedPatronToPatron(context, tenantId, patronInvitationCode, ?ct = ct)
            member this.RevokePatronInvitation (context, tenantId, userId, ?ct) =
                this.RevokePatronInvitation(context, tenantId, userId, ?ct = ct)
            member this.GetUserRoleAsync (context, tenantId, userId, ?ct) =
                this.GetUserRole(context, tenantId, userId, ?ct = ct)
            member this.GetAllPublicTenantsAsync (context, ?ct) =
                this.GetAllPublicTenants(context, ?ct = ct)
            member this.GetAllowedTenantsAsync (context, ?ct) =
                this.GetAllowedTenants(context, ?ct = ct)
            member this.GetMyTenantsAsync (context, ?ct) =
                this.GetMyTenants(context, ?ct = ct)
            member this.GetMyOwnedTenantsAsync (context, ?ct) =
                this.GetMyOwnedTenants(context, ?ct = ct)
            member this.SetPublicAsync (context, tenantId, ?ct) =
                this.SetPublic(context, tenantId, ?ct = ct)
            member this.SetPrivateAsync (context, tenantId, ?ct) =
                this.SetPrivate(context, tenantId, ?ct = ct)


