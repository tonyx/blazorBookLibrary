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

    type RefreshableReservationDetails =
        {
            ReservationDetails: ReservationDetails
            Refresher: unit -> Result<ReservationDetails, string>
        }
        member this.Refresh () =
            result {
                let! reservationDetails = this.Refresher ()
                return 
                    { 
                        this with
                            ReservationDetails = reservationDetails 
                    }
            }
        interface Refreshable<RefreshableReservationDetails> with
            member this.Refresh () =
                this.Refresh ()

    type RefreshableUserDetails =
        {
            UserDetails: UserDetails
            Refresher: unit -> Result<UserDetails, string>
        }
        member this.Refresh () =
            result {
                let! userDetails = this.Refresher ()
                return 
                    { 
                        this with
                            UserDetails = userDetails 
                    }
            }
        interface Refreshable<RefreshableUserDetails> with
            member this.Refresh () =
                this.Refresh ()
    
    type RefreshableBookDetails =
        {
            BookDetails: BookDetails
            Refresher: unit -> Result<BookDetails, string>
        }
        member this.Refresh () =
            result {
                let! bookDetails = this.Refresher ()
                return 
                    { 
                        this with
                            BookDetails = bookDetails 
                    }
            }
        interface Refreshable<RefreshableBookDetails> with
            member this.Refresh () =
                this.Refresh ()

    type RefreshableAuthorDetails =
        {
            AuthorDetails: AuthorDetails
            Refresher: unit -> Result<AuthorDetails, string>
        }
        member this.Refresh () =
            result {
                let! authorDetails = this.Refresher ()
                return 
                    { 
                        this with
                            AuthorDetails = authorDetails 
                    }
            }
        interface Refreshable<RefreshableAuthorDetails> with
            member this.Refresh () =
                this.Refresh ()