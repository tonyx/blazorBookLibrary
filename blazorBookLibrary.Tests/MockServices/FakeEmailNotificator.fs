namespace blazorBookLibrary.Tests.MockServices
open System.Threading.Tasks

open blazorBookLibrary.Shared.Infrastructure.Services

type FakeEmailNotificator() = 
    interface IMailNotificator with
        member this.SendEmailAsync(emailFrom: string, nameFrom: string, emailRecipient: string, subject: string, body: string) =
            printfn "Email sent to %s with subject %s has been sent" emailRecipient subject
            Task.CompletedTask
            
