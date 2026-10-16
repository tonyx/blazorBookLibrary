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
                    Authenticated(UserId guid, roles)
                | _ -> Anonymous
        else
            Anonymous

    let mapFromClaimsPrincipalAndTenant (principal: ClaimsPrincipal) (tenantId: TenantId) =
        mapFromClaimsPrincipal principal

    let mapFromRequest (request: Microsoft.AspNetCore.Http.HttpRequest) =
        let context = mapFromClaimsPrincipal request.HttpContext.User
        if request = null then
            context
        else
            let headerTenant =
                if request.Headers <> null && request.Headers.ContainsKey("X-Tenant-Id") then
                    let h = request.Headers.["X-Tenant-Id"].ToString()
                    match System.Guid.TryParse(h) with
                    | true, g -> Some (TenantId g)
                    | _ -> None
                else
                    None

            let cookieTenant =
                if headerTenant.IsNone && request.Cookies <> null then
                    let tryCookie name =
                        if request.Cookies.ContainsKey(name) then
                            let c = request.Cookies.[name]
                            match System.Guid.TryParse(c) with
                            | true, g -> Some (TenantId g)
                            | _ -> None
                        else
                            None
                    match tryCookie "selected_tenant_id" with
                    | Some tid -> Some tid
                    | None -> tryCookie "selected_tenant"
                else
                    None

            match headerTenant |> Option.orElse cookieTenant with
            | Some tid -> context.WithNewTenant(tid)
            | None -> context

