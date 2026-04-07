
namespace BookLibrary.Server.SeedServices
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

open BookLibrary.Shared.Details
open BookLibrary.Details.Details

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Tests.TestSeedExtensions.AuthorSeeds
open Microsoft.Extensions.Logging
open BookLibrary.Tests.TestSeedExtensions
open BookLibrary.Tests.TestSeedExtensions.BookSeeds

type RandomBooksGeneratorService (
    bookService: IBookService,
    authorService: IAuthorService,
    configuration: IConfiguration,
    logger: ILogger<RandomBooksGeneratorService>
) = 
    member this.SeedRandomBooksAccordingToThreshold () = 
        let random = Random()
        taskResult
            {
                let! existingBooks = bookService.GetAllAsync()
                let! existingAuthors = authorService.GetAllAsync()
                if (existingBooks.Length >= 10) then
                    logger.LogInformation("Book threshold reached: {Count}", existingBooks.Length)
                    return ()
                else
                    let numberOfBooksToCreate = configuration.GetValue<int>("TestDataSeedSetup:NumberOfRandomBooks", 0)
                    let randomAuthor = existingAuthors |> List.item (random.Next(existingAuthors.Length))

                    let! randomBooks =
                        [ 1 .. numberOfBooksToCreate]
                        |> List.traverseTaskResultM (fun _ -> task { return randomBook [randomAuthor.AuthorId] })

                    logger.LogInformation("Generated {Count} random books", randomBooks.Length)
                    let! result = 
                        bookService.AddBooksAsync(randomBooks)

                    logger.LogInformation("Added {Count} random books", randomBooks.Length)

                    return ()
            }







                        
                        