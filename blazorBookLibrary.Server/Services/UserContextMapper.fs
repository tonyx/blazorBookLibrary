
namespace BookLibrary.Services

open System.Security.Claims
open BookLibrary.Shared.Commons

module UserContextMapper =
    let mapFromClaimsPrincipal (principal: ClaimsPrincipal) =
        if principal <> null && principal.Identity <> null && principal.Identity.IsAuthenticated then
            let userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            if userIdClaim = null then 
                Anonymous
            else
                match System.Guid.TryParse(userIdClaim.Value) with
                | (true, guid) ->
                    let roles = 
                        principal.FindAll(ClaimTypes.Role)
                        |> Seq.choose (fun c -> 
                            match c.Value.ToLowerInvariant() with
                            | "admin" -> Some Admin
                            | "manager" -> Some Manager
                            | _ -> None)
                        |> Seq.toList
                    Authenticated(UserId guid, roles, TenantId.Default)
                | _ -> Anonymous
        else
            Anonymous
    let mapFromClaimsPrincipalAndTenant (principal: ClaimsPrincipal) (tenantId: TenantId) =
        if principal <> null && principal.Identity <> null && principal.Identity.IsAuthenticated then
            let userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            if userIdClaim = null then 
                Anonymous
            else
                match System.Guid.TryParse(userIdClaim.Value) with
                | (true, guid) ->
                    let roles = 
                        principal.FindAll(ClaimTypes.Role)
                        |> Seq.choose (fun c -> 
                            match c.Value.ToLowerInvariant() with
                            | "admin" -> Some Admin
                            | "manager" -> Some Manager
                            | _ -> None)
                        |> Seq.toList
                    Authenticated(UserId guid, roles, tenantId)
                | _ -> Anonymous
        else
            Anonymous

    let mapFromRequest (request: Microsoft.AspNetCore.Http.HttpRequest) =
        let context = mapFromClaimsPrincipal request.HttpContext.User
        match context with
        | Authenticated(userId, roles, _) ->
            let tenantHeader = request.Headers.["X-Tenant-Id"]
            let tenantCookie = request.Cookies.["selected_tenant"]
            
            let tenantIdValue = 
                if not (string tenantCookie |> System.String.IsNullOrEmpty) then Some tenantCookie
                elif tenantHeader.Count > 0 then Some tenantHeader.[0]
                else None

            match tenantIdValue with
            | Some v ->
                match System.Guid.TryParse(v) with
                | (true, guid) -> Authenticated(userId, roles, TenantId(guid))
                | _ -> context
            | None -> context
        | _ -> context

    open BookLibrary.Shared.Services
    open System.Threading.Tasks

    let enrichContextAsync (userService: IUserService) (context: UserContext) =
        task {
            match context with
            | Authenticated(userId, roles, _) ->
                let! userResult = userService.GetUserAsync(context, userId)
                match userResult with
                | Ok user -> return Authenticated(userId, roles, user.CurrentTenant)
                | Error _ -> return context
            | Anonymous -> return context
        }
