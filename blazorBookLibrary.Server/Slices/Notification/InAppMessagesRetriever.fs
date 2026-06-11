namespace BookLibrary.Services

open System.Threading
open System
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.Globalization

type InAppMessagesRetriever() =

    interface IInAppMessagesRetriever with
        member this.GetLoanNotificationInAppAsync
            (
                bookTitle: Title,
                tenantName: TenantName,
                distributionPoint: string,
                loanDate: DateTime,
                dueDate: DateTime,
                shortLang: ShortLang,
                ?ct: CancellationToken
            ) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "InAppTemplates",
                    shortLang.Value,
                    "LoanNotification.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)

                let contentReplaced =
                    content
                        .Replace("{bookTitle}", bookTitle.Value)
                        .Replace("{tenantName}", tenantName.Value)
                        .Replace("{distributionPoint}", distributionPoint)
                        .Replace("{loanedAt}", loanDate.ToString("yyyy-MM-dd"))
                        .Replace("{dueDate}", dueDate.ToString("yyyy-MM-dd"))

                return contentReplaced
            }

        member this.GetReleaseLoanNotificationInAppAsync
            (
                userName: string,
                bookTitle: Title,
                loanedAt,
                returnedAt,
                tenantName: TenantName,
                dpName: NonEmptyName,
                shortLang,
                ?ct: CancellationToken
            ) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "InAppTemplates",
                    shortLang.Value,
                    "LoanReturnNotification.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)

                let contentReplaced =
                    content
                        .Replace("{UserName}", userName)
                        .Replace("{BookTitle}", bookTitle.Value)
                        .Replace("{LoanedAt}", loanedAt.ToString("yyyy-MM-dd"))
                        .Replace("{ReturnedAt}", returnedAt.ToString("yyyy-MM-dd"))
                        .Replace("{TenantName}", tenantName.Value)
                        .Replace("{DistributionPointName}", dpName.Value)

                return contentReplaced
            }

        member this.GetReservationNotificationInAppAsync
            (
                bookTitle: Title,
                code: ReservationCode,
                tenantName: TenantName,
                distributionPoint: string,
                shortLang,
                ?ct: CancellationToken
            ) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "InAppTemplates",
                    shortLang.Value,
                    "ReservationNotification.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)

                let replacedContent =
                    content
                        .Replace("{bookTitle}", bookTitle.Value)
                        .Replace("{code}", code.Value)
                        .Replace("{tenantName}", tenantName.Value)
                        .Replace("{distributionPoint}", distributionPoint)

                return replacedContent
            }

        member this.GetPatronInvitationInAppAsync
            (shortLang: ShortLang, ct: CancellationToken option)
            : Tasks.Task<Result<string, string>> =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "InAppTemplates",
                    shortLang.Value,
                    "PatronInvitationBody.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)
                return content
            }
