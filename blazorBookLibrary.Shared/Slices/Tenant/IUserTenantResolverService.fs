
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Domain

type IUserTenantResolverService = 
    abstract member GetTenantForUserAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<TenantId, string>>
    abstract member ResolveSearchTenantsAsync: context: UserContext * scope: TenantScope * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Tenant>, string>>