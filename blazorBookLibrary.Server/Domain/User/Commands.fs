
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type UserCommand =
    | AddReservation of ReservationId
    | RemoveReservation of ReservationId
    | AddLoan of LoanId
    | ReleaseLoan of LoanId
    | LoanFromReservation of LoanId * ReservationId
    | GdprGhost
    | SetCodiceFiscale of FiscalCode
    | SetPhoneNumber of PhoneNumber
    | SetPhysicalIdentification
    | UnsetPhysicalIdentification
    | SetNome of string
    | SetCognome of string
    | SetAppUserInfo of AppUserInfo
    interface AggregateCommand<User, UserEvent> with
        member this.Execute (user: User) =
            match this with
            | AddReservation reservationId ->
                user.AddReservation reservationId
                |> Result.map (fun u -> (u, [ReservationAdded(reservationId)]))
            | RemoveReservation reservationId ->
                user.RemoveReservation reservationId
                |> Result.map (fun u -> (u, [ReservationRemoved(reservationId)]))
            | AddLoan loanId ->
                user.AddLoan loanId
                |> Result.map (fun u -> (u, [LoanAdded(loanId)]))
            | ReleaseLoan loanId ->
                user.ReleaseLoan loanId
                |> Result.map (fun u -> (u, [LoanReleased(loanId)]))
            | LoanFromReservation (loanId, reservationId) ->
                user.ConvertReservationToLoan loanId reservationId
                |> Result.map (fun u -> (u, [LoanTakenFromReservation(loanId, reservationId)]))
            | GdprGhost ->
                user.GdprGhost()
                |> Result.map (fun u -> (u, [GdprGhosted]))
            | SetCodiceFiscale fiscalCode ->
                user.SetCodiceFiscale fiscalCode
                |> Result.map (fun u -> (u, [CodiceFiscaleSet(fiscalCode)]))
            | SetPhoneNumber phoneNumber ->
                user.SetPhoneNumber phoneNumber
                |> Result.map (fun u -> (u, [PhoneNumberSet(phoneNumber)]))
            | SetPhysicalIdentification ->
                user.SetIsIdentifiedPhysically()
                |> Result.map (fun u -> (u, [PhysicalIdentificationSet]))
            | UnsetPhysicalIdentification ->
                user.UnsetIdentifiedPhysically()
                |> Result.map (fun u -> (u, [PhysicalIdentificationUnset]))
            | SetNome nome ->
                user.SetNome nome
                |> Result.map (fun u -> (u, [NomeSet(nome)]))
            | SetCognome cognome ->
                user.SetCognome cognome
                |> Result.map (fun u -> (u, [CognomeSet(cognome)]))
            | SetAppUserInfo appUserInfo ->
                user.SetAppUserInfo appUserInfo
                |> Result.map (fun u -> (u, [AppUserInfoSet(appUserInfo)]))

        member this.Undoer = None
