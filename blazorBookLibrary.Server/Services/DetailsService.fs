
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
    tenantViewerAsync: AggregateViewerAsync2<Tenant>,
    distributionPointViewerAsync: AggregateViewerAsync2<DistributionPoint>,
    loanService: ILoanService,
    reservationService: IReservationService,
    reviewService: IReviewService,
    scopeFactory: IServiceScopeFactory)
    =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenant = tenantViewerAsync (ct |> Some) context.TenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    member this.GetReviewsOfUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult {
            let! user = userViewerAsync (Some ct) userId.Value |> TaskResult.map snd

            // todo: check permissions roles
            // do! user.Tenants |> Seq.contains context.TenantId |> Result.ofBool $"User '{user.AppUserInfo.Email}' doesn't have access to this tenant '{context.TenantId}'"

            let! reviewsWithId = 
                StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> (fun review -> review.UserId = userId) eventStore (Some ct)
            let reviews = 
                reviewsWithId
                |> List.ofSeq
                |> List.map snd
            let! booksInvolved =
                reviews
                |> List.traverseTaskResultM (fun review -> bookViewerAsync (Some ct) review.BookId.Value |> TaskResult.map (fun x -> x |> snd))
            let result = List.zip booksInvolved reviews
            return result
        }

    member private this.MakeUserDetailsRefresher(context: UserContext, id: UserId, ?ct: CancellationToken) = 
        fun (ct: Option<CancellationToken>) -> 
            taskResult {
                let ct = ct |> Option.defaultValue CancellationToken.None
                let! user = userViewerAsync (Some ct) id.Value |> TaskResult.map snd

                // todo: check permissions roles
                // do! user.Tenants |> Seq.contains context.TenantId |> Result.ofBool $"User '{user.AppUserInfo.Email}' doesn't have access to this tenant '{context.TenantId}'"

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

                let reservationsAndBooks = List.zip futurereservations reservedBooks

                let! loansedBooks =
                    currentLoans
                    |> List.traverseTaskResultM (fun loan -> bookViewerAsync (Some ct) loan.BookId.Value |> TaskResult.map snd)

                let loansAndBooks = List.zip currentLoans loansedBooks

                let! booksAndReviews = this.GetReviewsOfUserAsync(context, id, ct)
                let! currentTenant = tenantViewerAsync (Some ct) user.CurrentTenant.Value |> TaskResult.map snd

                return 
                    {
                        User = user
                        AppUser = user.AppUserInfo
                        CurrentTenant = currentTenant
                        FutureReservations = reservationsAndBooks
                        CurrentLoans = loansAndBooks
                        BooksAndReviews = booksAndReviews
                    }
            }

    member private this.GetRefreshableUserDetailsAsync(context: UserContext, userId: UserId, ?ct:CancellationToken): TaskResult<RefreshableUserDetails, string> =
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher = this.MakeUserDetailsRefresher(context, userId, ct)

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

    member this.GetUserDetailsAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableUserDetails = this.GetRefreshableUserDetailsAsync(context, userId, ct)
            return refreshableUserDetails.UserDetails
        }

    member private this.GetRefreshableLoanDetailsAsync (context: UserContext, loanId: LoanId, ?ct: CancellationToken): TaskResult<RefreshableLoanDetails, string> = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let! loan = loanViewerAsync ct loanId.Value |> TaskResult.map snd
                            do! loan.TenantId = context.TenantId |> Result.ofBool $"Loan '{loanId}' doesn't belong to this tenant '{context.TenantId}'"
                            let! book = bookViewerAsync ct loan.BookId.Value |> TaskResult.map snd
                            let! userDetail = this.GetUserDetailsAsync (context, loan.UserId, ct |> Option.defaultValue CancellationToken.None)
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

    member this.GetLoanDetailsAsync (context: UserContext, loanId: LoanId, ?ct: CancellationToken): TaskResult<LoanDetails, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableLoanDetails = this.GetRefreshableLoanDetailsAsync(context, loanId, ct)
            return refreshableLoanDetails.LoanDetails
        }

    member this.GetAllLoanDetailsAsync (context: UserContext, ?ct: CancellationToken): TaskResult<List<LoanDetails>, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! allLoans = loanService.GetLoansAsync (context, ct)
            return! allLoans |> List.traverseTaskResultM (fun loan -> this.GetLoanDetailsAsync(context, loan.LoanId, ct))
        }

    member this.MakeReservationRefresher(context: UserContext, id: ReservationId, ?ct:CancellationToken) = 
        fun (ct: Option<CancellationToken>) ->
            taskResult {
                let ct = ct |> Option.defaultValue CancellationToken.None
                let! reservation = reservationViewerAsync (ct |> Some) id.Value |> TaskResult.map snd
                do! reservation.TenantId = context.TenantId |> Result.ofBool $"Reservation '{id}' doesn't belong to this tenant '{context.TenantId}'"
                let! book = bookViewerAsync (ct |> Some) reservation.BookId.Value |> TaskResult.map snd
                let! userDetails = this.GetUserDetailsAsync (context, reservation.UserId, ct)
                return 
                    {
                        Reservation = reservation
                        Book = book
                        UserDetails = userDetails
                    }
            }

    member private this.GetRefreshableReservationDetailsAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher = this.MakeReservationRefresher(context, id, ct) 
                this.MakeReservationDetailsBuilder(id, refresher, ct)

        let key = DetailsCacheKey.OfType typeof<RefreshableReservationDetails> id.Value
        StateView.getRefreshableDetailsTaskResultAsync<RefreshableReservationDetails> (fun ct -> detailsBuilder ct) key ct

    member this.GetReservationDetailsAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableDetails = this.GetRefreshableReservationDetailsAsync (context, id, ct)
            return refreshableDetails.ReservationDetails
        }

    member private this.MakeReservationDetailsBuilder(id: ReservationId, refresher: Option<CancellationToken> -> TaskResult<ReservationDetails, string>, ct: CancellationToken) = 
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

    member private this.GetRefreshableAuthorDetailsAsync(context: UserContext, id: AuthorId, ?ct: CancellationToken) =
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! author = authorViewerAsync (Some ct) id.Value |> TaskResult.map snd
                            do! author.TenantId = context.TenantId |> Result.ofBool $"Author '{id}' doesn't belong to this tenant '{context.TenantId}'"
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

    member this.GetAuthorDetailsAsync (context: UserContext, id: AuthorId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableAuthorDetails = this.GetRefreshableAuthorDetailsAsync (context, id, ct)
            return refreshableAuthorDetails.AuthorDetails 
        }

    member private this.GetRefreshableBookDetailsAsync(context: UserContext, bookId: BookId, ?ct:CancellationToken): TaskResult<RefreshableBookDetails, string> =
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                            do! book.TenantId = context.TenantId |> Result.ofBool $"Book '{bookId}' doesn't belong to this tenant '{context.TenantId}'"
                            let! currentLoan = 
                                match book.CurrentLoan with
                                | Some loanId -> 
                                    taskResult {
                                        let! loan = this.GetLoanDetailsAsync (context, loanId, ct)
                                        return loan |> Some
                                    }
                                | None -> taskResult { return None }
                            let! authors = 
                                book.Authors
                                |> List.traverseTaskResultM (fun authorId -> authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd)
                            let! futureReservations = 
                                book.CurrentReservations
                                |> List.traverseTaskResultM (fun reservationId -> reservationService.GetReservationDetailsAsync (context, reservationId, ct))

                            let! approvedVisibleReviews = this.GetApprovedVisibleReviewsOfBookAsync (context, bookId, ct)

                            let! distributionPoint = 
                                match book.DistributionPoint with
                                | None -> taskResult { return None }
                                | Some distributionPointId -> 
                                    taskResult {
                                        let! distributionPoint = distributionPointViewerAsync (ct |> Some) distributionPointId.Value|> TaskResult.map (fun (x, y) -> y )
                                        return distributionPoint |> Some
                                    }
                            return 
                                { 
                                    Authors = authors
                                    Book = book
                                    CurrentLoan = currentLoan
                                    DistributionPoint = distributionPoint
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

    member this.GetBookDetailsAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken): TaskResult<BookDetails, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableBookDetails = this.GetRefreshableBookDetailsAsync(context, bookId, ct)
            return refreshableBookDetails.BookDetails
        }

    member this.GetAllPendingReservationDetailsAsync (context: UserContext, ?ct: CancellationToken): TaskResult<List<ReservationDetails>, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! reservations = 
                StateView.getAllFilteredAggregateStatesAsync<Reservation, ReservationEvent, string> (fun reservation -> reservation.IsPending && reservation.TenantId = context.TenantId) eventStore (Some ct)
                |> TaskResult.map (fun reservations -> reservations |> List.map snd)
            return! reservations |> List.traverseTaskResultM (fun reservation -> this.GetReservationDetailsAsync (context, reservation.ReservationId, ct))
        }

    member this.GetRefreshableReviewDetailsAsync (context: UserContext, reviewId: ReviewId, ?ct: CancellationToken): TaskResult<RefreshableReviewDetails, string> = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! review = reviewsViewerAsync (Some ct) reviewId.Value |> TaskResult.map snd
                            do! review.TenantId = context.TenantId |> Result.ofBool $"Review '{reviewId}' doesn't belong to this tenant '{context.TenantId}'"
                            let! user = userViewerAsync (Some ct) review.UserId.Value |> TaskResult.map snd
                            let! book = bookViewerAsync (Some ct) review.BookId.Value |> TaskResult.map snd
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

    member this.GetReviewDetailsAsync (context: UserContext, reviewId: ReviewId, ?ct: CancellationToken): TaskResult<ReviewDetails, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! refreshableReviewDetails = this.GetRefreshableReviewDetailsAsync(context, reviewId, ct)
            return refreshableReviewDetails.ReviewDetails
        }

    member this.GetAllReviewsDetailsAsync (context: UserContext, ?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! reviews = 
                StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> (fun review -> review.TenantId = context.TenantId) eventStore (Some ct)
                |> TaskResult.map (fun reviews -> reviews |> List.map snd)
            return! reviews |> List.traverseTaskResultM (fun review -> this.GetReviewDetailsAsync (context, review.ReviewId, ct))
        }

    member this.GetApprovedVisibleReviewsOfBookAsync (context: UserContext, bookId:BookId, ?ct: CancellationToken): TaskResult<List<ReviewDetails>, string> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! book = bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
            do! book.TenantId = context.TenantId |> Result.ofBool $"Book '{bookId}' doesn't belong to this tenant '{context.TenantId}'"

            let! reviews = reviewService.GetApprovedVisibleReviewsOfBookAsync (context, bookId, ct) |> TaskResult.map (fun reviews -> reviews |> List.map snd)
            return! reviews |> List.traverseTaskResultM (fun review -> this.GetReviewDetailsAsync (context, review.ReviewId, ct))
        }



    new(eventStore: IEventStore<string>, loanService: ILoanService, reservationService: IReservationService, reviewService: IReviewService, scopeFactory: IServiceScopeFactory) =
        DetailsService(
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> eventStore,
            loanService,
            reservationService,
            reviewService,
            scopeFactory
        )

    new(configuration: IConfiguration, loanService: ILoanService, reservationService: IReservationService, reviewService: IReviewService, scopeFactory: IServiceScopeFactory, secretsReader: SecretsReader) =
        DetailsService(PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString ()), loanService, reservationService, reviewService, scopeFactory)

    interface IDetailsService with
        member this.GetUserDetailsAsync (context, userId, ?ct) = 
            this.GetUserDetailsAsync(context, userId, defaultArg ct CancellationToken.None)
        member this.GetLoanDetailsAsync (context, loanId, ?ct) = 
            this.GetLoanDetailsAsync(context, loanId, defaultArg ct CancellationToken.None)
        member this.GetAllLoansDetailsAsync (context, ?ct) = 
            this.GetAllLoanDetailsAsync(context, defaultArg ct CancellationToken.None)
        member this.GetBookDetailsAsync (context, bookId, ?ct) = 
            this.GetBookDetailsAsync(context, bookId, defaultArg ct CancellationToken.None)
        member this.GetReservationDetailsAsync (context, reservationId, ?ct) = 
            this.GetReservationDetailsAsync(context, reservationId, defaultArg ct CancellationToken.None)
        member this.GetAuthorDetailsAsync (context, authorId, ?ct) = 
            this.GetAuthorDetailsAsync(context, authorId, defaultArg ct CancellationToken.None)
        member this.GetAllPendingReservationsDetailsAsync (context, ?ct) = 
            this.GetAllPendingReservationDetailsAsync(context, defaultArg ct CancellationToken.None)
        member this.GetReviewDetailsAsync (context, reviewId, ?ct) = 
            this.GetReviewDetailsAsync(context, reviewId, defaultArg ct CancellationToken.None)
        member this.GetAllReviewsDetailsAsync (context, ?ct) = 
            this.GetAllReviewsDetailsAsync(context, defaultArg ct CancellationToken.None)
        member this.GetApprovedVisibleReviewsOfBookAsync (context, bookId, ?ct) = 
            this.GetApprovedVisibleReviewsOfBookAsync (context, bookId, defaultArg ct CancellationToken.None)
