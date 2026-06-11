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
        mapFromClaimsPrincipal request.HttpContext.User

