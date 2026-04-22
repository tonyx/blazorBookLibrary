namespace BookLibrary.Details
open Sharpino.Core
open Sharpino.Cache
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Details
open System

module Details = 

    open BookLibrary.Domain
    open BookLibrary.Shared.Commons
    open System.Threading

    type RefreshableReservationDetails =
        {
            ReservationDetails: ReservationDetails
            Refresher: Option<CancellationToken> -> TaskResult<ReservationDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! reservationDetails = this.Refresher ct
                return 
                    { 
                        this with
                            ReservationDetails = reservationDetails 
                    }
            }
        interface RefreshableAsync<RefreshableReservationDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct

    type RefreshableUserDetails =
        {
            UserDetails: UserDetails
            Refresher: Option<CancellationToken> -> TaskResult<UserDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! userDetails = this.Refresher ct
                return 
                    { 
                        this with
                            UserDetails = userDetails 
                    }
            }
        interface RefreshableAsync<RefreshableUserDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct
    
    type RefreshableBookDetails =
        {
            BookDetails: BookDetails
            Refresher: Option<CancellationToken> -> TaskResult<BookDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! bookDetails = this.Refresher ct
                return 
                    { 
                        this with
                            BookDetails = bookDetails 
                    }
            }
        interface RefreshableAsync<RefreshableBookDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct

    type RefreshableAuthorDetails =
        {
            AuthorDetails: AuthorDetails
            Refresher: Option<CancellationToken> -> TaskResult<AuthorDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! authorDetails = this.Refresher ct
                return 
                    { 
                        this with
                            AuthorDetails = authorDetails 
                    }
            }
        interface RefreshableAsync<RefreshableAuthorDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct

    type RefreshableLoanDetails =
        {
            LoanDetails: LoanDetails
            Refresher: Option<CancellationToken> -> TaskResult<LoanDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! loanDetails = this.Refresher ct
                return 
                    { 
                        this with
                            LoanDetails = loanDetails 
                    }
            }
        interface RefreshableAsync<RefreshableLoanDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct

    type RefreshableReviewDetails =
        {
            ReviewDetails: ReviewDetails
            Refresher: Option<CancellationToken> -> TaskResult<ReviewDetails, string>
        }
        member this.RefreshAsync ct =
            taskResult {
                let! reviewDetails = this.Refresher ct
                return 
                    { 
                        this with
                            ReviewDetails = reviewDetails 
                    }
            }
        interface RefreshableAsync<RefreshableReviewDetails> with
            member this.RefreshAsync ct =
                this.RefreshAsync ct
