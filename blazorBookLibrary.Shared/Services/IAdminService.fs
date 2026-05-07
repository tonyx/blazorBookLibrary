
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IAdminServices = 
    abstract member PurgeVectorsReferringDroppedBooksAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AdjustBookStatesReferringMissingEmbeddingsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member AssignUserToDistributionPointAsync: distributionPointId:DistributionPointId * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UnassignUserFromDistributionPointAsync: distributionPointId:DistributionPointId * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateDistributionPointInfoAsync: distributionPointId:DistributionPointId * info:Info * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RenameDistributionPointAsync: distributionPointId:DistributionPointId * name:NonEmptyName * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    