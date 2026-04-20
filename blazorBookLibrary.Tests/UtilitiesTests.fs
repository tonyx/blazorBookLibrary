module UtilitiesTests

open Expecto
open BookLibrary.Utils
open BookLibrary.Shared.Commons
open System

[<Tests>]
let converterUtilsTests =
    testList "ConverterUtils" [
        testCase "parseIsbns handles comma separated values" <| fun _ ->
            let input = "978-3-16-148410-0, 0-19-852663-6"

            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 2 "Should have 2 ISBNs"
            Expect.contains result (Isbn "9783161484100") "Should contain first ISBN"
            Expect.contains result (Isbn "0198526636") "Should contain second ISBN"

        testCase "parseIsbns handles newline separated values" <| fun _ ->
            let input = "978-3-16-148410-0\n0-19-852663-6"
            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 2 "Should have 2 ISBNs"

        testCase "parseIsbns removes duplicates" <| fun _ ->
            let input = "978-3-16-148410-0, 9783161484100, 978-3-16-148410-0 "
            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 1 "Should have only 1 unique ISBN"
            Expect.equal result.Head (Isbn "9783161484100") "Should be the cleaned ISBN"

        testCase "parseIsbns discards invalid ISBNs" <| fun _ ->
            let input = "978-3-16-148410-0, invalid-isbn, 12345"
            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 1 "Should only have the valid ISBN"
            Expect.equal result.Head (Isbn "9783161484100") "Should be the valid ISBN"

        testCase "parseIsbns handles spurious characters like spaces and hyphens" <| fun _ ->
            let input = " 978 - 3 - 16 - 148410 - 0 , 0 1 9 8 5 2 6 6 3 6 "
            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 2 "Should handle spaces and hyphens within ISBNs"
            Expect.contains result (Isbn "9783161484100") "Should contain first cleaned ISBN"
            Expect.contains result (Isbn "0198526636") "Should contain second cleaned ISBN"

        testCase "parseIsbns handles empty input" <| fun _ ->
            let result = ConverterUtils.parseIsbns ""
            Expect.isEmpty result "Should be empty"
            
            let resultNull = ConverterUtils.parseIsbns null
            Expect.isEmpty resultNull "Should be empty for null"

        testCase "parseIsbns handles mixed delimiters" <| fun _ ->
            let input = "978-3-16-148410-0\n0-19-852663-6, 978-0-306-40615-7"
            let result = ConverterUtils.parseIsbns input
            Expect.equal result.Length 3 "Should handle mixed newline and comma"
    ]
