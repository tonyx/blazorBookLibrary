
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons

type IAdminServices = 
    abstract member PurgeVectorsReferringDroppedBooksAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AdjustBookStatesReferringMissingEmbeddingsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member AssignUserToDistributionPointAsync: context:UserContext * distributionPointId:DistributionPointId * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UnassignUserFromDistributionPointAsync: context:UserContext * distributionPointId:DistributionPointId * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateDistributionPointInfoAsync: context:UserContext * distributionPointId:DistributionPointId * info:Info * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RenameDistributionPointAsync: context:UserContext * distributionPointId:DistributionPointId * name:NonEmptyName * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member PurgeDuplicatedVectorsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    