
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IUserTenantResolverService = 
    abstract member GetTenantForUserAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<TenantId, string>>
    // abstract member SetTenantForUserAsync: context: UserContext * tenantId: TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<TenantId, string>>