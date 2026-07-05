
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open Microsoft.Extensions.DependencyInjection
open BookLibrary.Services.UserMapping
open Microsoft.Extensions.Logging
open BookLibrary.Utils

type UserTenantResolverService(
    eventStore: IEventStore<string>,
    messageSenders: MessageSenders,
    userViewerAsync: AggregateViewerAsync2<User>,
    cookieService: ICookieService
) =
    let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

    let isTenantPrivate (tenantId: TenantId, ct: CancellationToken) =
        task {
            if tenantId = TenantId.Default then
                return Ok false
            else
                let! tenantRes = tenantViewerAsync (Some ct) tenantId.Value
                match tenantRes with
                | Ok (_, tenant) -> return Ok tenant.TenantVisibility.IsPrivate
                | Error _ -> return Ok false
        }

    new (eventStore: IEventStore<string>, messageSenders: MessageSenders, userViewerAsync: AggregateViewerAsync2<User>) =
        UserTenantResolverService(eventStore, messageSenders, userViewerAsync, null)

    new (secretsReader: SecretsReader, cookieService: ICookieService)
        =
            let connectionString = secretsReader.GetBookLibraryConnectionString ()
            let eventStore = PgStorage.PgEventStore connectionString
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
            UserTenantResolverService(eventStore, MessageSenders.NoSender, userViewerAsync, cookieService)

    member this.GetTenantForUser (context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! resolvedTenantId =
                taskResult {
                    match context.TenantId with
                    | Some tenantId -> return tenantId
                    | None ->
                        if context.IsAnonymous then
                            if System.Object.ReferenceEquals(cookieService, null) then
                                return TenantId.Default
                            else
                                let! cookieVal = cookieService.GetCookieAsync("selected_tenant_id")
                                match cookieVal with
                                | Some guidStr ->
                                    match System.Guid.TryParse(guidStr) with
                                    | true, guid -> return TenantId guid
                                    | _ -> return TenantId.Default
                                | None -> return TenantId.Default
                        else
                            let userId = context.UserId.Value
                            let! user = userViewerAsync ct userId.Value |> TaskResult.map snd
                            return user.CurrentTenant
                }

            if context.IsAnonymous then
                let! isPrivate = isTenantPrivate (resolvedTenantId, ctValue)
                if isPrivate then
                    return TenantId.Default
                else
                    return resolvedTenantId
            else
                return resolvedTenantId
        }

    interface IUserTenantResolverService with 
        member this.GetTenantForUserAsync (context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetTenantForUser (context, ct)


        
