

namespace BookLibrary.Services
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
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Details.Details
open System.Globalization
open blazorBookLibrary.Shared.Infrastructure.Services

type MailBodyRetriever() =

    interface IMailBodyRetriever with
        member this.GetLoanNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) =
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "LoanNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }

        member this.GetReleaseLoanNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) = 
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "LoanReturnNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }

        member this.GetReservationNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) = 
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "ReservationNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }
