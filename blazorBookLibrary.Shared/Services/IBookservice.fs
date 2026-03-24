namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Definitions
open Sharpino.Core
// open Sharpino.Cache
open BookLibrary.Domain
open BookLibrary.Shared.Commons
// open BookLibrary.Details.Details

type IBookService =
    abstract member AddBookAsync : book: Book * ?ct: CancellationToken -> TaskResult<EventId * Book, string>
    abstract member AddAuthorToBookAsync : authorId: AuthorId * bookId: BookId * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * (Book * Author), string>
    abstract member RemoveAuthorFromBookAsync : authorId: AuthorId * bookId: BookId * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * (Book * Author), string>
    abstract member GetBookAsync : id: BookId * ?ct: CancellationToken -> Task<Result<Book, string>>
    // abstract member GetBookDetailAsync : bookId: BookId * ?ct: CancellationToken -> Task<Refreshable<BookDetails>>
    
    // // Other missing general purpose members
    // abstract member AddReservationAsync : reservation: Reservation * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * (Book * Reservation), string>
    // abstract member RemoveReservationAsync : reservationId: ReservationId * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * Book, string>
    // abstract member AddLoanAsync : loan: Loan * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * (Book * Loan), string>
    // abstract member ReleaseLoanAsync : loanId: LoanId * dateTime: System.DateTime * ?ct: CancellationToken -> TaskResult<EventId * (Book * Loan), string>
