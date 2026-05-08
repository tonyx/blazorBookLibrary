
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
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Shared
open BookLibrary.Utils

type DistributionPointService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders
    ) =
    new(secretsReader: SecretsReader, configuration: IConfiguration) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender
        DistributionPointService(eventStore, messageSenders)
    member this.GetAllDistributionPointsAsync(?ct: CancellationToken) = 
        taskResult
            {
                let! result =
                    StateView.getAllAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        eventStore
                        ct
                return result |>> snd
            }

    member this.GetDistributionPointAsync(id: DistributionPointId, ?ct: CancellationToken) = 
        taskResult
            {
                let! result = 
                    StateView.getAggregateFreshStateAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        ct
                return result |> snd
            }

    member this.FindDistributionPointsAsync(name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let! result = 
                    StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        (fun (x: DistributionPoint) -> x.Name.Value.ToLower().Contains(name.Value.ToLower()))
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

                return!
                    runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSenders
                    distributionPoint
                    ct
            }

    interface IDistributionPointService with
        member this.GetDistributionPointAsync(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) = 
            this.GetDistributionPointAsync(id, ?ct=ct)
        member this.GetAllDistributionPointsAsync(context: UserContext, ?ct: CancellationToken) = 
            this.GetAllDistributionPointsAsync(?ct=ct)
        member this.FindDistributionPointsAsync(context: UserContext, name: Name, ?ct: CancellationToken) = 
            this.FindDistributionPointsAsync(name, ?ct=ct)
        member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
            this.CreateDistributionPointAsync(context, distributionPoint, ?ct=ct)
