namespace blazorBookLibrary.Shared

open System.Security.Claims
open System

module ConverterUtils =
    open BookLibrary.Shared.Commons
    
    let fromClaimsPrincipal (principal: ClaimsPrincipal) =
        if principal <> null && principal.Identity <> null && principal.Identity.IsAuthenticated then
            let userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            let userIdValue = 
                match userIdClaim with
                | null -> 
                    // Try alternative claim types common in some providers
                    match principal.FindFirst("sub") with
                    | null -> 
                        match principal.FindFirst(ClaimTypes.Name) with
                        | null -> Guid.Empty.ToString()
                        | c -> c.Value
                    | c -> c.Value
                | c -> c.Value
            
            let guid = 
                match Guid.TryParse(userIdValue) with
                | (true, g) -> g
                | _ -> Guid.Empty
            
            let roles = 
                principal.Claims 
                |> Seq.filter (fun c -> c.Type = ClaimTypes.Role || c.Type = "role" || c.Type = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") 
                |> Seq.choose (fun c -> 
                    match c.Value.ToLowerInvariant() with
                    | "admin" -> Some Admin
                    | "manager" -> Some Manager
                    | "user" -> Some User // user is not a role. verify
                    | _ -> None)
                |> Seq.toList
            UserContext.Authenticated(UserId(guid), roles)
        else
            UserContext.Anonymous

    let fromClaimsPrincipalAndTenant (principal: ClaimsPrincipal) (tenantId: TenantId) =
        if principal <> null && principal.Identity <> null && principal.Identity.IsAuthenticated then
            let userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            let userIdValue = 
                match userIdClaim with
                | null -> 
                    // Try alternative claim types common in some providers
                    match principal.FindFirst("sub") with
                    | null -> 
                        match principal.FindFirst(ClaimTypes.Name) with
                        | null -> Guid.Empty.ToString()
                        | c -> c.Value
                    | c -> c.Value
                | c -> c.Value
            
            let guid = 
                match Guid.TryParse(userIdValue) with
                | (true, g) -> g
                | _ -> Guid.Empty
            
            let roles = 
                principal.Claims 
                |> Seq.filter (fun c -> c.Type = ClaimTypes.Role || c.Type = "role" || c.Type = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") 
                |> Seq.choose (fun c -> 
                    match c.Value.ToLowerInvariant() with
                    | "admin" -> Some Admin
                    | "manager" -> Some Manager
                    | "user" -> Some User // user is not a role. verify
                    | _ -> None)
                |> Seq.toList
            let context = UserContext.Authenticated(UserId(guid), roles)
            context.WithNewTenant(tenantId)
        else
            UserContext.Anonymous
        

    let parseIsbns (input: string) =
        if String.IsNullOrWhiteSpace(input) then []
        else
            input.Split([|','; '\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim().Replace("-", "").Replace(" ", ""))
            |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))
            |> Array.choose (fun s -> 
                match Isbn.New s with
                | Ok isbn -> Some isbn
                | Error _ -> None
            )
            |> Array.distinct
            |> Array.toList