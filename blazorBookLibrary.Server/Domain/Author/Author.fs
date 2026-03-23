
namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Commons
open System

type Author = {
    AuthorId: AuthorId
    Name: Name
    Isni: Isni
    Sealed: Sealed
    Books: List<BookId>
} with 
    static member New (name: Name) (isni: Isni) = 
        {   
            AuthorId = AuthorId.New(); 
            Name = name;
            Isni = isni;
            Sealed = Sealed.New(DateTime.UtcNow)
            Books = []
        }
    static member NewWithoutIsni (name: Name) = 
        {   
            AuthorId = AuthorId.New(); 
            Name = name;
            Isni = Isni.EmptyIsni
            Sealed = Sealed.New(DateTime.UtcNow)
            Books = []
        }

    member this.UpdateName (name: Name) (dateTime: DateTime)= 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with Name = name }
            }

    member this.UpdateIsni (isni: Isni) (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with Isni = isni } 
            }

    member this.AddBook (bookId: BookId) = 
        if this.Books |> List.contains bookId then
            Error "Book already added"
        else
            { 
                this with Books = bookId :: this.Books 
            } 
            |> Ok

    member this.RemoveBook (bookId: BookId) = 
        if this.Books 
            |> List.contains bookId
            |> not then
            Error "Book not found"
        else
            { 
                this with Books = this.Books |> List.filter (fun id -> id <> bookId) 
            } 
            |> Ok

    member this.Seal(dateTime: DateTime) =
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with Sealed = this.Sealed.Seal(dateTime) } 
            }

    member this.Unseal(dateTime: DateTime) =
        { 
            this 
                with 
                    Sealed = this.Sealed.Unseal(dateTime) 
        } 
        |> Ok

    member this.Id = this.AuthorId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Author"
    static member Version = "_01"
    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
    static member Deserialize (data: string) =
        try
            let author = JsonSerializer.Deserialize<Author> (data, jsonOptions)
            Ok author
        with
            | ex -> Error ex.Message