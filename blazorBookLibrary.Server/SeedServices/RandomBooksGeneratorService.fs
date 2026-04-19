
namespace BookLibrary.Server.SeedServices
open System

open BookLibrary.Domain
open FsToolkit.ErrorHandling

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open BookLibrary.Shared.Services
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







                        
                        