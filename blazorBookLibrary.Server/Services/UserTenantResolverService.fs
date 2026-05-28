
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
    userViewerAsync: AggregateViewerAsync2<User>
) =
    new (secretsReader: SecretsReader)
        =
            let connectionString = secretsReader.GetBookLibraryConnectionString ()
            let eventStore = PgStorage.PgEventStore connectionString
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
            UserTenantResolverService(eventStore, MessageSenders.NoSender, userViewerAsync)

    member this.GetTenantForUser(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            match context.TenantId with
            | Some tenantId -> return tenantId
            | None ->
                if context.IsAnonymous then
                    return TenantId.Default
                else
                    let userId = context.UserId.Value

                    let! user = userViewerAsync ct userId.Value |> TaskResult.map snd
                    return user.CurrentTenant
        }
    interface IUserTenantResolverService with 
        member this.GetTenantForUserAsync(context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetTenantForUser(context, ct)


        
