
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type ITenantService = 
    abstract member EnsureDefaultTenantExistsAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    // abstract member CreateTenantAsync: UserContext * Tenant * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<TenantId, string>>