namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Author001 = {
    AuthorId: AuthorId
    Name: Name
    Isni: Isni
    Sealed: Sealed
    Books: List<BookId>
}
with member this.Upcast (): Author = 
            {
                AuthorId = this.AuthorId
                Name = this.Name
                Isni = this.Isni
                ImageUri = None
                Sealed = this.Sealed
                Books = this.Books
            }

and Author = {
    AuthorId: AuthorId
    Name: Name
    Isni: Isni
    ImageUri: Option<Uri>
    Sealed: Sealed
    Books: List<BookId>
} with 
    static member New (name: Name) (isni: Isni) = 
        {   
            AuthorId = AuthorId.New(); 
            Name = name;
            Isni = isni;
            ImageUri = None;
            Sealed = Sealed.New(DateTime.UtcNow)
            Books = []
        }
    static member NewWithoutIsni (name: Name) = 
        {   
            AuthorId = AuthorId.New(); 
            Name = name;
            Isni = Isni.EmptyIsni
            ImageUri = None;
            Sealed = Sealed.New(DateTime.UtcNow)
            Books = []
        }
    static member NewWithOptionalIsniAndImageUrl(name: Name, ?isni: Isni, ?imageUrl: Uri) = 
        {   
            AuthorId = AuthorId.New(); 
            Name = name;
            Isni = isni |> Option.defaultValue Isni.EmptyIsni
            ImageUri = imageUrl
            Sealed = Sealed.New(DateTime.UtcNow)
            Books = []
        }

    member this.Rename (name: Name) (dateTime: DateTime)= 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with Name = name }
            }

    member this.UpdateImageUrl (imageUrl: Uri) (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with ImageUri = imageUrl |> Some } 
            }

    member this.RemoveImageUrl (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Author is sealed"
                return { this with ImageUri = None } 
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
    member this.Editable =
        not (this.Sealed.IsSealed(DateTime.UtcNow))

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
            | ex -> 
                let author001 = JsonSerializer.Deserialize<Author001> (data, jsonOptions)
                Ok (author001.Upcast ())