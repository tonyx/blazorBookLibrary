
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
                let ct = defaultArg ct CancellationToken.None

                let! result =
                    StateView.getAllAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        eventStore
                        (ct |> Some)
                return result |>> snd
            }

    member this.GetDistributionPointAsync(id: DistributionPointId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    StateView.getAggregateFreshStateAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        (ct |> Some)
                return result |> snd
            }

    member this.FindDistributionPointsAsync(name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                        (fun (x: DistributionPoint) -> x.Name.Value.ToLower().Contains(name.Value.ToLower()))
                        eventStore
                        (ct |> Some)
                return result |>> snd
            }

    member this.CreateDistributionPointAsync(distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
        taskResult
            {
                return!
                    runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSenders
                    distributionPoint
                    ct
            }

    interface IDistributionPointService with
        member this.GetDistributionPointAsync(id, ?ct) = this.GetDistributionPointAsync(id, ?ct=ct)
        member this.GetAllDistributionPointsAsync(?ct) = this.GetAllDistributionPointsAsync(?ct=ct)
        member this.FindDistributionPointsAsync(name, ?ct) = this.FindDistributionPointsAsync(name, ?ct=ct)
        member this.CreateDistributionPointAsync(distributionPoint, ?ct) = this.CreateDistributionPointAsync(distributionPoint, ?ct=ct)
