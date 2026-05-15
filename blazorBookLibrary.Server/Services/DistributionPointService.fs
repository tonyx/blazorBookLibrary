
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
        userViewerAsync: AggregateViewerAsync2<User>
    ) =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! rolesForThisTenant = userRolesForTenant userViewerAsync context.TenantId context.UserId (ct |> Some)
            let allowed = 
                match context with
                | UserContext.Anonymous -> false
                | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
                | UserContext.Authenticated _ when rolesForThisTenant |> (List.exists (fun r -> r = Role.Manager || r = Role.Admin)) -> true
                | _ -> false
            do! 
                allowed
                |> Result.ofBool "Updating of book allowed only to admins or managers"
            return ()   
        }
    new(secretsReader: SecretsReader, configuration: IConfiguration) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        DistributionPointService(eventStore, messageSenders, userViewerAsync)
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
                do!  
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Creation of distribution point allowed only to admins"
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
