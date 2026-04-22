
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open BookLibrary.Services.UserMapping
open Sharpino.Cache
open BookLibrary.Details.Details

type ReviewService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        reviewViewerAsync: AggregateViewerAsync2<Review>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>,
        scopeFactory: IServiceScopeFactory
    ) =
    new 
        (eventStore: IEventStore<string>, scopeFactory: IServiceScopeFactory) =
            let messageSenders = MessageSenders.NoSender
            let commentViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore
            let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
            let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
            let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
            let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
            let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore

            ReviewService(
                eventStore,
                messageSenders,
                commentViewerAsync,
                authorViewerAsync,
                editorViewerAsync,
                bookViewerAsync,
                reservationViewerAsync,
                loanViewerAsync,
                userViewerAsync,
                scopeFactory
            )

    new (secretsReader: SecretsReader, scopeFactory: IServiceScopeFactory) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        ReviewService(eventStore, scopeFactory)

    member this.GetReviewAsync (commentId: ReviewId, ?ct: CancellationToken): TaskResult<Review, string> = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult
            {
                let! comment =
                    reviewViewerAsync (Some ct) commentId.Value
                return comment |> snd
            }

    member this.GetAllReviewsAsync (?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult
            {
                let! result =
                    StateView.getAllAggregateStatesAsync<Review, ReviewEvent, string> eventStore (Some ct)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return result
            }
    member this.GetPendingReviewsAsync (?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult
            {
                let! result =
                    StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> (fun review -> review.ApprovalStatus = ApprovalStatus.Pending) eventStore (Some ct)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return result
            }

    member this.AddReviewAsync (comment: Review, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        taskResult
            {
                let! user = 
                    userViewerAsync (Some ct) comment.UserId.Value |> TaskResult.map snd
                let! book = 
                    bookViewerAsync (Some ct) comment.BookId.Value |> TaskResult.map snd

                let! loans = 
                    taskResult {
                        let! loans = 
                            StateView.getAllFilteredAggregateStatesAsync<
                                Loan,
                                LoanEvent,
                                string> 
                                (fun loan -> 
                                    loan.UserId = comment.UserId &&
                                    loan.BookId = comment.BookId &&
                                    loan.LoanStatus.IsReturned)
                                eventStore 
                                (Some ct)
                            |> TaskResult.map (fun x -> x |> List.map snd)
                        return loans 
                    }
                if loans.IsEmpty then 
                    return! Error "User has not borrowed this book"
                
                else
                    let! result =
                        runInitAsync<Review, ReviewEvent, string> 
                            eventStore 
                            messageSenders
                            comment
                            (Some ct)

                    let bookKey = DetailsCacheKey.OfType typeof<RefreshableBookDetails> (book.BookId.Value)
                    DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [|comment.Id|] bookKey

                    let reviewKey = DetailsCacheKey.OfType typeof<RefreshableReviewDetails> (comment.Id)
                    DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [|comment.Id|] reviewKey

                    return result
            }

    member this.EditReviewAsync (commentId: ReviewId, editedComment: string, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        let now = DateTime.UtcNow // todo: review and clarify the policies of dates "now"/"utc now"
        taskResult {
            let! review =
                reviewViewerAsync (Some ct) commentId.Value |> TaskResult.map snd
            let! result =
                CommandHandler.runAggregateCommandMdAsync<Review, ReviewEvent, string> 
                    commentId.Value
                    eventStore
                    messageSenders
                    ""
                    (CommentCommand.Edit (editedComment, now))
                    (Some ct)

            return result
        }

    member this.ApproveAsync (commentId: ReviewId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        let now = DateTime.UtcNow // todo: review and clarify the policies of dates "now"/"utc now"
        CommandHandler.runAggregateCommandMdAsync<Review, ReviewEvent, string> 
            commentId.Value
            eventStore
            messageSenders
            ""
            (CommentCommand.Approve now)
            (Some ct)

    member this.RejectAsync (commentId: ReviewId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        let now = DateTime.UtcNow // todo: review and clarify the policies of dates "now"/"utc now"
        CommandHandler.runAggregateCommandMdAsync<Review, ReviewEvent, string> 
            commentId.Value
            eventStore
            messageSenders
            ""
            (CommentCommand.Reject now)
            (Some ct)

    member this.ShowAsync (commentId: ReviewId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        let now = DateTime.UtcNow // todo: review and clarify the policies of dates "now"/"utc now"
        taskResult {
            let! review =
                reviewViewerAsync (Some ct) commentId.Value |> TaskResult.map snd
            let! result =
                CommandHandler.runAggregateCommandMdAsync<Review, ReviewEvent, string> 
                    commentId.Value
                    eventStore
                    messageSenders
                    ""
                    (CommentCommand.Show now)
                    (Some ct)
            return result
        }

    member this.HideAsync (commentId: ReviewId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None
        let now = DateTime.UtcNow // todo: review and clarify the policies of dates "now"/"utc now"
        taskResult {    
            let! review =
                reviewViewerAsync (Some ct) commentId.Value |> TaskResult.map snd
            let! result =
                CommandHandler.runAggregateCommandMdAsync<Review, ReviewEvent, string> 
                    commentId.Value
                    eventStore
                    messageSenders
                    ""
                    (CommentCommand.Hide now)
                    (Some ct)
            let reviewKey = DetailsCacheKey.OfType typeof<RefreshableReviewDetails> (commentId.Value)
            DetailsCache.Instance.UpdateMultipleAggregateIdAssociation 
                [|commentId.Value; review.UserId.Value; review.BookId.Value|] reviewKey
            let bookKey = DetailsCacheKey.OfType typeof<RefreshableBookDetails> (review.BookId.Value)
            DetailsCache.Instance.UpdateMultipleAggregateIdAssociation 
                [|review.BookId.Value; review.UserId.Value|] bookKey
            let userKey = DetailsCacheKey.OfType typeof<RefreshableUserDetails> (review.UserId.Value)
            DetailsCache.Instance.UpdateMultipleAggregateIdAssociation 
                [|review.UserId.Value; review.BookId.Value|] userKey
            return result
        }

    member this.GetReviewsOfBookAsync (bookId: BookId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult
            {
                let! reviews = 
                    StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> (fun review -> review.BookId = bookId) eventStore (Some ct)
                    |> TaskResult.map (fun x -> x |> List.map snd)

                let! users =
                    reviews
                    |> List.traverseTaskResultM (fun review -> userViewerAsync (Some ct) review.UserId.Value |> TaskResult.map (fun x -> x |> snd))

                let result =
                    List.zip (users |> List.map (fun user -> user.AppUserInfo)) reviews
                return result
            }

    member this.GetApprovedVisibleReviewsOfBookAsync (bookId: BookId, ?ct: CancellationToken) = 
        let ct = ct |> Option.defaultValue CancellationToken.None

        taskResult
            {
                let! reviews = 
                    StateView.getAllFilteredAggregateStatesAsync<Review, ReviewEvent, string> 
                        (fun review -> review.BookId = bookId && review.ApprovalStatus.IsApproved && not review.Hidden) eventStore (Some ct)
                    |> TaskResult.map (fun x -> x |> List.map snd)

                let! users =
                    reviews
                    |> List.traverseTaskResultM (
                        fun review -> 
                            userViewerAsync (Some ct) review.UserId.Value |> TaskResult.map snd
                    )

                let result =
                    List.zip (users |> List.map (fun user -> user.AppUserInfo)) reviews
                return result
            }

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

    interface IReviewService with
        member this.GetReviewAsync (commentId: ReviewId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetReviewAsync (commentId, ct)

        member this.GetAllReviewsAsync (?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetAllReviewsAsync ct

        member this.GetPendingReviewsAsync (?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetPendingReviewsAsync ct

        member this.AddReviewAsync (comment: Review, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.AddReviewAsync (comment, ct)

        member this.EditReviewAsync (commentId: ReviewId, editedComment: string, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.EditReviewAsync (commentId, editedComment, ct)

        member this.ApproveAsync (commentId: ReviewId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.ApproveAsync (commentId, ct)

        member this.RejectAsync (commentId: ReviewId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.RejectAsync (commentId, ct)

        member this.ShowAsync (commentId: ReviewId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.ShowAsync (commentId, ct)

        member this.HideAsync (commentId: ReviewId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.HideAsync (commentId, ct)

        member this.GetReviewsOfBookAsync (bookId: BookId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetReviewsOfBookAsync (bookId, ct)

        member this.GetReviewsOfUserAsync (userId: UserId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetReviewsOfUserAsync (userId, ct)

        member this.GetApprovedVisibleReviewsOfBookAsync (bookId: BookId, ?ct: CancellationToken) = 
            let ct = ct |> Option.defaultValue CancellationToken.None
            this.GetApprovedVisibleReviewsOfBookAsync (bookId, ct)

        

        
        

        
             









    