namespace blazorBookLibrary.Shared.Infrastructure.Services

open System.Threading
open System.Threading.Tasks
open BookLibrary.Shared.Commons

type INotificationDispatcher =
    abstract member DispatchNotificationAsync : 
        context: UserContext * 
        recipientId: UserId * 
        tenantId: TenantId * 
        actionUrl: string option * 
        ?ct: CancellationToken -> Task<Result<unit, string>>
