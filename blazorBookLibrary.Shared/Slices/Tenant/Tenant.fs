namespace BookLibrary.Domain

open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open Sharpino
open System

type PatronRole =
    | Manager
    | User
    | Suspended of string

type Tenant =
    { OwnerId: UserId
      TenantId: TenantId
      InvitedPatrons: List<(UserId * PatronInvitationCode)>
      Patrons: List<UserId * PatronRole>
      Name: TenantName
      Address: string
      TenantState: TenantState
      ReservationNotificationPreference: NotificationPreference
      LoanNotificationPreference: NotificationPreference
      TenantVisibility: TenantVisibility
      Tags: List<Tag>
      CurrentJoinPin: Option<string>
      JoinRequests: List<UserId> 
      GeoLocSettings: GeoLocSettings
      GuestProximitySettings: GuestProximitySettings
      PartnerApiKeys: List<PartnerApiKey>
    }

    static member New (userId: UserId, tenantName: TenantName, address: string, ?pub: bool): Tenant =
        { OwnerId = userId
          TenantId = TenantId.New()
          InvitedPatrons = []
          Patrons = []
          Name = tenantName
          Address = address
          TenantState = TenantState.Active
          TenantVisibility =
            if pub |> Option.defaultValue false then
                TenantVisibility.RequestedPublic
            else
                TenantVisibility.Private
          ReservationNotificationPreference = NotificationPreference.InApp
          LoanNotificationPreference = NotificationPreference.InApp
          Tags = []
          CurrentJoinPin = None
          JoinRequests = [] 
          GeoLocSettings = GeoLocSettings.Disabled
          GuestProximitySettings = GuestProximitySettings.Disabled
          PartnerApiKeys = []
        }

    static member NewDefault (userId: UserId, tenantName: TenantName, address: string) =
        { Tenant.New (userId, tenantName, address, true) with
            TenantId = TenantId.Default
            TenantVisibility = TenantVisibility.Public }

    member this.GenerateJoinPin2(pin: string) =
        { this with CurrentJoinPin = Some pin } |> Ok

    member this.AddJoinRequest2(userId: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = userId)
                |> not
                |> Result.ofBool "User is already a patron of this library"

            do!
                this.JoinRequests
                |> List.contains userId
                |> not
                |> Result.ofBool "User has already requested to join this library"

            return
                { this with
                    JoinRequests = this.JoinRequests @ [ userId ] }
        }

    member this.ApproveJoinRequest2(userId: UserId) =
        result {
            do!
                this.JoinRequests
                |> List.contains userId
                |> Result.ofBool "Join request not found"

            return
                { this with
                    Patrons = this.Patrons @ [ (userId, PatronRole.User) ]
                    JoinRequests = this.JoinRequests |> List.filter (fun u -> u <> userId) }
        }

    member this.RejectJoinRequest2(userId: UserId) =
        result {
            do!
                this.JoinRequests
                |> List.contains userId
                |> Result.ofBool "Join request not found"

            return
                { this with
                    JoinRequests = this.JoinRequests |> List.filter (fun u -> u <> userId) }
        }

    member this.Deactivate =
        match this.TenantState with
        | Active ->
            { this with
                TenantState = TenantState.Deactivated }
            |> Ok
        | _ -> Error "Tenant is already deactivated"

    member this.AddTag(tag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = tag)
                |> not
                |> Result.ofBool "Tag already exists"

            return { this with Tags = this.Tags @ [ tag ] }
        }

    member this.RemoveTag(tag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = tag)
                |> Result.ofBool "Tag does not exist"

            return
                { this with
                    Tags = this.Tags |> List.filter (fun t -> t <> tag) }
        }

    member this.AddPatron(user: UserId, role: PatronRole) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> not
                |> Result.ofBool "User is already a patron"

            return
                { this with
                    Patrons = this.Patrons @ [ (user, role) ] }
        }

    member this.InvitePatron(user: UserId, invitationCode: PatronInvitationCode) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> not
                |> Result.ofBool "User is already a patron"

            return
                { this with
                    InvitedPatrons = this.InvitedPatrons @ [ (user, invitationCode) ] }
        }

    member this.RevokeInvitation(userId: UserId) =
        result {
            do!
                this.InvitedPatrons
                |> List.exists (fun (u, _) -> u = userId)
                |> Result.ofBool "User is not an invited patron"

            return
                { this with
                    InvitedPatrons = this.InvitedPatrons |> List.filter (fun (u, _) -> u <> userId) }
        }

    member this.ConvertInvitedPatronToPatron(invitationCode: PatronInvitationCode) =
        result {
            do!
                this.InvitedPatrons
                |> List.exists (fun (_, i) -> i = invitationCode)
                |> Result.ofBool "User is not an invited patron"

            let user =
                this.InvitedPatrons |> List.find (fun (_, i) -> i = invitationCode) |> fst

            return
                { this with
                    Patrons = this.Patrons @ [ (user, PatronRole.User) ]
                    InvitedPatrons = this.InvitedPatrons |> List.filter (fun (u, _) -> u <> user) }
        }

    member this.DemotePatron(user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.User) else (u, r)) 
                    PartnerApiKeys = this.PartnerApiKeys |> List.filter (fun k -> k.PartnerId <> user) 
                }
        }

    member this.PromotePatron (user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.Manager) else (u, r)) }
        }

    member this.SuspendPatron(user: UserId, reason: string) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) ->
                            if u = user then
                                (u, PatronRole.Suspended reason)
                            else
                                (u, r)) }
        }

    member this.ReAdmittPatron (user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            do!
                this.Patrons
                |> List.find (fun (u, _) -> u = user)
                |> fun (_, r) -> r.IsSuspended
                |> Result.ofBool "Must be suspended before readmission"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.User) else (u, r)) }
        }

    member this.RemovePatron (user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            do!
                this.Patrons
                |> List.find (fun (u, _) -> u = user)
                |> fun (_, r) -> r.IsSuspended
                |> Result.ofBool "Must be suspended before removal"

            return
                { this with
                    Patrons = this.Patrons |> List.filter (fun (u, _) -> u <> user) }
        }

    member this.IsManager (user: UserId) =
        this.Patrons |> List.exists (fun (u, r) -> u = user && r.IsManager)

    member this.IsOwner (user: UserId) =
        this.OwnerId = user

    member this.ReplaceTag(oldTag: Tag, newTag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = oldTag)
                |> Result.ofBool "Tag does not exist"

            return
                { this with
                    Tags = this.Tags |> List.map (fun t -> if t = oldTag then newTag else t) }
        }

    // this should be allowed only if the previous state is RequestedPublic but for backward events processing I should allow it anyway
    member this.SetPublic() =
        { this with
            TenantVisibility = TenantVisibility.Public }
        |> Ok

    member this.RequestPublic() =
        result {
            do!
                this.TenantVisibility.IsPrivate
                |> Result.ofBool "The library must be public or private before requesting public visibility"

            return
                { this with
                    TenantVisibility = TenantVisibility.RequestedPublic }
        }

    member this.DisableGeoLocSettings () =
        result
            {
                return
                    {
                        this with
                            GeoLocSettings = GeoLocSettings.Disabled
                            GuestProximitySettings = GuestProximitySettings.Disabled
                    }
            }

    member this.SetGeoLocSettings (geoLocSettings: GeoLocSettings) =
        result
            {
                return
                    {
                        this with
                            GeoLocSettings = geoLocSettings
                    }
            }

    member this.IsNear (userCoordinates: Coordinates) = 
        match this.GeoLocSettings with
        | GeoLocSettings.Disabled ->
            false
        | GeoLocSettings.Enabled tenantCoordinates ->
            userCoordinates.DistanceInMeters (tenantCoordinates) <= userCoordinates.RadiusInMeters

    /// Checks whether the coordinates are within the proximity radius configured by the library.
    /// If library geolocation is disabled, returns true (no proximity control).
    member this.IsInProximity (userCoordinates: Coordinates) =
        match this.GeoLocSettings with
        | GeoLocSettings.Disabled -> true
        | GeoLocSettings.Enabled tenantCoordinates ->
            tenantCoordinates.DistanceInMeters(userCoordinates) <= tenantCoordinates.RadiusInMeters

    member this.EnableGuestLoanProximity (meters: double) =
        result {
            do!
                this.GeoLocSettings.IsEnabled
                |> Result.ofBool "Must enable geo-location settings"
            return 
                {
                    this with
                        GuestProximitySettings = GuestProximitySettings.Enabled meters
                }
        }

    member this.DisableGuestLoanProximity () =
        result 
            {
                return
                    {
                        this with
                            GuestProximitySettings = GuestProximitySettings.Disabled
                    }
            }


    /// Validates proximity requirements for guest reservations.
    /// If library geolocation is disabled, no control is done (returns Ok ()).
    /// If library geolocation is enabled, guest coordinates must be provided and within proximity radius.
    /// Uses GuestProximitySettings radius if enabled; otherwise falls back to GeoLocSettings radius.
    member this.CheckGuestProximity (guestCoordinates: Option<Coordinates>) : Result<unit, string> =
        match this.GeoLocSettings with
        | GeoLocSettings.Disabled -> Ok ()
        | GeoLocSettings.Enabled tenantCoordinates ->
            let allowedRadius =
                match this.GuestProximitySettings with
                | GuestProximitySettings.Enabled r -> r
                | GuestProximitySettings.Disabled -> tenantCoordinates.RadiusInMeters

            match guestCoordinates with
            | None ->
                Error "Guest reservation requires device location verification when library geolocation is enabled"
            | Some coords ->
                let distance = tenantCoordinates.DistanceInMeters (coords)
                if distance <= allowedRadius then
                    Ok ()
                else
                    Error $"Guest reservation requires proximity to the library (distance: {int (Math.Round distance)}m, allowed: {int allowedRadius}m)"

    member this.SetPrivate() =
        { this with
            TenantVisibility = TenantVisibility.Private }
        |> Ok

    member this.SetReservationNotificationPreference (notificationPreference: NotificationPreference) =
        { this with
            ReservationNotificationPreference = notificationPreference }
        |> Ok

    member this.SetLoanNotificaitonPreference (notificationPreference: NotificationPreference) =
        { this with
            LoanNotificationPreference = notificationPreference }
        |> Ok

    member this.GetUserRole userId =
        this.Patrons |> List.tryFind (fun (u, _) -> u = userId) |> Option.map snd

    member this.AddPartnerKey (partnerApiKey: PartnerApiKey) =
        result {
            let userId = partnerApiKey.PartnerId
            do!
                this.PartnerApiKeys
                |> List.exists (fun k -> k.PartnerId = userId)
                |> not
                |> Result.ofBool $"Partner key already exists for user {userId.Value}"
            return
                { this with
                    PartnerApiKeys = this.PartnerApiKeys |> List.append [ partnerApiKey ] }
        }

    member this.RevokeCatalogKey (catalogApiKey: CatalogApiKey, now: DateTime) =
        result {
            do!
                this.PartnerApiKeys
                |> List.exists (fun k -> k.Key = catalogApiKey)
                |> Result.ofBool $"Partner key {catalogApiKey} does not exist"

            let updatedKeys =
                this.PartnerApiKeys
                |> List.map (fun pk ->
                    if pk.Key = catalogApiKey then { pk with RevokedAt = Some now } else pk)
            return
                { this with
                    PartnerApiKeys = updatedKeys }
        }

    member this.RemovePartnerKey (partnerApiKey: PartnerApiKey) =
        result {
            do!
                this.PartnerApiKeys
                |> List.exists (fun k -> k = partnerApiKey)
                |> Result.ofBool $"Partner key {partnerApiKey} does not exist"

            let updatedKeys =
                this.PartnerApiKeys
                |> List.filter (fun k -> k <> partnerApiKey)
            return
                { this with
                    PartnerApiKeys = updatedKeys }
        }

    member this.TryFindActivePartnerKey (key: CatalogApiKey, ?now: DateTime) : PartnerApiKey option =
        let currentTime = defaultArg now DateTime.UtcNow
        this.PartnerApiKeys
        |> List.tryFind (fun k ->
            k.Key = key
            && k.RevokedAt.IsNone
            && (k.ExpiresAt.IsNone || k.ExpiresAt.Value >= currentTime))

    member this.ValidateCatalogKey (key: CatalogApiKey, ?configuredKey: CatalogApiKey, ?now: DateTime) =
        result {
            match configuredKey with
            | Some ck when ck = key -> return! Ok ()
            | _ ->
                match this.TryFindActivePartnerKey(key, ?now = now) with
                | Some _ -> return! Ok ()
                | None ->
                    let keyOpt = this.PartnerApiKeys |> List.tryFind (fun k -> k.Key = key)
                    match keyOpt with
                    | None -> return! Error $"Catalog key {key.Value} does not exist"
                    | Some pk when pk.RevokedAt.IsSome -> return! Error $"Catalog key {key.Value} has been revoked"
                    | Some _ -> return! Error $"Catalog key {key.Value} has expired"
        }

    member this.HasManagementAccess(context: UserContext) =
        match context with
        | UserContext.Anonymous | UserContext.AnonymousTenant _ -> false
        | UserContext.Authenticated(userId, roles) ->
            roles |> List.contains Role.Admin
            || roles |> List.contains Role.Manager
            || this.OwnerId = userId
            || this.IsManager userId
            || (this.GetUserRole userId = Some PatronRole.Manager)

    member this.IsOrdinaryPatron(context: UserContext) =
        match context with
        | UserContext.Anonymous | UserContext.AnonymousTenant _ -> false
        | UserContext.Authenticated(userId, _) ->
            this.GetUserRole userId = Some PatronRole.User

    member this.Activate =
        match this.TenantState with
        | Deactivated ->
            { this with
                TenantState = TenantState.Active }
            |> Ok
        | _ -> Error "Tenant is already activated"

    member this.ScheduleForDeletion(date: DateTime) =
        match this.TenantState with
        | Deactivated ->
            { this with
                TenantState = TenantState.ScheduledForDeletion date }
            |> Ok
        | _ -> Error "Tenant must be deactivated to be scheduled for deletion"

    member this.Id = this.TenantId.Value
    static member SnapshotsInterval = 100
    static member StorageName = "_Tenant"
    static member Version = "_01"

    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize (data: string) =
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant> |> Ok
        with ex ->
            Error $"Error in deserializing {data} - {ex.Message}"

[<AutoOpen>]
module UserContextTenantExtensions =
    type UserContext with
        member this.IsManagerOrOwnerOf(tenant: Tenant) =
            tenant.HasManagementAccess(this)

                
