
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type UserEvent =
    | ReservationAdded of ReservationId
    | ReservationRemoved of ReservationId
    | LoanAdded of LoanId
    | LoanReleased of LoanId
    | LoanTakenFromReservation of LoanId * ReservationId
    | GdprGhosted 
    | CodiceFiscaleSet of FiscalCode
    | PhoneNumberSet of PhoneNumber
    | PhysicalIdentificationSet
    | PhysicalIdentificationUnset
    | NomeSet of string
    | CognomeSet of string
    | AppUserInfoSet of AppUserInfo
    interface Event<User> with
        member this.Process (user: User) : Result<User, string> =
            match this with
            | ReservationAdded reservationId ->
                user.AddReservation reservationId
            | ReservationRemoved reservationId ->
                user.RemoveReservation reservationId
            | LoanAdded loanId ->
                user.AddLoan loanId
            | LoanReleased loanId ->
                user.ReleaseLoan loanId
            | LoanTakenFromReservation (loanId, reservationId) ->
                user.ConvertReservationToLoan loanId reservationId
            | GdprGhosted ->
                user.GdprGhost()
            | CodiceFiscaleSet fiscalCode ->
                user.SetCodiceFiscale fiscalCode
            | PhoneNumberSet phoneNumber ->
                user.SetPhoneNumber phoneNumber
            | PhysicalIdentificationSet ->
                user.SetIsIdentifiedPhysically()
            | PhysicalIdentificationUnset ->
                user.UnsetIdentifiedPhysically()
            | NomeSet nome ->
                user.SetNome nome
            | CognomeSet cognome ->
                user.SetCognome cognome
            | AppUserInfoSet appUserInfo ->
                user.SetAppUserInfo appUserInfo

    static member Deserialize (x: string): Result<UserEvent, string> =
        try
            JsonSerializer.Deserialize<UserEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
