
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

type RandomAuthorGeneratorService (
    authorService: IAuthorService,
    configuration: IConfiguration,
    logger: ILogger<RandomAuthorGeneratorService>
) = 
    member this.SeedRandomAuthorsAccordingToThreshold () = 
        taskResult
            {
                let! existingAuthors = authorService.GetAllAsync()
                if (existingAuthors.Length > 10) then
                    return ()
                else
                    let numberOfAuthorsToCreate = configuration.GetValue<int>("TestDataSeedSetup:NumberOfRandomAuthors", 0)
                    let newAuthors = 
                        [ 1 .. numberOfAuthorsToCreate ]
                        |> List.map (fun _ -> generateRandomAuthor())

                    let result =
                        authorService.AddAuthorsAsync newAuthors
                    return! result
            }
