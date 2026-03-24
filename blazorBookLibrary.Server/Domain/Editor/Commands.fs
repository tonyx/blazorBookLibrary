
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type EditorCommand =
    | UpdateName of Name * DateTime
    | Seal of DateTime
    | Unseal of DateTime
    interface AggregateCommand<Editor, EditorEvent> with
        member this.Execute (editor: Editor) =
            match this with
            | UpdateName (name, dateTime) ->
                editor.UpdateName name dateTime
                |> Result.map (fun e -> (e, [NameUpdated(name, dateTime)]))
            | Seal dateTime ->
                editor.Seal dateTime
                |> Result.map (fun e -> (e, [EditorSealed(dateTime)]))
            | Unseal dateTime ->
                editor.Unseal dateTime
                |> Result.map (fun e -> (e, [EditorUnsealed(dateTime)]))

        member this.Undoer = None
