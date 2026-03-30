
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization



type Book001 = {
    BookId: BookId
    Title: Title
    Authors: List<AuthorId>
    Translators: List<AuthorId>
    Languages: List<CultureInfo>
    CurrentReservations: List<ReservationId>
    CurrentLoan: Option<LoanId>
    Editor: Option<EditorId>
    MainCategory: Category
    AdditionalCategories: List<Category>
    Year: Year
    Isbn: Isbn
    Sealed: Sealed
}
with
    member 
        this.Upcast (): Book =
        {
            BookId = this.BookId;
            Title = this.Title;
            ImageUrl = None;
            Authors = this.Authors;
            Translators = this.Translators;
            Languages = this.Languages;
            CurrentReservations = this.CurrentReservations;
            CurrentLoan = this.CurrentLoan;
            Editor = this.Editor;
            MainCategory = this.MainCategory;
            AdditionalCategories = this.AdditionalCategories;
            Year = this.Year;
            Isbn = this.Isbn;
            Sealed = this.Sealed
        }


and Book = {
    BookId: BookId
    Title: Title
    ImageUrl: Option<Uri>   
    Authors: List<AuthorId>
    Translators: List<AuthorId>
    Languages: List<CultureInfo>
    CurrentReservations: List<ReservationId>
    CurrentLoan: Option<LoanId>
    Editor: Option<EditorId>
    MainCategory: Category
    AdditionalCategories: List<Category>
    Year: Year
    Isbn: Isbn
    Sealed: Sealed
}
with 
    static member New 
        (title: Title) 
        (authors: list<AuthorId>) 
        (translators: list<AuthorId>) 
        (languages: list<CultureInfo>) 
        (editor: Option<EditorId>) 
        (year: Year) 
        (isbn: Isbn) 
        = 
        {
            BookId = BookId.New(); 
            Title = title; 
            ImageUrl = None;
            Authors = authors; 
            Translators = translators;
            Languages = languages;
            CurrentReservations = [];
            CurrentLoan = None;
            Editor = editor; 
            MainCategory = Category.Other;
            AdditionalCategories = [];
            Year = year; 
            Isbn = isbn
            Sealed = Sealed.New(DateTime.UtcNow)
        }
    static member NewWithMainCategory  
        (title: Title) 
        (authors: list<AuthorId>) 
        (translators: list<AuthorId>) 
        (languages: list<CultureInfo>) 
        (editor: Option<EditorId>) 
        (mainCategory: Category) 
        (year: Year) 
        (isbn: Isbn) = 
        {   
            BookId = BookId.New(); 
            Title = title; 
            ImageUrl = None;
            Authors = authors; 
            Translators = translators;
            Languages = languages;
            CurrentReservations = [];
            CurrentLoan = None;
            Editor = editor; 
            MainCategory = mainCategory;
            AdditionalCategories = [];
            Year = year; 
            Isbn = isbn
            Sealed = Sealed.New(DateTime.UtcNow)
        }

    static member NewWithMainCategoryAndAdditionalCategories
        (title: Title) 
        (authors: list<AuthorId>) 
        (translators: list<AuthorId>) 
        (languages: list<CultureInfo>) 
        (editor: Option<EditorId>) 
        (mainCategory: Category) 
        (additionalCategories: list<Category>) 
        (year: Year) 
        (isbn: Isbn) 
        (imageUrl: Option<Uri>)
        = 
        {   
            BookId = BookId.New(); 
            Title = title; 
            ImageUrl = imageUrl;
            Authors = authors; 
            Translators = translators;
            Languages = languages;
            CurrentReservations = [];
            CurrentLoan = None;
            Editor = editor; 
            MainCategory = mainCategory;
            AdditionalCategories = additionalCategories;
            Year = year; 
            Isbn = isbn
            Sealed = Sealed.New(DateTime.UtcNow)
        } 

    member this.UpdateTitle 
        (title: Title) 
        (dateTime: DateTime)=
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Title = title } 
            }
    member this.UpdateAuthors 
        (authors: List<AuthorId>) 
        (dateTime: DateTime) = 
        result
            { 
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Authors = authors } 
            }
    member this.AddAuthor 
        (author: AuthorId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Authors
                    |> List.contains author
                    |> not
                    |> Result.ofBool "Author already in book"
                return { this with Authors = this.Authors @ [author] } 
            }
    member this.AddTranslator 
        (translator: AuthorId) 
        (dateTime: DateTime) = 
        result
            {   
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Translators
                    |> List.contains translator
                    |> not
                    |> Result.ofBool "Translator already in book"
                return { this with Translators = this.Translators @ [translator] } 
            }
    member this.RemoveTranslator 
        (translator: AuthorId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Translators
                    |> List.contains translator
                    |> Result.ofBool "Translator not in book"
                return { this with Translators = this.Translators |> List.filter (fun x -> x <> translator) } 
            }
    member this.AddLanguage 
        (language: CultureInfo) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Languages
                    |> List.contains language
                    |> not
                    |> Result.ofBool "Language already in book"
                return { this with Languages = this.Languages @ [language] } 
            }
    member this.RemoveLanguage 
        (language: CultureInfo) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Languages
                    |> List.contains language
                    |> Result.ofBool "Language not in book"
                return { this with Languages = this.Languages |> List.filter (fun x -> x <> language) } 
            }

    member this.RemoveAuthor 
        (author: AuthorId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.Authors
                    |> List.contains author
                    |> Result.ofBool "Author not in book"
                return { this with Authors = this.Authors |> List.filter (fun x -> x <> author) } 
            }

    member this.SetImageUrl 
        (imageUrl: Uri) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with ImageUrl = Some imageUrl } 
            }
    member this.RemoveImageUrl 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with ImageUrl = None } 
            }

    member this.SetCurrentLoan 
        (loanId: LoanId) 
        (dateTime: DateTime) = 
        result
            {
                do!
                    this.CurrentLoan
                    |> Option.isSome
                    |> not
                    |> Result.ofBool "Book is already on loan"
                return { this with CurrentLoan = Some loanId } 
            }
    member this.ReleaseLoan (loanId: LoanId) (dateTime: DateTime) = 
        result
            {
                let! currentLoan =
                    this.CurrentLoan
                    |> Result.ofOption "Book is not on loan"
                do!
                    currentLoan = loanId
                    |> Result.ofBool "Book is not on the specified loan"
                return { this with CurrentLoan = None } 
            }

    member this.ReturnFromLoan (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.CurrentLoan
                    |> Option.isSome
                    |> Result.ofBool "Book is not on loan"
                return { this with CurrentLoan = None } 
            }
    member this.AddReservation 
        (reservationId: ReservationId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.CurrentReservations
                    |> List.contains reservationId
                    |> not
                    |> Result.ofBool "Reservation already in book"
                return { this with CurrentReservations = this.CurrentReservations @ [reservationId] } 
            }
    member this.RemoveReservation 
        (reservationId: ReservationId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do!
                    this.CurrentReservations
                    |> List.contains reservationId
                    |> Result.ofBool "Reservation not in book"
                return { this with CurrentReservations = this.CurrentReservations |> List.filter (fun x -> x <> reservationId) } 
            }
    member this.UpdateEditor 
        (editor: EditorId) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Editor = Some editor } 
            }
    member this.ChangeMainCategory 
        (mainCategory: Category) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do! 
                    this.AdditionalCategories
                    |> List.contains mainCategory
                    |> not
                    |> Result.ofBool "Main category already in additional categories"
                return { this with MainCategory = mainCategory } 
            }
    member this.AddAdditionalCategory 
        (category: Category) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do! 
                    this.AdditionalCategories
                    |> List.contains category
                    |> not
                    |> Result.ofBool "Category already in additional categories"
                do! 
                    this.MainCategory
                    |> fun c -> c <> category
                    |> Result.ofBool "Category already in additional categories"
                return { this with AdditionalCategories = this.AdditionalCategories @ [category] } 
            }
    member this.RemoveAdditionalCategory 
        (category: Category) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                do! 
                    this.AdditionalCategories
                    |> List.contains category
                    |> Result.ofBool "Category not in additional categories"
                return { this with AdditionalCategories = this.AdditionalCategories |> List.filter (fun x -> x <> category) } 
            }
    member this.RemoveEditor (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Editor = None } 
            }
    member this.UpdateYear 
        (year: Year) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Year = year } 
            }
    member this.UpdateIsbn 
        (isbn: Isbn) 
        (dateTime: DateTime) = 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Book is sealed"
                return { this with Isbn = isbn } 
            }

    member this.Seal(dateTime: DateTime) =
        { 
            this with 
                Sealed = this.Sealed.Seal(dateTime) 
        } 
        |> Ok

    member this.Unseal(dateTime: DateTime) =
        { 
            this 
                with 
                    Sealed = this.Sealed.Unseal(dateTime) 
        } 
        |> Ok
    member this.Editable =
        not (this.Sealed.IsSealed(DateTime.UtcNow)) &&
        this.NoLoan &&
        this.NoReservations

    member this.NoLoan = 
        this.CurrentLoan
        |> Option.isNone

    member this.NoReservations = 
        this.CurrentReservations
        |> List.isEmpty

    member this.Available = 
        this.CurrentLoan
        |> Option.isNone

    member this.ImmediatelyAvailable =
        this.Available &&
        this.CurrentReservations
        |> List.isEmpty

    member this.Id = this.BookId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Book"
    static member Version = "_01"
    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
    static member Deserialize (data: string) =
        try
            let book = JsonSerializer.Deserialize<Book> (data, jsonOptions)
            Ok book
        with
            | ex -> 
                try
                    let book001 = JsonSerializer.Deserialize<Book001> (data, jsonOptions)
                    Ok (book001.Upcast())
                with
                    | ex2 -> Error (ex.Message + " " + ex2.Message)
