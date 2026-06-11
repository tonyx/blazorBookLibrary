
namespace BookLibrary.CleanServices
open System.Threading

open System.Threading.Tasks
open System.Runtime.InteropServices

open BookLibrary.Shared.Commons

open BookLibrary.MessagesScheduler

type IMailResenderService = 
    abstract member AddMailQueueItemAsync: mailQueueItem: MailQueueItem * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveMailQueueItemAsync: mailQueueItemId: MailQueueItemId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member CreateInitialMailQueueInstanceAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<unit>
    abstract member ReSendPendingItemsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
