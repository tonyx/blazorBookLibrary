
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

type TenantService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        configuration: IConfiguration,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userViewerAsync: AggregateViewerAsync2<User>,
        logger: ILogger<ITenantService>
    ) =
    new 
        (secretsReader: SecretsReader, 
        configuration: IConfiguration,
        logger: ILogger<ITenantService>) =
            let connectionString = secretsReader.GetBookLibraryConnectionString()
            let messageSenders = MessageSenders.NoSender
            let eventStore = PgStorage.PgEventStore connectionString
            let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, BookLibrary.Domain.TenantEvent, string> eventStore
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, BookLibrary.Domain.UserEvent, string> eventStore
            TenantService(eventStore, messageSenders, configuration, tenantViewerAsync, userViewerAsync, logger)

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
                do!
                    match context, tenant with
                    | UserContext.Anonymous, _ -> Error "Anonymous users cannot create tenants"
                    | UserContext.Authenticated(userId, _, _), tenant when tenant.OwnerId <> userId -> Error "User is not the owner of the tenant"
                    | UserContext.Authenticated(userId, _, _), tenant when tenant.OwnerId = userId -> Ok()
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
                    tenant.Public || 
                    (match context with
                     | UserContext.Anonymous -> false
                     | UserContext.Authenticated(userId, _, _) when userId = tenant.OwnerId -> true
                     | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
                     | _ -> false)

                if allowed then
                    return tenant
                else
                    return! Error "Access denied to private tenant"
            }

        interface ITenantService with
            member this.EnsureDefaultTenantExistsAsync (userId: UserId,?ct: CancellationToken) =
                this.EnsureDefaultTenantExists(userId, ?ct = ct)
            member this.CreateTenantAsync (context: UserContext, tenant: Tenant, ?ct: CancellationToken) =
                this.CreateTenant(context, tenant, ?ct = ct)
            member this.GetTenantAsync (context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
                this.GetTenant(context, tenantId, ?ct = ct)