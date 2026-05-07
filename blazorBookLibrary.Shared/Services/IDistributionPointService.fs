
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type IDistributionPointService = 
    abstract member CreateDistributionPointAsync: distributionPoint:DistributionPoint * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetDistributionPointAsync: id:DistributionPointId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<DistributionPoint,string>>
    abstract member GetAllDistributionPointsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<DistributionPoint>, string>>
    abstract member FindDistributionPointsAsync: name:Name * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<DistributionPoint>, string>>