
namespace BookLibrary.Services
open System.Threading
open Sharpino
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open Microsoft.Extensions.Configuration
open BookLibrary.Utils

type DistributionPointService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        userViewerAsync: AggregateViewerAsync2<User>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>
    ) =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenant = tenantViewerAsync (ct |> Some) context.TenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    new(secretsReader: SecretsReader, configuration: IConfiguration) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        DistributionPointService(eventStore, messageSenders, userViewerAsync, tenantViewerAsync)
    member this.GetAllDistributionPointsAsync(context: UserContext, ?ct: CancellationToken) = 
        taskResult
            {
                let! result =
                    StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        (fun (x: DistributionPoint) -> x.TenantId = context.TenantId)
                        eventStore
                        ct
                return result |>> snd
            }

    member this.GetDistributionPointAsync(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) = 
        taskResult
            {
                let! result = 
                    StateView.getAggregateFreshStateAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        ct
                    |> TaskResult.map snd 
                do!
                    result.TenantId = context.TenantId
                    |> Result.ofBool $"Distribution point {id.Value} not found for tenant {context.TenantId}"
                
                return result 
            }

    member this.FindDistributionPointsAsync(context: UserContext, name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let! result = 
                    StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        (fun (x: DistributionPoint) -> x.Name.Value.ToLower().Contains(name.Value.ToLower()) && context.TenantId = x.TenantId )
                        eventStore
                        ct
                return result |>> snd
            }

    member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
        taskResult
            {
                do! checkIsGlobalAdminOrTenantManager context (defaultArg ct CancellationToken.None)
                do! 
                    distributionPoint.TenantId = context.TenantId
                    |> Result.ofBool $"Distribution point tenant id {distributionPoint.TenantId} does not match user tenant id {context.TenantId}"

                return!
                    runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSenders
                    distributionPoint
                    ct
            }

    interface IDistributionPointService with
        member this.GetDistributionPointAsync(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) = 
            this.GetDistributionPointAsync(context, id, ?ct=ct)
        member this.GetAllDistributionPointsAsync(context: UserContext, ?ct: CancellationToken) = 
            this.GetAllDistributionPointsAsync(context, ?ct=ct)
        member this.FindDistributionPointsAsync(context: UserContext, name: Name, ?ct: CancellationToken) = 
            this.FindDistributionPointsAsync(context, name, ?ct=ct)
        member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
            this.CreateDistributionPointAsync(context, distributionPoint, ?ct=ct)
