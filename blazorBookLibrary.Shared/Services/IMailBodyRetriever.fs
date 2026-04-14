
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IMailBodyRetriever = 
    abstract member GetLoanNotificationTextMailAsync: shortLang:ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<string, string>>
    abstract member GetReleaseLoanNotificationTextMailAsync: shortLang:ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<string, string>>
    abstract member GetReservationNotificationTextMailAsync: shortLang:ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<string, string>>
