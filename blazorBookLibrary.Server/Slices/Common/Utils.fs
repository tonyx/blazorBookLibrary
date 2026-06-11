namespace BookLibrary.Utils

open System

module Utils =
    let getFallbackUrl () =
        let envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        if not (String.IsNullOrEmpty(envUrls)) then
            let urls = envUrls.Split(';')
            let httpsUrl = urls |> Array.tryFind (fun u -> u.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            let selectedUrl = 
                match httpsUrl with
                | Some u -> u
                | None -> urls.[0]
            selectedUrl
                .Replace("0.0.0.0", "localhost")
                .Replace("*", "localhost")
                .Replace("+", "localhost")
                .TrimEnd('/')
        else
            "https://localhost:7201"
