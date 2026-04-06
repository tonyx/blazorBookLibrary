
module RandomGenerationTests

open System
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open System.Threading
open BookLibrary.Details.Details
open BookLibrary.Shared.Services
open BookLibrary.Tests.TestSeedExtensions.AuthorSeeds
open BookLibrary.Tests.TestSeedExtensions.BookSeeds

[<Tests>]
let tests =
    testList "generate random authors and books" [
        testCase "generate random author and book" <| fun _ ->
            setUp ()
            let author = generateRandomAuthor ()
            let book = randomBook ([author.AuthorId])
            printfn "%A\n" book
            Expect.isOk book "true"

        testCase "generate random isbn" <| fun _ ->
            let isbn = randomIsbn ()
            Expect.isOk isbn "should be ok"
    ]