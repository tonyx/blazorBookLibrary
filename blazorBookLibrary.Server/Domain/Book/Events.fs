
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json
open System.Globalization

type BookEvent =
    | TitleUpdated of Title * DateTime
    | AuthorsUpdated of list<AuthorId> * DateTime
    | EditorUpdated of EditorId * DateTime
    | YearUpdated of Year * DateTime
    | IsbnUpdated of Isbn * DateTime
    | AuthorAdded of AuthorId * DateTime
    | AuthorRemoved of AuthorId * DateTime
    | BookSealed of DateTime
    | BookUnsealed of DateTime
    | LoanReleased of LoanId * DateTime
    | TranslatorAdded of AuthorId * DateTime
    | TranslatorRemoved of AuthorId * DateTime
    | LanguageAdded of CultureInfo * DateTime
    | LanguageRemoved of CultureInfo * DateTime
    | CurrentLoanSet of LoanId * DateTime
    | ReturnedFromLoan of DateTime
    | ReservationAdded of ReservationId * DateTime
    | ReservationRemoved of ReservationId * DateTime

    interface Event<BookLibrary.Domain.Book> with
        member this.Process (book: Book) : Result<Book, string> =
            match this with
            | TitleUpdated (title, dateTime) ->
                book.UpdateTitle title dateTime
            | AuthorsUpdated (authors, dateTime) ->
                book.UpdateAuthors authors dateTime
            | EditorUpdated (editor, dateTime) ->
                book.UpdateEditor editor dateTime
            | YearUpdated (year, dateTime) ->
                book.UpdateYear year dateTime
            | IsbnUpdated (isbn, dateTime) ->
                book.UpdateIsbn isbn dateTime
            | AuthorAdded (authorId, dateTime) ->
                book.AddAuthor authorId dateTime
            | AuthorRemoved (authorId, dateTime) ->
                book.RemoveAuthor authorId dateTime
            | BookSealed dateTime ->
                book.Seal dateTime
            | BookUnsealed dateTime ->
                book.Unseal dateTime
            | TranslatorAdded (translatorId, dateTime) ->
                book.AddTranslator translatorId dateTime
            | TranslatorRemoved (translatorId, dateTime) ->
                book.RemoveTranslator translatorId dateTime
            | LanguageAdded (language, dateTime) ->
                book.AddLanguage language dateTime
            | LanguageRemoved (language, dateTime) ->
                book.RemoveLanguage language dateTime
            | CurrentLoanSet (loanId, dateTime) ->
                book.SetCurrentLoan loanId dateTime
            | ReturnedFromLoan dateTime ->
                book.ReturnFromLoan dateTime
            | LoanReleased (loanId, dateTime) ->
                book.ReleaseLoan loanId dateTime
            | ReservationAdded (reservationId, dateTime) ->
                book.AddReservation reservationId dateTime
            | ReservationRemoved (reservationId, dateTime) ->
                book.RemoveReservation reservationId dateTime

    static member Deserialize (x: string): Result<BookEvent, string> =
        try
            JsonSerializer.Deserialize<BookEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)