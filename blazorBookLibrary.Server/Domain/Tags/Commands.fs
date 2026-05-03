
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Globalization

type TagCommand = 
    | AddTag of Tag
    | RemoveTag of Tag
    | ReplaceTag of oldTag:Tag * newTag:Tag

    interface AggregateCommand<Tags, TagEvent> with
        member this.Execute (tags: Tags) = 
            match this with
                | AddTag tag ->
                    tags.AddTag tag
                    |> Result.map (fun x -> (x, [TagAdded tag]))
                | RemoveTag tag ->
                    tags.RemoveTag tag
                    |> Result.map (fun x -> (x, [TagRemoved tag]))
                | ReplaceTag (oldTag, newTag) ->
                    tags.ReplaceTag (oldTag, newTag)
                    |> Result.map (fun x -> (x, [TagReplaced (oldTag, newTag)]))

        member this.Undoer =
            None

