namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System
open BookLibrary.Shared.Commons

type ITenantCacheWarmupService =
    abstract member WarmupTenantAsync: tenantId: TenantId * ct: CancellationToken -> Task

