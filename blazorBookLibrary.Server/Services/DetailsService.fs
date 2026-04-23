
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open BookLibrary.Services.UserMapping
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection
open FsToolkit.ErrorHandling

type DetailsService (
    eventStore: IEventStore<string>,
    messageSenders: MessageSenders,
    bookViewerAsync: AggregateViewerAsync2<Book>,
    authorViewerAsync: AggregateViewerAsync2<Author>,
    editorViewerAsync: AggregateViewerAsync2<Editor>,
    reservationViewerAsync: AggregateViewerAsync2<Reservation>,
    loanViewerAsync: AggregateViewerAsync2<Loan>,
    userViewerAsync: AggregateViewerAsync2<User>,
    reviewsViewerAsync: AggregateViewerAsync2<Review>,
    loanService: ILoanService,
    reservationService: IReservationService,
    reviewService: IReviewService,
    scopeFactory: IServiceScopeFactory)
    =
    new (eventStore: IEventStore<string>, loanService, reservationService, reviewService, scopeFactory) =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let reviewsViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore
        DetailsService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            reviewsViewerAsync,
            loanService,
            reservationService,
            reviewService,
            scopeFactory
        )
    new (configuration: IConfiguration, loanService: ILoanService, reservationService: IReservationService, reviewService: IReviewService, scopeFactory: IServiceScopeFactory, secretsReader: SecretsReader) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        DetailsService (eventStore, loanService, reservationService, reviewService, scopeFactory)

        member this.GetReviewsOfUserAsync (userId: UserId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            taskResult
                {
                    let! reviewsWithId = 
                        StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> (fun review -> review.UserId = userId) eventStore (Some ct)
                    let reviews = 
                        reviewsWithId
                        |> List.ofSeq
                        |> List.map snd
                    let! booksInvolved =
                        reviews
                        |> List.traverseTaskResultM (fun review -> bookViewerAsync (Some ct) review.BookId.Value |> TaskResult.map (fun x -> x |> snd))
                    let result =
                        List.zip booksInvolved reviews
                    return result
                }

        member private 
            this.MakeUserDetailsRefresher(id: UserId, ?ct: CancellationToken) = 
                fun (ct: Option<CancellationToken>) -> 
                    taskResult 
                        {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! user = userViewerAsync (Some ct) id.Value |> TaskResult.map snd
                            let! futurereservations = 
                                user.Reservations 
                                |> List.traverseTaskResultM (fun reservationId -> reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd)
                            let! currentLoans =
                                user.CurrentLoans
                                |> List.traverseTaskResultM (fun loanId -> loanViewerAsync (Some ct) loanId.Value |> TaskResult.map snd)

                            let! user = userViewerAsync (Some ct) id.Value |> TaskResult.map snd

                            let! reservedBooks =
                                futurereservations
                                |> List.traverseTaskResultM (fun reservation -> bookViewerAsync (Some ct) reservation.BookId.Value |> TaskResult.map snd)

                            let reservationsAndBooks =
                                List.zip futurereservations reservedBooks

                            let! loansedBooks =
                                currentLoans
                                |> List.traverseTaskResultM (fun loan -> bookViewerAsync (Some ct) loan.BookId.Value |> TaskResult.map snd)

                            let loansAndBooks =
                                List.zip currentLoans loansedBooks

                            let! booksAndReviews =
                                this.GetReviewsOfUserAsync(id, ct)
                                
                            return 
                                {
                                    User = user
                                    AppUser = user.AppUserInfo
                                    FutureReservations = reservationsAndBooks
                                    CurrentLoans = loansAndBooks
                                    BooksAndReviews = booksAndReviews
                                }
                        }

        member private 
            this.GetRefreshableUserDetailsAsync(userId: UserId, ?ct:CancellationToken): TaskResult<RefreshableUserDetails, string> =
                let detailsBuilder =
                    fun (ct: Option<CancellationToken>) ->
                        let ct = ct |> Option.defaultValue CancellationToken.None
                        let refresher = this.MakeUserDetailsRefresher(userId, ct)

                        taskResult {
                            let! userDetails = refresher (Some ct)
                            return 
                                { 
                                    UserDetails = userDetails
                                    Refresher = refresher
                                } :> RefreshableAsync<RefreshableUserDetails>
                                ,
                                userId.Value :: 
                                (userDetails.CurrentLoans |> List.map (fun (x,_) -> x.LoanId.Value)) @ 
                                (userDetails.FutureReservations |> List.map (fun (x, _) -> x.ReservationId.Value)) @
                                (userDetails.FutureReservations |> List.map (fun (_, x) -> x.BookId.Value)) @
                                (userDetails.CurrentLoans |> List.map (fun (_, x) -> x.BookId.Value))
                        }
                let key = DetailsCacheKey.OfType typeof<RefreshableUserDetails> userId.Value
                StateView.getRefreshableDetailsTaskResultAsync<RefreshableUserDetails> (fun ct -> detailsBuilder ct) key ct
        member  this.GetUserDetailsAsync (userId: UserId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! refreshableUserDetails =
                    this.GetRefreshableUserDetailsAsync(userId, ct)
                return refreshableUserDetails.UserDetails
            }

        member private this.GetRefreshableLoanDetailsAsync (loanId: LoanId, ?ct: CancellationToken): TaskResult<RefreshableLoanDetails, string> = 
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let refresher =
                        fun (ct: Option<CancellationToken>) ->
                            taskResult {
                                let! loan = 
                                    loanViewerAsync ct loanId.Value |> TaskResult.map snd
                                let! book = 
                                    bookViewerAsync ct loan.BookId.Value |> TaskResult.map snd
                                let! userDetail = 
                                    this.GetUserDetailsAsync (loan.UserId, ct |> Option.defaultValue CancellationToken.None)
                                return
                                    { 
                                        Loan = loan
                                        Book = book
                                        UserDetails = userDetail
                                    }
                            }
                    taskResult {
                        let! loanDetails = refresher (Some ct)
                        return
                            {
                                LoanDetails = loanDetails
                                Refresher = refresher
                            } :> RefreshableAsync<RefreshableLoanDetails>
                            ,
                            [
                                loanId.Value;
                                loanDetails.Book.Id;
                                loanDetails.UserDetails.User.Id
                            ]
                        }
            let key = DetailsCacheKey.OfType typeof<RefreshableLoanDetails> loanId.Value    
            StateView.getRefreshableDetailsTaskResultAsync<RefreshableLoanDetails> (fun ct -> detailsBuilder ct) key ct

        member this.GetLoanDetailsAsync (loanId: LoanId, ?ct: CancellationToken): TaskResult<LoanDetails, string> = 
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! refreshableLoanDetails =
                    this.GetRefreshableLoanDetailsAsync(loanId, ct)
                return refreshableLoanDetails.LoanDetails
            }

        member  this.GetAllLoanDetailsAsync (?ct: CancellationToken): TaskResult<List<LoanDetails>, string> = 
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! allLoans =
                    loanService.GetLoansAsync ct
                let! result  =
                    allLoans 
                    |> List.traverseTaskResultM (fun loan -> this.GetLoanDetailsAsync(loan.LoanId, ct))
                return result
            }

        member this.MakeReservationRefresher(id: ReservationId, ?ct:CancellationToken) = 
            fun (ct: Option<CancellationToken>) ->
                taskResult
                    {
                        let ct = ct |> Option.defaultValue CancellationToken.None
                        let! reservation = 
                            reservationViewerAsync (ct |> Some) id.Value |> TaskResult.map snd
                        let! book = 
                            bookViewerAsync (ct |> Some) reservation.BookId.Value |> TaskResult.map snd
                        let! userDetails = 
                            this.GetUserDetailsAsync (reservation.UserId, ct)
                        return 
                            {
                                Reservation = reservation
                                Book = book
                                UserDetails = userDetails
                            }
                    }

        member private this.GetRefreshableReservationDetailsAsync (id: ReservationId, ?ct: CancellationToken) = 
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let refresher = 
                        this.MakeReservationRefresher(id, ct) 
                    this.MakeReservationDetailsBuilder(id, refresher, ct)

            let key = DetailsCacheKey.OfType typeof<RefreshableReservationDetails> id.Value
            StateView.getRefreshableDetailsTaskResultAsync<RefreshableReservationDetails> (fun ct -> detailsBuilder ct) key ct

        member this.GetReservationDetailsAsync (id: ReservationId, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableDetails = this.GetRefreshableReservationDetailsAsync (id, ct)
                    return refreshableDetails.ReservationDetails
                }

        member private 
            this.MakeReservationDetailsBuilder(id: ReservationId, refresher: Option<CancellationToken> -> TaskResult<ReservationDetails, string>, ct: CancellationToken) = 
                taskResult {
                    let! reservationDetails = refresher(Some ct)
                    return 
                        {
                            ReservationDetails = reservationDetails    
                            Refresher = refresher
                        } :> RefreshableAsync<RefreshableReservationDetails>
                        ,
                        [id.Value ;
                        reservationDetails.Reservation.BookId.Value ;
                        reservationDetails.Book.BookId.Value]
                    }

        member private
            this.GetRefreshableAuthorDetailsAsync(id: AuthorId, ?ct: CancellationToken) =
                let detailsBuilder =
                    fun (ct: Option<CancellationToken>) ->
                        let ct = ct |> Option.defaultValue CancellationToken.None
                        let refresher =
                            fun (ct: Option<CancellationToken>) ->
                                taskResult {
                                    let ct = ct |> Option.defaultValue CancellationToken.None
                                    let! author = 
                                        authorViewerAsync (Some ct) id.Value |> TaskResult.map snd
                                    let! books = 
                                        author.Books
                                        |> List.traverseTaskResultM (fun bookId -> bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd)
                                    return
                                        {
                                            Author = author
                                            Books = books
                                        }
                                }
                        taskResult {
                            let! authorDetails = refresher(Some ct)
                            return
                                {
                                    AuthorDetails = authorDetails
                                    Refresher = refresher
                                } :> RefreshableAsync<RefreshableAuthorDetails>
                                ,
                                id.Value :: (authorDetails.Author.Books |> List.map _.Value)
                        }
                let key = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> id.Value
                StateView.getRefreshableDetailsTaskResultAsync<RefreshableAuthorDetails> (fun ct -> detailsBuilder ct) key ct

        member private
            this.GetAuthorDetailsAsync (id: AuthorId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                taskResult {
                    let! refreshableAuthorDetails = this.GetRefreshableAuthorDetailsAsync (id, ct)
                    return refreshableAuthorDetails.AuthorDetails 
                }
        member private 
            this.GetRefreshableBookDetailsAsync(bookId: BookId, ?ct:CancellationToken): TaskResult<RefreshableBookDetails, string> =
                let detailsBuilder =
                    fun (ct: Option<CancellationToken>) ->
                        let ct = ct |> Option.defaultValue CancellationToken.None
                        let refresher =
                            fun (ct: Option<CancellationToken>) ->
                                taskResult {
                                    let ct = ct |> Option.defaultValue CancellationToken.None
                                    let! book = 
                                        bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                                    let! currentLoan = 
                                        match book.CurrentLoan with
                                        | Some loanId -> 
                                            taskResult {
                                                let! loan = this.GetLoanDetailsAsync (loanId, ct)
                                                return loan |> Some
                                            }
                                        | None -> 
                                            taskResult { return None }
                                    let! authors = 
                                        book.Authors
                                        |> List.traverseTaskResultM (fun authorId -> authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd)
                                    let! futureReservations = 
                                        book.CurrentReservations
                                        |> List.traverseTaskResultM (fun reservationId -> reservationService.GetReservationDetailsAsync (reservationId, ct))

                                    let! approvedVisibleReviews = 
                                        this.GetApprovedVisibleReviewsOfBookAsync (bookId, ct)
                                    return 
                                        { 
                                            Authors = authors
                                            Book = book
                                            CurrentLoan = currentLoan
                                            ReservationsDetails = futureReservations
                                            ApprovedVisibleReviews = approvedVisibleReviews 
                                        } 
                                }

                        taskResult {
                            let! bookDetails = refresher (Some ct)
                            return 
                                { 
                                    BookDetails = bookDetails
                                    Refresher = refresher
                                } :> RefreshableAsync<RefreshableBookDetails>
                                ,
                                bookId.Value :: 
                                (if bookDetails.CurrentLoan.IsSome then [bookDetails.CurrentLoan.Value.Loan.LoanId.Value] else []) @ 
                                (bookDetails.ReservationsDetails |> List.map _.Reservation.ReservationId.Value) @
                                (bookDetails.Authors |> List.map _.AuthorId.Value)@
                                (bookDetails.ApprovedVisibleReviews |> List.map _.Review.Id)
                        }
                let key = DetailsCacheKey.OfType typeof<RefreshableBookDetails> bookId.Value
                StateView.getRefreshableDetailsTaskResultAsync<RefreshableBookDetails> (fun ct -> detailsBuilder ct) key ct

        member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken): TaskResult<BookDetails, string> = 
            taskResult {
                let ct = defaultArg ct CancellationToken.None
                let! refreshableBookDetails =
                    this.GetRefreshableBookDetailsAsync(bookId, ct)
                return refreshableBookDetails.BookDetails
            }

        member this.GetAllPendingReservationDetailsAsync (?ct: CancellationToken): TaskResult<List<ReservationDetails>, string> = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! reservations = 
                        StateView.getAllFilteredAggregateStatesAsync<Reservation, ReservationEvent, string> (fun reservation -> reservation.IsPending) eventStore (Some ct)
                        |> TaskResult.map (fun reservations -> reservations |> List.map snd)
                    let! reservationDetails = 
                        reservations
                        |> List.traverseTaskResultM (fun reservation -> this.GetReservationDetailsAsync (reservation.ReservationId, ct))
                    return reservationDetails 
                }

        member this.GetRefreshableReviewDetailsAsync (reviewId: ReviewId, ?ct: CancellationToken): TaskResult<RefreshableReviewDetails, string> = 
            use scope = scopeFactory.CreateScope()
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let refresher =
                        fun (ct: Option<CancellationToken>) ->
                            taskResult {
                                let ct = ct |> Option.defaultValue CancellationToken.None
                                let! review = 
                                    reviewsViewerAsync (Some ct) reviewId.Value |> TaskResult.map snd
                                let! user = userViewerAsync (Some ct) review.UserId.Value |> TaskResult.map snd
                                let! book = 
                                    bookViewerAsync (Some ct) review.BookId.Value
                                    |> TaskResult.map snd
                                let! authors = 
                                    book.Authors
                                    |> List.traverseTaskResultM (fun authorId -> authorViewerAsync (ct |> Some) authorId.Value |> TaskResult.map snd)
                                return
                                    { 
                                        Review = review
                                        AppUser = user.AppUserInfo
                                        Book = book
                                        Authors = authors
                                    }   
                            }

                    taskResult {
                        let! reviewDetails = refresher (Some ct)
                        return
                            {
                                ReviewDetails = reviewDetails
                                Refresher = refresher
                            } :> RefreshableAsync<RefreshableReviewDetails>
                            ,
                            [
                                reviewId.Value; 
                                reviewDetails.Book.Id
                            ]
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableReviewDetails> reviewId.Value
            StateView.getRefreshableDetailsTaskResultAsync<RefreshableReviewDetails> (fun ct -> detailsBuilder ct) key ct 

        member this.GetReviewDetailsAsync (reviewId: ReviewId, ?ct: CancellationToken): TaskResult<ReviewDetails, string> = 
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! refreshableReviewDetails = this.GetRefreshableReviewDetailsAsync(reviewId, ct)
                return refreshableReviewDetails.ReviewDetails
            }

        member this.GetAllReviewsDetailsAsync (?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
            let ct = defaultArg ct CancellationToken.None
            taskResult
                {
                    let! reviews = 
                        StateView.getAllAggregateStatesAsync<Review, ReviewEvent, string> eventStore (Some ct)
                        |> TaskResult.map (fun reviews -> reviews |> List.map snd)
                    let! reviewDetails = 
                        reviews
                        |> List.traverseTaskResultM (fun review -> this.GetReviewDetailsAsync (review.ReviewId, ct))
                    return reviewDetails 
                }

        member this.GetApprovedVisibleReviewsOfBookAsync (bookId:BookId, ?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
            let ct = defaultArg ct CancellationToken.None
            taskResult {

                let! reviews = 
                    reviewService.GetApprovedVisibleReviewsOfBookAsync (bookId, ct) |> TaskResult.map (fun reviews -> reviews |> List.map snd)

                let! reviewDetails = 
                    reviews
                    |> List.traverseTaskResultM 
                        (fun review -> 
                            let res = this.GetReviewDetailsAsync (review.ReviewId, ct)
                            res)
                return reviewDetails 
            }

        interface IDetailsService with
            member this.GetUserDetailsAsync (userId: UserId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetUserDetailsAsync(userId, ct)

            member this.GetLoanDetailsAsync (loanId: LoanId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                taskResult {
                    return! this.GetLoanDetailsAsync(loanId, ct)
                }

            member this.GetAllLoansDetailsAsync (?ct: CancellationToken): TaskResult<List<LoanDetails>, string> = 
                let ct = defaultArg ct CancellationToken.None
                this.GetAllLoanDetailsAsync ct

            member this.GetBookDetailsAsync (bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                taskResult {
                    let! refreshableBookDetails = 
                        this.GetRefreshableBookDetailsAsync(bookId, ct)
                    return refreshableBookDetails.BookDetails
                }

            member this.GetReservationDetailsAsync (reservationId: ReservationId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetReservationDetailsAsync(reservationId, ct)

            member this.GetAuthorDetailsAsync (authorId: AuthorId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetAuthorDetailsAsync(authorId, ct)

            member this.GetAllPendingReservationsDetailsAsync (?ct: CancellationToken): TaskResult<List<ReservationDetails>, string> = 
                let ct = defaultArg ct CancellationToken.None
                this.GetAllPendingReservationDetailsAsync(ct)

            member this.GetReviewDetailsAsync (reviewId: ReviewId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetReviewDetailsAsync(reviewId, ct)

            member this.GetAllReviewsDetailsAsync (?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
                let ct = defaultArg ct CancellationToken.None
                this.GetAllReviewsDetailsAsync(ct)

            member this.GetApprovedVisibleReviewsOfBookAsync (bookId:BookId, ?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
                let ct = defaultArg ct CancellationToken.None
                this.GetApprovedVisibleReviewsOfBookAsync (bookId, ct)
            
        








