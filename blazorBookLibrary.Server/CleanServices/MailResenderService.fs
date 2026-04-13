
namespace BookLibrary.CleanServices
open System.Threading
open Sharpino
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open Mailjet.Client

open FsToolkit.ErrorHandling
open System.Threading.Tasks

open BookLibrary.Shared.Commons
open BookLibrary.MessagesScheduler
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration

type MailResenderService(
    configuration: IConfiguration,
    eventStore: IEventStore<string>,
    mailQueueViewerAsync: AggregateViewerAsync2<MailQueue>,
    mailJetCient: MailjetClient,
    logger: ILogger<MailResenderService>
) =
    new (configuration: IConfiguration, logger: ILogger<MailResenderService>) =
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        let mailQueueViewerAsync = getAggregateStorageFreshStateViewerAsync<MailQueue, MailQueueEvent, string> eventStore
        let mailjetApiKey = configuration["Mailjet:ApiKey"]
        let mailjetSecretKey = configuration["Mailjet:SecretKey"]
        let mailJetCient = MailjetClient(mailjetApiKey, mailjetSecretKey)
        
        MailResenderService(configuration, eventStore, mailQueueViewerAsync, mailJetCient, logger)

    member this.GetAllPendingItemsAsync (?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! mailQueue = 
                    mailQueueViewerAsync (ct |> Some) MailQueue.UniqueInstanceId.Value 
                let mailQueue = mailQueue |> snd
                let pendingItems = 
                    mailQueue.MailQueueItems
                return pendingItems
            }
    member this.IncrementRetryCountAsync (mailQueueItemId: MailQueueItemId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let command = MailQueueCommand.IncrementRetryCount mailQueueItemId
                let! result = 
                    runNAggregateCommandsMdAsync<MailQueue, MailQueueEvent, string> 
                        [MailQueue.UniqueInstanceId.Value]
                        eventStore
                        MessageSenders.NoSender
                        ""
                        [command]
                        (ct |> Some)
                return result
            }

    interface IMailResenderService with
        member this.AddMailQueueItemAsync (mailQueueItem: MailQueueItem, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let command = MailQueueCommand.AddMailQueueItem mailQueueItem
                    let! result = 
                        runNAggregateCommandsMdAsync<MailQueue, MailQueueEvent, string> 
                            [MailQueue.UniqueInstanceId.Value]
                            eventStore
                            MessageSenders.NoSender
                            ""
                            [command]
                            (ct |> Some)
                    return result
                }

        member this.RemoveMailQueueItemAsync (mailQueueItemId: MailQueueItemId, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let command = MailQueueCommand.RemoveMailQueueItem mailQueueItemId
                    let! result = 
                        runNAggregateCommandsMdAsync<MailQueue, MailQueueEvent, string> 
                            [MailQueue.UniqueInstanceId.Value]
                            eventStore
                            MessageSenders.NoSender
                            ""
                            [command]
                            (ct |> Some)
                    return result
                }

        member this.CreateInitialMailQueueInstanceAsync (?ct: CancellationToken) =
            task
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! existing =
                        mailQueueViewerAsync (ct |> Some) MailQueue.UniqueInstanceId.Value

                    if existing.IsOk then
                        return ()
                    else    
                        let instance = MailQueue.New()
                        let! result =  
                            runInitAsync<MailQueue, MailQueueEvent, string> 
                                eventStore
                                MessageSenders.NoSender
                                instance
                                (ct |> Some)
                        match result with
                        | Ok _ -> return ()
                        | Error _ -> failwith "Failed to create initial mail queue instance"
                }
        member this.ReSendPendingItemsAsync (?ct: CancellationToken) = 
            let maxEmailSendingRetries = configuration.GetSection("BooksLibrary").GetValue<int>("MaxEmailSendingRetries", 5)
            let ct = defaultArg ct CancellationToken.None
            taskResult
                {
                    let! pendingItems = this.GetAllPendingItemsAsync ct

                    let resend = 
                        pendingItems
                        |> List.map 
                            (fun item -> 
                                let mail = item.TransactionalEmail
                                if (item.RetryCount < maxEmailSendingRetries) then
                                    try
                                        mailJetCient.SendTransactionalEmailAsync mail |> ignore
                                        (this:> IMailResenderService).RemoveMailQueueItemAsync(item.MailQueueItemId, ct)
                                    with
                                        | ex -> 
                                            logger.LogError(ex, "Failed to send email")
                                            (this.IncrementRetryCountAsync(item.MailQueueItemId, ct))
                                else
                                    logger.LogWarning("Mail queue item {MailQueueItemId} has been retried {RetryCount} times", item.MailQueueItemId, item.RetryCount)
                                    Task.FromResult (Ok ())
                            )
                    return ()
                }
            
