
namespace BookLibrary.CleanServices
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open Sharpino.StateView

open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open System.Runtime.InteropServices

open BookLibrary.Shared.Details
open BookLibrary.Details.Details

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open Microsoft.Extensions.Configuration

open BookLibrary.MessagesScheduler

type IMailResenderService = 
    abstract member AddMailQueueItemAsync: mailQueueItem: MailQueueItem * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveMailQueueItemAsync: mailQueueItemId: MailQueueItemId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member CreateInitialMailQueueInstanceAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<unit>
    abstract member ReSendPendingItemsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
