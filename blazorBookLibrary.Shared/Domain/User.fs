
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization

type User001 =
    { 
        Id: UserId
        CurrentTenant: TenantId
        AppUserInfo: AppUserInfo
        Reservations: List<ReservationId>
        CurrentLoans: List<LoanId>
    }
        with 
            member this.Upcast(): User = {
                UserId = this.Id
                CurrentTenant = this.CurrentTenant
                AppUserInfo = this.AppUserInfo
                Reservations = this.Reservations
                CurrentLoans = this.CurrentLoans
                LangPref = ShortLang.New "it"
            }

and User =
    {
        UserId: UserId
        CurrentTenant: TenantId
        AppUserInfo: AppUserInfo
        Reservations: List<ReservationId>
        CurrentLoans: List<LoanId>
        LangPref: ShortLang
    }
    with
        // yes, its correct that any newly created user is set to the default tenant
        static member New (userId: UserId) = 
            { 
                CurrentTenant = TenantId.Default
                UserId = userId
                AppUserInfo = AppUserInfo.NewEmpty(userId)
                Reservations = []
                CurrentLoans = [] 
                LangPref = ShortLang.New "it"
            }
        static member NewWithUserInfo(userId: UserId, appUserInfo: AppUserInfo) = 
            { 
                CurrentTenant = TenantId.Default
                UserId = userId
                AppUserInfo = appUserInfo
                Reservations = []
                CurrentLoans = [] 
                LangPref = ShortLang.New "it" 
            }
    
        member this.AddReservation (reservationId: ReservationId) = 
            { this with Reservations = reservationId :: this.Reservations } |> Ok
        member this.SetCodiceFiscale (fiscalCode: FiscalCode) = 
            { this with AppUserInfo = { this.AppUserInfo with CodiceFiscale = fiscalCode.Value } } |> Ok

        member this.GetCodiceFiscale () =
            match this.AppUserInfo.CodiceFiscale with
            | "" -> FiscalCode.NewEmpty () |> Ok
            | x  when FiscalCode.IsValid x -> FiscalCode.New x
            | x -> FiscalCode.NewInvalid x |> Ok

        member this.SetPhoneNumber (phoneNumber: PhoneNumber) = 
            { this with AppUserInfo = { this.AppUserInfo with PhoneNumber = phoneNumber.Value } } |> Ok

        member this.GetPhoneNumber () =
            match this.AppUserInfo.PhoneNumber with
            | "" -> PhoneNumber.NewEmpty () |> Ok
            | x  when PhoneNumber.IsValid x -> PhoneNumber.New x
            | x -> PhoneNumber.NewInvalid x |> Ok

        member this.SetIsIdentifiedPhysically() = 
            { this with AppUserInfo = { this.AppUserInfo with IsIdentifiedPhysically = true } } |> Ok

        member this.UnsetIdentifiedPhysically() = 
            { this with AppUserInfo = { this.AppUserInfo with IsIdentifiedPhysically = false } } |> Ok

        member this.SetNome (nome: string) = 
            { this with AppUserInfo = { this.AppUserInfo with Nome = nome } } |> Ok

        member this.SetCognome (cognome: string) = 
            { this with AppUserInfo = { this.AppUserInfo with Cognome = cognome } } |> Ok

        member this.GetAppUserInfo () =
            this.AppUserInfo

        member this.SetCurrentTenant (tenantId: TenantId) =
            { this with CurrentTenant = tenantId } |> Ok

        member this.SetAppUserInfo (appUserInfo: AppUserInfo) =
            { this with AppUserInfo = appUserInfo } |> Ok

        member this.RemoveReservation (reservationId: ReservationId) = 
            { this with Reservations = this.Reservations |> List.filter (fun id -> id <> reservationId) } |> Ok

        member this.AddLoan (loanId: LoanId) = 
            { this with CurrentLoans = loanId :: this.CurrentLoans } |> Ok
    
        member this.ReleaseLoan (loanId: LoanId) = 
            { this with CurrentLoans = this.CurrentLoans |> List.filter (fun id -> id <> loanId) } |> Ok

        member this.SetLangPref (langPref: ShortLang) = 
            { this with LangPref = langPref } |> Ok

        member this.ConvertReservationToLoan (loanId: LoanId) (reservationId: ReservationId) = 
            result
                {
                    do! 
                        this.Reservations
                        |> List.contains reservationId
                        |> fun x -> if x then Ok () else Error "User has no future reservation"
                    do! 
                        this.CurrentLoans
                        |> List.contains loanId
                        |> not
                        |> fun x -> if x then Ok () else Error "User has already a current loan"
                    return
                        {
                            this with 
                                Reservations = this.Reservations |> List.filter (fun id -> id <> reservationId)
                                CurrentLoans = loanId :: this.CurrentLoans
                        }
                }

        // this was meant to replace the entire stream of events with GdprGhosted events (identity event) 
        // but it is not needed anymore as anonymizing ApplicationUser is enough
        member this.GdprGhost () =
            this |> Ok

        member this.HasFutureReservation (reservationId: ReservationId) = 
            this.Reservations |> List.contains reservationId
    
        member this.HasCurrentLoan (loanId: LoanId) = 
            this.CurrentLoans |> List.contains loanId

        member this.Id = this.UserId.Value
        static member StorageName = "_User"
        static member SnapshotsInterval = 100
        static member Version = "_01"
        member this.Serialize = 
            (this, jsonOptions) |> JsonSerializer.Serialize
        static member Deserialize (data: string) = 
            try
                (data, jsonOptions) |> JsonSerializer.Deserialize<User> |> Ok
            with
                | ex ->
                    try
                        let result = 
                            (data, jsonOptions) |> JsonSerializer.Deserialize<User001>
                        result.Upcast() |> Ok
                    with
                        | ex2 -> 
                            Error (ex.Message + ", " + ex2.Message)

    
