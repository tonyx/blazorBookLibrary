
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Commons
open System.Text.Json

type EditorEvent =
    | NameUpdated of Name * DateTime
    | EditorSealed of DateTime
    | EditorUnsealed of DateTime
    interface Event<Editor> with
        member this.Process (editor: Editor) =
            match this with
            | NameUpdated (name, dateTime) ->
                editor.UpdateName name dateTime
            | EditorSealed dateTime ->
                editor.Seal dateTime
            | EditorUnsealed dateTime ->
                editor.Unseal dateTime

    static member Deserialize (x: string): Result<EditorEvent, string> =
        try
            JsonSerializer.Deserialize<EditorEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)