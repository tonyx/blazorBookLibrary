
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
open Microsoft.Extensions.DependencyInjection
open BookLibrary.Services.UserMapping
open Microsoft.Extensions.Logging
open BookLibrary.Utils

type UserTenantResolverService(
    eventStore: IEventStore<string>,
    messageSenders: MessageSenders,
    userViewerAsync: AggregateViewerAsync2<User>,
    cookieService: ICookieService
) =
    let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

    let isTenantPublic (tenantId: TenantId, ct: CancellationToken) =
        task {
            if tenantId = TenantId.Default then
                return Ok true
            else
                let! tenantRes = tenantViewerAsync (Some ct) tenantId.Value
                match tenantRes with
                | Ok (_, tenant) -> return Ok tenant.TenantVisibility.IsPublic
                | Error _ -> return Ok false
        }

    let userHasAccessToTenant (context: UserContext) (tenant: Tenant) =
        if tenant.TenantId = TenantId.Default || tenant.TenantVisibility.IsPublic then
            true
        elif not context.IsAuthenticated then
            false
        else
            let userId = context.UserId.Value
            context.IsInRole Role.Admin
            || tenant.OwnerId = userId
            || (tenant.Patrons |> List.exists (fun (p, _) -> p = userId))
            || (tenant.InvitedPatrons |> List.exists (fun (p, _) -> p = userId))

    new (eventStore: IEventStore<string>, messageSenders: MessageSenders, userViewerAsync: AggregateViewerAsync2<User>) =
        UserTenantResolverService(eventStore, messageSenders, userViewerAsync, null)

    new (secretsReader: SecretsReader, cookieService: ICookieService)
        =
            let connectionString = secretsReader.GetBookLibraryConnectionString ()
            let eventStore = PgStorage.PgEventStore connectionString
            let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
            UserTenantResolverService(eventStore, MessageSenders.NoSender, userViewerAsync, cookieService)

    member this.GetTenantForUser (context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! userOpt =
                task {
                    match context with
                    | UserContext.Authenticated(userId, _) ->
                        let! userRes = userViewerAsync (Some ctValue) userId.Value
                        match userRes with
                        | Ok (_, u) -> return Some u
                        | Error _ -> return None
                    | _ -> return None
                }

            let! resolvedTenantId =
                taskResult {
                    match context.TenantId with
                    | Some tenantId -> return tenantId
                    | None ->
                        if not (System.Object.ReferenceEquals(cookieService, null)) then
                            let! cookieVal = 
                                task {
                                    let! c1 = cookieService.GetCookieAsync("selected_tenant_id")
                                    match c1 with
                                    | Some _ -> return c1
                                    | None -> return! cookieService.GetCookieAsync("selected_tenant")
                                }
                            match cookieVal with
                            | Some guidStr ->
                                match System.Guid.TryParse(guidStr) with
                                | true, guid -> return TenantId guid
                                | _ ->
                                    match userOpt with
                                    | Some u -> return u.CurrentTenant
                                    | None -> return TenantId.Default
                            | None ->
                                match userOpt with
                                | Some u -> return u.CurrentTenant
                                | None -> return TenantId.Default
                        else
                            match userOpt with
                            | Some u -> return u.CurrentTenant
                            | None -> return TenantId.Default
                }

            if not context.IsAuthenticated then
                let! isPublic = isTenantPublic (resolvedTenantId, ctValue)
                if not isPublic then
                    return TenantId.Default
                else
                    return resolvedTenantId
            else
                let! tenantOpt =
                    task {
                        let! res = tenantViewerAsync (Some ctValue) resolvedTenantId.Value
                        match res with
                        | Ok (_, t) -> return Some t
                        | Error _ -> return None
                    }
                match tenantOpt with
                | Some tenant ->
                    let userId = context.UserId.Value
                    let hasAccess =
                        resolvedTenantId = TenantId.Default
                        || context.IsInRole Role.Admin
                        || tenant.TenantVisibility.IsPublic
                        || tenant.OwnerId = userId
                        || (tenant.Patrons |> List.exists (fun (u, _) -> u = userId))
                        || (tenant.InvitedPatrons |> List.exists (fun (u, _) -> u = userId))
                    if hasAccess then
                        return resolvedTenantId
                    else
                        match userOpt with
                        | Some u when u.CurrentTenant <> resolvedTenantId ->
                            let! userTenantOpt =
                                task {
                                    let! res = tenantViewerAsync (Some ctValue) u.CurrentTenant.Value
                                    match res with
                                    | Ok (_, t) -> return Some t
                                    | Error _ -> return None
                                }
                            match userTenantOpt with
                            | Some uTenant ->
                                let userHasAccess =
                                    u.CurrentTenant = TenantId.Default
                                    || context.IsInRole Role.Admin
                                    || uTenant.TenantVisibility.IsPublic
                                    || uTenant.OwnerId = userId
                                    || (uTenant.Patrons |> List.exists (fun (uId, _) -> uId = userId))
                                    || (uTenant.InvitedPatrons |> List.exists (fun (uId, _) -> uId = userId))
                                if userHasAccess then
                                    return u.CurrentTenant
                                else
                                    return TenantId.Default
                            | None -> return TenantId.Default
                        | _ -> return TenantId.Default
                | None ->
                    match userOpt with
                    | Some u when u.CurrentTenant <> resolvedTenantId -> return u.CurrentTenant
                    | _ -> return TenantId.Default
        }

    member this.ResolveSearchTenants (context: UserContext, scope: TenantScope, ?ct: CancellationToken) =
        taskResult {
            let ctValue = defaultArg ct CancellationToken.None
            let! activeTenantId = this.GetTenantForUser (context, ctValue)
            
            let! allTenantsWithId = 
                StateView.getAllFilteredAggregateStatesAsync<Tenant, TenantEvent, string> 
                    (fun _ -> true) eventStore (Some ctValue)
            let allTenants = allTenantsWithId |> List.map snd

            match scope with
            | TenantScope.ActiveTenantOnly ->
                let activeTenant = allTenants |> List.tryFind (fun t -> t.TenantId = activeTenantId)
                match activeTenant with
                | Some t when userHasAccessToTenant context t ->
                    return [ t ]
                | _ ->
                    return []

            | TenantScope.ExplicitTenants requestedIds ->
                let requestedSet = Set.ofList requestedIds
                let filtered =
                    allTenants
                    |> List.filter (fun t ->
                        requestedSet.Contains t.TenantId && userHasAccessToTenant context t
                    )
                return filtered

            | TenantScope.ProximityTenants (coords, includeActive) ->
                let isGuest = not context.IsAuthenticated
                let effectiveRadius =
                    if isGuest then
                        Math.Min(coords.RadiusInMeters, MaxGuestProximityRadiusMeters)
                    else
                        coords.RadiusInMeters
                let effectiveCoords = { coords with RadiusInMeters = effectiveRadius }

                let isWithinRadius (t: Tenant) =
                    match t.GeoLocSettings.OptCoordinates with
                    | Some tCoords -> effectiveCoords.IsWithin(tCoords)
                    | None -> false

                let filtered =
                    allTenants
                    |> List.filter (fun t ->
                        userHasAccessToTenant context t &&
                        (isWithinRadius t || (includeActive && t.TenantId = activeTenantId))
                    )
                return filtered

            | TenantScope.AllAccessibleTenants optCoords ->
                match optCoords with
                | None ->
                    if not context.IsAuthenticated then
                        let filtered =
                            allTenants
                            |> List.filter (fun t -> t.TenantId = activeTenantId && userHasAccessToTenant context t)
                        return filtered
                    else
                        let filtered =
                            allTenants
                            |> List.filter (fun t -> userHasAccessToTenant context t)
                        return filtered
                | Some coords ->
                    let isGuest = not context.IsAuthenticated
                    let effectiveRadius =
                        if isGuest then
                            Math.Min(coords.RadiusInMeters, MaxGuestProximityRadiusMeters)
                        else
                            coords.RadiusInMeters
                    let effectiveCoords = { coords with RadiusInMeters = effectiveRadius }

                    let isWithinRadius (t: Tenant) =
                        match t.GeoLocSettings.OptCoordinates with
                        | Some tCoords -> effectiveCoords.IsWithin(tCoords)
                        | None -> false

                    let filtered =
                        allTenants
                        |> List.filter (fun t ->
                            userHasAccessToTenant context t && isWithinRadius t
                        )
                    return filtered
        }

    interface IUserTenantResolverService with 
        member this.GetTenantForUserAsync (context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetTenantForUser (context, ct)

        member this.ResolveSearchTenantsAsync (context: UserContext, scope: TenantScope, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ResolveSearchTenants (context, scope, ct)


        
