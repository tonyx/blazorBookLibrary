namespace blazorBookLibrary.Shared

open System.Security.Claims
open System

module ConverterUtils =
    open BookLibrary.Shared.Commons
    
    let fromClaimsPrincipal (principal: ClaimsPrincipal) =
        if principal.Identity.IsAuthenticated then
            let userId = principal.FindFirst(ClaimTypes.NameIdentifier).Value
            let roles = 
                principal.Claims 
                |> Seq.filter (fun c -> c.Type = ClaimTypes.Role) 
                |> Seq.map (fun c -> c.Value) 
                |> Seq.toList
                |> List.map Role.FromString
            UserContext.Authenticated(UserId(Guid.Parse(userId)), roles)
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