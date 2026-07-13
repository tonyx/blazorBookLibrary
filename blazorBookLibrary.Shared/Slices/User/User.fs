namespace BookLibrary.Domain

open System.Text.Json
open BookLibrary.Shared.Commons

type User =
    { UserId: UserId
      CurrentTenant: TenantId
      AppUserInfo: AppUserInfo
      LangPref: ShortLang }
    // yes, it's correct that any newly created user is set to the default tenant
    static member New(userId: UserId) =
        { CurrentTenant = TenantId.Default
          UserId = userId
          AppUserInfo = AppUserInfo.NewEmpty(userId)
          LangPref = ShortLang.New "it" }

    static member NewWithUserInfo(userId: UserId, appUserInfo: AppUserInfo) =
        { CurrentTenant = TenantId.Default
          UserId = userId
          AppUserInfo = appUserInfo
          LangPref = ShortLang.New "it" }

    member this.SetCodiceFiscale(fiscalCode: FiscalCode) =
        { this with
            User.AppUserInfo.CodiceFiscale = fiscalCode.Value }
        |> Ok

    member this.GetCodiceFiscale() =
        match this.AppUserInfo.CodiceFiscale with
        | "" -> FiscalCode.NewEmpty() |> Ok
        | x when FiscalCode.IsValid x -> FiscalCode.New x
        | x -> FiscalCode.NewInvalid x |> Ok

    member this.SetPhoneNumber(phoneNumber: PhoneNumber) =
        { this with
            User.AppUserInfo.PhoneNumber = phoneNumber.Value }
        |> Ok

    member this.GetPhoneNumber() =
        match this.AppUserInfo.PhoneNumber with
        | "" -> PhoneNumber.NewEmpty() |> Ok
        | x when PhoneNumber.IsValid x -> PhoneNumber.New x
        | x -> PhoneNumber.NewInvalid x |> Ok

    member this.SetIsIdentifiedPhysically() =
        { this with
            User.AppUserInfo.IsIdentifiedPhysically = true }
        |> Ok

    member this.UnsetIdentifiedPhysically() =
        { this with
            User.AppUserInfo.IsIdentifiedPhysically = false }
        |> Ok

    member this.SetNome(nome: string) =
        { this with
            User.AppUserInfo.Nome = nome }
        |> Ok

    member this.SetCognome(cognome: string) =
        { this with
            User.AppUserInfo.Cognome = cognome }
        |> Ok

    member this.GetAppUserInfo() = this.AppUserInfo

    member this.SetCurrentTenant(tenantId: TenantId) =
        { this with CurrentTenant = tenantId } |> Ok

    member this.SetAppUserInfo(appUserInfo: AppUserInfo) =
        { this with AppUserInfo = appUserInfo } |> Ok

    member this.SetLangPref(langPref: ShortLang) = { this with LangPref = langPref } |> Ok

    // this was meant to replace the entire stream of events with GdprGhosted events (identity event)
    // but it is not needed anymore as anonymizing ApplicationUser is enough
    member this.GdprGhost() = this |> Ok

    member this.Id = this.UserId.Value
    static member StorageName = "_User"
    static member SnapshotsInterval = 100
    static member Version = "_01"
    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<User> |> Ok
        with ex ->
            Error ex.Message
