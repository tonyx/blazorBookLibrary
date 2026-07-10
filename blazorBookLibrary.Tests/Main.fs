module boleroBookLibrary.Tests
open Expecto

[<EntryPoint>]
let main argv =
    let argv = 
        if argv |> Array.contains "--sequenced" || argv |> Array.contains "-s" then
            argv
        else
            Array.append [| "--sequenced" |] argv
    Tests.runTestsInAssemblyWithCLIArgs [ CLIArguments.Sequenced ] argv
