namespace BookLibrary.Hubs

open System
open Microsoft.AspNetCore.SignalR
open BookLibrary.Shared.Services
open BookLibrary.Services

type LibraryHub
    (
        warmupService: ITenantCacheWarmupService,
        userTenantResolverService: IUserTenantResolverService
    ) =
    inherit Hub()

    override this.OnConnectedAsync() =
        let user = this.Context.User
        let context = UserContextMapper.mapFromClaimsPrincipal user
        let baseTask = base.OnConnectedAsync()
        task {
            try
                let! tenantIdResult = userTenantResolverService.GetTenantForUserAsync(context)
                match tenantIdResult with
                | Ok tenantId ->
                    let _ = System.Threading.Tasks.Task.Run(fun () -> warmupService.WarmupTenantAsync(tenantId, System.Threading.CancellationToken.None))
                    ()
                | Error _ -> ()
            with ex ->
                System.Console.WriteLine($"[Hub Warmup Error] {ex.Message}")
            
            return! baseTask
        }



