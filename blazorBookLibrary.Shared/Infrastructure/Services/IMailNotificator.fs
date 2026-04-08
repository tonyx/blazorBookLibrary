namespace blazorBookLibrary.Shared.Infrastructure.Services
open System.Threading.Tasks

type IMailNotificator =
    abstract member SendEmailAsync : emailFrom: string * nameFrom: string * emailRecipient: string * subject: string * body: string -> Task
