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

    member this.JoinTenantGroup(tenantId: Guid) =
        this.Groups.AddToGroupAsync(this.Context.ConnectionId, $"Tenant_{tenantId}") :> System.Threading.Tasks.Task

    member this.LeaveTenantGroup(tenantId: Guid) =
        this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, $"Tenant_{tenantId}") :> System.Threading.Tasks.Task

    member this.JoinBookGroup(bookId: Guid) =
        this.Groups.AddToGroupAsync(this.Context.ConnectionId, $"Book_{bookId}") :> System.Threading.Tasks.Task

    member this.LeaveBookGroup(bookId: Guid) =
        this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, $"Book_{bookId}") :> System.Threading.Tasks.Task

    member this.JoinUserGroup(userId: Guid) =
        this.Groups.AddToGroupAsync(this.Context.ConnectionId, $"User_{userId}") :> System.Threading.Tasks.Task

    member this.LeaveUserGroup(userId: Guid) =
        this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, $"User_{userId}") :> System.Threading.Tasks.Task



