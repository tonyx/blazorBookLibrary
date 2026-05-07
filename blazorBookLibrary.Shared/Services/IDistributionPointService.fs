
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type IDistributionPointService = 
    abstract member CreateDistributionPointAsync: context:UserContext * distributionPoint:DistributionPoint * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetDistributionPointAsync: context:UserContext * id:DistributionPointId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<DistributionPoint,string>>
    abstract member GetAllDistributionPointsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<DistributionPoint>, string>>
    abstract member FindDistributionPointsAsync: context:UserContext * name:Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<DistributionPoint>, string>>