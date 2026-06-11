
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
    | SetLangPref of ShortLang
    | SetCurrentTenant of TenantId
    interface AggregateCommand<User, UserEvent> with
        member this.Execute (user: User) =
            match this with
            | AddReservation reservationId ->
                Ok (user, [ReservationAdded(reservationId)])
            | RemoveReservation reservationId ->
                Ok (user, [ReservationRemoved(reservationId)])
            | AddLoan loanId ->
                Ok (user, [LoanAdded(loanId)])
            | ReleaseLoan loanId ->
                Ok (user, [LoanReleased(loanId)])
            | LoanFromReservation (loanId, reservationId) ->
                Ok (user, [LoanTakenFromReservation(loanId, reservationId)])
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
            | SetCurrentTenant tenantId ->
                user.SetCurrentTenant tenantId
                |> Result.map (fun u -> (u, [CurrentTenantSet tenantId]))
            | SetLangPref langPref ->
                user.SetLangPref langPref
                |> Result.map (fun u -> (u, [LangPrefSet(langPref)]))

        member this.Undoer = None
