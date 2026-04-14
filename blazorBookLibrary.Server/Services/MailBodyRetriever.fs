

namespace BookLibrary.Services
open System.Threading
open System
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.Globalization
open blazorBookLibrary.Shared.Resources

type MailBodyRetriever() =

    interface IMailBodyRetriever with
        member this.GetLoanNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) =
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "LoanNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }

        member this.GetLoanNotificationSubject (shortLang: ShortLang) = 
            let culture = CultureInfo(shortLang.Value)
            SharedResources.ResourceManager.GetString("LoanNotification", culture)
        
        member this.GetReleaseLoanNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) = 
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "LoanReturnNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }

        member this.GetReleaseLoanNotificationSubject (shortLang: ShortLang) = 
            let culture = CultureInfo(shortLang.Value)
            SharedResources.ResourceManager.GetString("LoanReturnNotification", culture)
        member this.GetReservationNotificationTextMailAsync (shortLang: ShortLang, ?ct:CancellationToken) = 
            let templatePath = System.IO.Path.Combine (AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "ReservationNotification.txt")
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! content = System.IO.File.ReadAllTextAsync (templatePath, ct)
                return content
            }

        member this.GetReservationNotificationSubject (shortLang: ShortLang) = 
            let culture = CultureInfo(shortLang.Value)
            SharedResources.ResourceManager.GetString("BookReservationConfirmation", culture)


