module TextEmbeddingTests

open Expecto
open TestSetup
open BookLibrary.Shared.Services
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System.Threading.Tasks

[<Tests>]
let tests =
    testList "text embedding service tests" [
        testCaseTask "get embedding for a simple text - Ok" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let text = "The Constitution of the United States is the supreme law of the United States of America."
            let! result = textEmbeddingService.GetEmbeddingAsync text
            
            match result with
            | Ok embedding ->
                printfn "%A" embedding.Vector
                Expect.isNotNull embedding.Vector "vector should not be null"
                Expect.isTrue (embedding.Vector.Length > 0) "vector should have values"
                Expect.equal embedding.Model "models/gemini-embedding-2" "model name should match"
            | Error e ->
                failwithf "Should be ok but failed with: %s" e
        }

        testCaseTask "embed, store and read back - Ok" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let vectorDbService = getVectorDbService()
            let text = "Deep Learning is a subset of machine learning."
            
            let! embeddingResult = textEmbeddingService.GetEmbeddingAsync text
            match embeddingResult with
            | Error e -> failwithf "embedding failed: %s" e
            | Ok embedding ->
                let id = EmbeddingDataId.New()
                let bookId = BookId.New()
                let! storeResult = vectorDbService.StoreEmbeddingAsync(id, bookId, embedding)
                Expect.isOk storeResult "storing should be ok"
                
                let! readResult = vectorDbService.ReadEmbeddingAsync id
                match readResult with
                | Error e -> failwithf "reading failed: %s" e
                | Ok (readEmbedding, readBookId) ->
                    Expect.equal readBookId bookId "book id should match"
                    Expect.equal readEmbedding.Model embedding.Model "model should match"
                    Expect.equal readEmbedding.Vector.Length embedding.Vector.Length "vector length should match"
                    readEmbedding.Vector
                    |> Array.iteri (fun i v ->
                        Expect.floatClose Accuracy.high (float v) (float embedding.Vector.[i]) (sprintf "vector element at %d should match" i)
                    )
        }
        testCaseTask "update embedding - Ok" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let vectorDbService = getVectorDbService()
            let text1 = "Machine Learning"
            let text2 = "Deep Learning"
            
            let! embedding1Result = textEmbeddingService.GetEmbeddingAsync text1
            match embedding1Result with
            | Error e -> failwithf "embedding 1 failed: %s" e
            | Ok embedding1 ->
                let id = EmbeddingDataId.New()
                let bookId = BookId.New()
                let! _ = vectorDbService.StoreEmbeddingAsync(id, bookId, embedding1)
                
                let! embedding2Result = textEmbeddingService.GetEmbeddingAsync text2
                match embedding2Result with
                | Error e -> failwithf "embedding 2 failed: %s" e
                | Ok embedding2 ->
                    let! updateResult = vectorDbService.UpdateEmbeddingAsync(id, embedding2)
                    Expect.isOk updateResult "update should be ok"
                    
                    let! readResult = vectorDbService.ReadEmbeddingAsync id
                    match readResult with
                    | Error e -> failwithf "reading failed: %s" e
                    | Ok (readEmbedding, _) ->
                        Expect.equal readEmbedding.Model embedding2.Model "model should be updated"
                        Expect.floatClose Accuracy.high (float readEmbedding.Vector.[0]) (float embedding2.Vector.[0]) "vector should be updated"
        }

        testCaseTask "delete embedding - Ok" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let vectorDbService = getVectorDbService()
            let text = "To be deleted"
            
            let! embeddingResult = textEmbeddingService.GetEmbeddingAsync text
            match embeddingResult with
            | Error e -> failwithf "embedding failed: %s" e
            | Ok embedding ->
                let id = EmbeddingDataId.New()
                let bookId = BookId.New()
                let! _ = vectorDbService.StoreEmbeddingAsync(id, bookId, embedding)
                
                let! deleteResult = vectorDbService.RemoveEmbeddingAsync id
                Expect.isOk deleteResult "delete should be ok"
                
                let! readResult = vectorDbService.ReadEmbeddingAsync id
                Expect.isError readResult "reading should fail after delete"
        }

        testCaseTask "calculate similarity with itself - high value" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let text = "Artificial Intelligence is transforming the world."
            
            let! embeddingResult = textEmbeddingService.GetEmbeddingAsync text
            match embeddingResult with
            | Error e -> failwithf "embedding failed: %s" e
            | Ok embedding ->
                let similarityResult = embedding.Similarity embedding
                match similarityResult with
                | Error e -> failwithf "similarity calculation failed: %s" e
                | Ok similarity ->
                    Expect.floatClose Accuracy.high (float similarity) 1.0 "similarity with itself should be close to 1"
        }

        testCaseTask "calculate distance with itself - low value" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let text = "Artificial Intelligence is transforming the world."
            
            let! embeddingResult = textEmbeddingService.GetEmbeddingAsync text
            match embeddingResult with
            | Error e -> failwithf "embedding failed: %s" e
            | Ok embedding ->
                let distanceResult = embedding.Distance embedding
                match distanceResult with
                | Error e -> failwithf "distance calculation failed: %s" e
                | Ok distance ->
                    Expect.floatClose Accuracy.high (float distance) 0.0 "distance with itself should be close to 0"
        }

        testCaseTask "semantic search with vector - find related items" <| fun _ -> task {
            let textEmbeddingService = getTextEmbeddingService()
            let vectorDbService = getVectorDbService()
            
            let textA = "The theory of relativity is a pillar of modern physics."
            let textB = "Einstein's equations changed our understanding of space and time."
            let textC = "I enjoy walking my dog in the park every morning."
            let textQuery = "Physics and gravitational theories." 

            let! results = 
                [| textA; textB; textC; textQuery |]
                |> Array.map textEmbeddingService.GetEmbeddingAsync
                |> Array.toList
                |> Task.WhenAll
            
            let embeddings = 
                results 
                |> Array.map (function Ok e -> e | Error e -> failwithf "embedding failed: %s" e)
            
            let idA = EmbeddingDataId.New()
            let idB = EmbeddingDataId.New()
            let idC = EmbeddingDataId.New()
            let bookIdA = BookId.New()
            let bookIdB = BookId.New()
            let bookIdC = BookId.New()

            let! _ = vectorDbService.StoreEmbeddingAsync(idA, bookIdA, embeddings.[0])
            let! _ = vectorDbService.StoreEmbeddingAsync(idB, bookIdB, embeddings.[1])
            let! _ = vectorDbService.StoreEmbeddingAsync(idC, bookIdC, embeddings.[2])

            // Search for items similar to textQuery (embeddings.[3]) without storing it first
            let! searchResult = vectorDbService.SearchSimilarEmbeddingsAsync(embeddings.[3], 2)
            match searchResult with
            | Error e -> failwithf "search failed: %s" e
            | Ok similarItems ->
                let items = similarItems |> Seq.toList
                Expect.equal items.Length 2 "should find 2 similar items"
                
                let containsUnrelated = items |> List.exists (fun (item, _) -> item.Vector = embeddings.[2].Vector)
                Expect.isFalse containsUnrelated "unrelated item (C) should not be in the top 2 results"
                
                let containsA = items |> List.exists (fun (item, bid) -> item.Vector = embeddings.[0].Vector && bid = bookIdA)
                let containsB = items |> List.exists (fun (item, bid) -> item.Vector = embeddings.[1].Vector && bid = bookIdB)
                Expect.isTrue (containsA && containsB) "both physics statements (A and B) should be found with their correct book ids"
        }
    ]
    |> testSequenced
