
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

        member this.EnsureDefaultTenantExists (?ct: CancellationToken) =
            taskResult {
                let! defaultTenantExists = this.DefaultTenantIdExists(?ct = ct)
                if defaultTenantExists then
                    return! Ok()
                else
                    let! tenantName = TenantName.New (configuration.GetValue<string>("BooksLibrary:DefaultTenantName", "Public Library"))
                    let initialInstance = Tenant.New(tenantName, "Elm Street")
                    let! result  =
                        runInitAsync<Tenant, TenantEvent, string>
                            eventStore
                            messageSenders
                            initialInstance
                            ct
                    return result
            }


        interface ITenantService with
            member this.EnsureDefaultTenantExistsAsync (?ct: CancellationToken) =
                this.EnsureDefaultTenantExists(?ct = ct)