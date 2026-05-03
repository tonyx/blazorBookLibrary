namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type TagEvent = 
    | TagAdded of Tag
    | TagRemoved of Tag
    | TagReplaced of oldTag:Tag * newTag:Tag

    interface Event<Tags> with
        member this.Process (tags: Tags) = 
            match this with
            | TagAdded tag -> 
                tags.AddTag tag
            | TagRemoved tag -> 
                tags.RemoveTag tag
            | TagReplaced (oldTag, newTag) -> 
                tags.ReplaceTag (oldTag, newTag)

    member this.Serialize =
        JsonSerializer.Serialize(this, jsonOptions)
    
    static member Deserialize(json: string) =
        try
            JsonSerializer.Deserialize<TagEvent>(json, jsonOptions) |> Ok
        with
            | ex -> ex.Message |> Error