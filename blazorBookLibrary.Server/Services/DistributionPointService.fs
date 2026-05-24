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
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        bookService: IBookService,
        userTenantResolverService: IUserTenantResolverService
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
            bookService: IBookService,
            userTenantResolverService: IUserTenantResolverService
        ) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender

        let userViewerAsync =
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore

        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

        DistributionPointService(
            eventStore,
            messageSenders,
            userViewerAsync,
            tenantViewerAsync,
            bookService,
            userTenantResolverService
        )

    member this.GetAllDistributionPointsAsync(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ?ct = ct)

            let! result =
                StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                    (fun (x: DistributionPoint) -> x.TenantId = tenantId)
                    eventStore
                    ct

            return result |>> snd
        }

    member this.GetBooksOfADistributionPoint(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) =
        taskResult {
            let searchCriteria =
                BookSearchCriteria(fun (x: Book) ->
                    x.DistributionPoint.IsSome && x.DistributionPoint.Value.Value = id.Value)

            let! result = bookService.GetAllAsync(context, searchCriteria)
            return result
        }

    member this.GetDistributionPointAsync(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ctValue)

            let! result =
                StateView.getAggregateFreshStateAsync<DistributionPoint, DistributionPointEvent, string>
                    id.Value
                    eventStore
                    ct
                |> TaskResult.map snd

            do!
                result.TenantId = tenantId
                |> Result.ofBool $"Distribution point {id.Value} not found for tenant {tenantId}"

            return result
        }

    member this.FindDistributionPointsAsync(context: UserContext, name: Name, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ctValue)

            let! result =
                StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                    (fun (x: DistributionPoint) ->
                        x.Name.Value.ToLower().Contains(name.Value.ToLower()) && tenantId = x.TenantId)
                    eventStore
                    ct

            return result |>> snd
        }

    member this.CreateDistributionPointAsync
        (context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken)
        =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            do! checkIsGlobalAdminOrTenantManager context ctValue
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ctValue)

            do!
                distributionPoint.TenantId = tenantId
                |> Result.ofBool
                    $"Distribution point tenant id {distributionPoint.TenantId} does not match user tenant id {tenantId}"

            return!
                runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSenders
                    distributionPoint
                    ct
        }

    member this.RemoveDistributionPointAsync
        (context: UserContext, distributionPointId: DistributionPointId, ?ct: CancellationToken)
        =
        let ctValue = defaultArg ct CancellationToken.None

        taskResult {
            do! checkIsGlobalAdminOrTenantManager context ctValue
            let! books = this.GetBooksOfADistributionPoint(context, distributionPointId, ?ct = ct)

            do!
                books.Length = 0
                |> Result.ofBool $"Distribution point has {books.Length} books. Please remove all books first."

            let! result =
                runDeleteAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSenders
                    distributionPointId.Value
                    (fun _ -> books.Length = 0)
                    (ctValue |> Some)

            return result
        }

    member this.GetAllDistributionPointsOfTenantAsync
        (context: UserContext, tenantId: TenantId, ?ct: CancellationToken)
        =
        let ctValue = defaultArg ct CancellationToken.None

        taskResult {
            let! userId =
                match context with
                | UserContext.Anonymous -> Error "Anonymous users cannot access distribution points"
                | UserContext.Authenticated(userId, _) -> Ok userId

            let! selectedTenantId = userTenantResolverService.GetTenantForUserAsync(context, ctValue)

            do!
                selectedTenantId = tenantId
                |> Result.ofBool "User is not authorized to access this tenant"

            let! result =
                StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string>
                    (fun (x: DistributionPoint) -> x.TenantId = tenantId)
                    eventStore
                    ct

            return result |>> snd
        }

    member this.IsRemovableAsync
        (context: UserContext, distributionPointId: DistributionPointId, ?ct: CancellationToken)
        =
        let ctValue = defaultArg ct CancellationToken.None

        taskResult {
            let! books = this.GetBooksOfADistributionPoint(context, distributionPointId, ?ct = ct)
            return books.Length = 0
        }

    interface IDistributionPointService with
        member this.GetDistributionPointAsync(context: UserContext, id: DistributionPointId, ?ct: CancellationToken) =
            this.GetDistributionPointAsync(context, id, ?ct = ct)

        member this.GetAllDistributionPointsOfATenantAsync
            (context: UserContext, tenantId: TenantId, ct: CancellationToken option)
            : Tasks.Task<Result<List<DistributionPoint>, string>> =
            this.GetAllDistributionPointsOfTenantAsync(context, tenantId, ?ct = ct)

        member this.GetAllBooksOfADistributionPointAsync
            (context: UserContext, distributionPointId: DistributionPointId, ?ct: CancellationToken)
            =
            this.GetBooksOfADistributionPoint(context, distributionPointId, ?ct = ct)

        member this.IsRemovableAsync
            (context: UserContext, distributionPointId: DistributionPointId, ?ct: CancellationToken)
            =
            this.IsRemovableAsync(context, distributionPointId, ?ct = ct)

        member this.GetAllDistributionPointsAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetAllDistributionPointsAsync(context, ?ct = ct)

        member this.FindDistributionPointsAsync(context: UserContext, name: Name, ?ct: CancellationToken) =
            this.FindDistributionPointsAsync(context, name, ?ct = ct)

        member this.CreateDistributionPointAsync
            (context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken)
            =
            this.CreateDistributionPointAsync(context, distributionPoint, ?ct = ct)

        member this.RemoveDistributionPointAsync
            (context: UserContext, distributionPointId: DistributionPointId, ?ct: CancellationToken)
            =
            this.RemoveDistributionPointAsync(context, distributionPointId, ?ct = ct)
