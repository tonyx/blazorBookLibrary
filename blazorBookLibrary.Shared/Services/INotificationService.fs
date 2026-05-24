namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type INotificationService =
    abstract member GetUnreadNotificationsForUserAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Notification>, string>>
    abstract member GetAllNotificationsForUserAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Notification>, string>>
    abstract member MarkAsReadAsync: context: UserContext * notificationId: NotificationId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member CreateNotificationAsync: context: UserContext * notification: Notification * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
