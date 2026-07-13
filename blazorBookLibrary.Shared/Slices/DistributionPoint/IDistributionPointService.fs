namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IDistributionPointService =
    abstract member CreateDistributionPointAsync:
        context: UserContext *
        distributionPoint: DistributionPoint *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member GetDistributionPointAsync:
        context: UserContext *
        id: DistributionPointId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<DistributionPoint, string>>

    // this will implicitly get the distribution points of a tenannt encoded in the usercontext
    abstract member GetAllDistributionPointsAsync:
        context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<DistributionPoint>, string>>

    abstract member AddReferenceUser:
        context: UserContext *
        distributionPointId: DistributionPointId *
        userId: UserId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member RemoveReferenceUser:
        context: UserContext *
        distributionPointId: DistributionPointId *
        userId: UserId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member GetAllDistributionPointsManagedByUser:
        context: UserContext * userId: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<DistributionPoint>, string>>

    // this will use the context only to validate permissions and will get the tenant id from the explicit parameter
    abstract member GetAllDistributionPointsOfATenantAsync:
        context: UserContext * tenantId: TenantId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<DistributionPoint>, string>>

    abstract member IsRemovableAsync:
        context: UserContext *
        distributionPointId: DistributionPointId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<bool, string>>

    abstract member GetAllBooksOfADistributionPointAsync:
        context: UserContext *
        distributionPointId: DistributionPointId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<Book>, string>>

    abstract member FindDistributionPointsAsync:
        context: UserContext * name: Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<DistributionPoint>, string>>

    abstract member RemoveDistributionPointAsync:
        context: UserContext *
        id: DistributionPointId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>
