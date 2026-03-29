
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Globalization

type BookCommand =
    | UpdateTitle of Title * DateTime
    | UpdateAuthors of list<AuthorId> * DateTime
    | UpdateEditor of EditorId * DateTime
    | UpdateYear of Year * DateTime
    | UpdateIsbn of Isbn * DateTime
    | AddAuthor of AuthorId * DateTime
    | RemoveAuthor of AuthorId * DateTime
    | AddTranslator of AuthorId * DateTime
    | RemoveTranslator of AuthorId * DateTime
    | AddLanguage of CultureInfo * DateTime
    | RemoveLanguage of CultureInfo * DateTime
    | SetCurrentLoan of LoanId * DateTime
    | ReleaseLoan of LoanId * DateTime
    | AddReservation of ReservationId * DateTime
    | RemoveReservation of ReservationId * DateTime
    | Seal of DateTime
    | Unseal of DateTime
    | ChangeMainCategory of Category * DateTime
    | AddAdditionalCategory of Category * DateTime
    | RemoveAdditionalCategory of Category * DateTime

    interface AggregateCommand<Book, BookEvent> with
        member this.Execute (book: Book) =
            match this with
            | UpdateTitle (title, dateTime) ->
                book.UpdateTitle title dateTime
                |> Result.map (fun b -> (b, [TitleUpdated(title, dateTime)]))
            | UpdateAuthors (authors, dateTime) ->
                book.UpdateAuthors authors dateTime
                |> Result.map (fun b -> (b, [AuthorsUpdated(authors, dateTime)]))
            | UpdateEditor (editor, dateTime) ->
                book.UpdateEditor editor dateTime
                |> Result.map (fun b -> (b, [EditorUpdated(editor, dateTime)]))
            | UpdateYear (year, dateTime) ->
                book.UpdateYear year dateTime
                |> Result.map (fun b -> (b, [YearUpdated(year, dateTime)]))
            | UpdateIsbn (isbn, dateTime) ->
                book.UpdateIsbn isbn dateTime
                |> Result.map (fun b -> (b, [IsbnUpdated(isbn, dateTime)]))
            | AddAuthor (authorId, dateTime) ->
                book.AddAuthor authorId dateTime
                |> Result.map (fun b -> (b, [AuthorAdded(authorId, dateTime)]))
            | RemoveAuthor (authorId, dateTime) ->
                book.RemoveAuthor authorId dateTime
                |> Result.map (fun b -> (b, [AuthorRemoved(authorId, dateTime)]))
            | AddTranslator (translatorId, dateTime) ->
                book.AddTranslator translatorId dateTime
                |> Result.map (fun b -> (b, [TranslatorAdded(translatorId, dateTime)]))
            | RemoveTranslator (translatorId, dateTime) ->
                book.RemoveTranslator translatorId dateTime
                |> Result.map (fun b -> (b, [TranslatorRemoved(translatorId, dateTime)]))
            | AddLanguage (language, dateTime) ->
                book.AddLanguage language dateTime
                |> Result.map (fun b -> (b, [LanguageAdded(language, dateTime)]))
            | RemoveLanguage (language, dateTime) ->
                book.RemoveLanguage language dateTime
                |> Result.map (fun b -> (b, [LanguageRemoved(language, dateTime)]))
            | SetCurrentLoan (loanId, dateTime) ->
                book.SetCurrentLoan loanId dateTime
                |> Result.map (fun b -> (b, [CurrentLoanSet(loanId, dateTime)]))
            | ReleaseLoan (loanId, dateTime) ->
                book.ReleaseLoan loanId dateTime
                |> Result.map (fun b -> (b, [LoanReleased(loanId, dateTime)]))
            | AddReservation (reservationId, dateTime) ->
                book.AddReservation reservationId dateTime
                |> Result.map (fun b -> (b, [ReservationAdded(reservationId, dateTime)]))
            | RemoveReservation (reservationId, dateTime) ->
                book.RemoveReservation reservationId dateTime
                |> Result.map (fun b -> (b, [ReservationRemoved(reservationId, dateTime)]))
            | Seal dateTime ->
                book.Seal dateTime
                |> Result.map (fun b -> (b, [BookSealed(dateTime)]))
            | Unseal dateTime ->
                book.Unseal dateTime
                |> Result.map (fun b -> (b, [BookUnsealed(dateTime)]))
            | ChangeMainCategory (category, dateTime) ->
                book.ChangeMainCategory category dateTime
                |> Result.map (fun b -> (b, [MainCategoryChanged(category, dateTime)]))
            | AddAdditionalCategory (category, dateTime) ->
                book.AddAdditionalCategory category dateTime
                |> Result.map (fun b -> (b, [AdditionalCategoryAdded(category, dateTime)]))
            | RemoveAdditionalCategory (category, dateTime) ->
                book.RemoveAdditionalCategory category dateTime
                |> Result.map (fun b -> (b, [AdditionalCategoryRemoved(category, dateTime)]))


        member this.Undoer = None
