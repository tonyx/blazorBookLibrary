
namespace BookLibrary.Domain
open System.Text.Json
open BookLibrary.Shared.Commons
open System

type BookTitleAndId =
    {
        Title: Title
        BookId: BookId
    }

type IsbnRegistry = 
    {   IsbnRegistryId: IsbnRegistryId
        Isbns: Map<Isbn, List<BookTitleAndId>>    
    } with 
    static member UniqueId = IsbnRegistryId (Guid.Parse("3c2a1c19-3999-4a22-a1ef-67cfe4eab2e8"))
    static member New () = 
        {   
            IsbnRegistryId = IsbnRegistry.UniqueId; 
            Isbns = Map<Isbn, List<BookTitleAndId>>[]
        }
    member this.AddIsbn (isbn: Isbn) (bookTitleAndId: BookTitleAndId) = 
        let newIsbns = 
            match this.Isbns.Keys.Contains isbn with
            | true -> 
                let existingValue = this.Isbns.[isbn]
                let newValue = existingValue |> List.append [bookTitleAndId]
                this.Isbns |> Map.remove isbn |> Map.add isbn newValue
            | false -> 
                this.Isbns |> Map.add isbn [bookTitleAndId]
        Ok { this with Isbns = newIsbns }
    member this.RemoveIsbn (isbn: Isbn) = 
        Ok ()

    member this.GetAllIsbn() =
        this.Isbns |> Map.keys

    member this.GetAllBookTitlesAndId () =
        this.Isbns 
        |> Map.values 
        |> List.ofSeq 
        |> List.concat 

    member this.GetAllBookTitles () =
        this.GetAllBookTitlesAndId () 
        |> List.map (fun bookTitleAndId -> bookTitleAndId.Title)

    member this.GetBooksTitlesAndIdsByIsdn (isbn: Isbn) =
        match this.Isbns.Keys.Contains isbn with
        | true -> 
            Ok this.Isbns.[isbn]

        | false -> 
            Error $"Isbn {isbn} not found"

    member this.Id = this.IsbnRegistryId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_IsbnRegistry"
    static member Version = "_01"
    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
    static member Deserialize (data: string) =
        try
            let isbnRegistry = JsonSerializer.Deserialize<IsbnRegistry> (data, jsonOptions)
            Ok isbnRegistry
        with
            | ex -> 
                Error ex