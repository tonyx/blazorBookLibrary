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
        member this.GetLoanNotificationTextMailAsync
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
                    "Templates",
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

        member this.GetLoanNotificationSubject(bookTitle: Title, dueDate: DateTime, shortLang, ?ct: CancellationToken) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "LoanNotificationSubject.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)

                let contentReplaced =
                    content.Replace("{BookTitle}", bookTitle.Value).Replace("{dueDate}", dueDate.ToString("yyyy-MM-dd"))

                return contentReplaced
            }

        member this.GetReleaseLoanNotificationTextMailAsync
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
                    "Templates",
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

        member this.GetReleaseLoanNotificationSubject(bookTitle: Title, shortLang: ShortLang, ?ct: CancellationToken) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "LoanReturnNotificationSubject.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)
                let contentReplaced = content.Replace("{bookTitle}", bookTitle.Value)
                return contentReplaced
            }

        member this.GetReservationNotificationTextMailAsync
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
                    "Templates",
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

        member this.GetReservationNotificationSubject(shortLang: ShortLang, ?ct: CancellationToken) =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "ReservationNotificationSubject.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)
                return content
            }

        member this.GetPatronInvitationSubject
            (tenantName: TenantName, userName: string, shortLang: ShortLang, ?ct: CancellationToken)
            =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "PatronInvitationSubject.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)

                let contentReplaced =
                    content.Replace("{tenantName}", tenantName.Value).Replace("{userName}", userName)

                return contentReplaced
            }

        member this.GetPatronInvitationTextMailAsync
            (shortLang: ShortLang, ct: CancellationToken option)
            : Tasks.Task<Result<string, string>> =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "PatronInvitationBody.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)
                return content
            }

        member this.GetAgreementTextMailAsync
            (shortLang: ShortLang, ct: CancellationToken option)
            : Tasks.Task<Result<string, string>> =
            let templatePath =
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Templates",
                    shortLang.Value,
                    "agreement.txt"
                )

            let ct = defaultArg ct CancellationToken.None

            taskResult {
                let! content = System.IO.File.ReadAllTextAsync(templatePath, ct)
                return content
            }
