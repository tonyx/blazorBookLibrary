namespace BookLibrary.Utils

open System
open BookLibrary.Shared.Commons

module ConverterUtils =
    
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