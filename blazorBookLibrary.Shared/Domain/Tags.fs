
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open Sharpino
open System

type Tag = 
    | BookTag of string
    | AuthorTag of string
    | GeneralTag of string
    | PersonTag of string
    | GenericTag of string
    member this.TagName = match this with | BookTag s | AuthorTag s | GeneralTag s | PersonTag s -> s | GenericTag s -> s

type Tags =
    {
        TagsId: TagsId
        Tags: list<Tag>
    }
    static member New = 
        { TagsId = TagsId.UniqueTagId; Tags = [] }
    member this.AddTag (tag: Tag) = 
        result
            {
                do! 
                    this.Tags
                    |> List.exists (fun t -> t = tag)
                    |> not
                    |> Result.ofBool $"Tag {tag} already exists in {this.TagsId}"
                
                return 
                    { this with Tags = this.Tags @ [tag] }
            }
    member this.RemoveTag (tag: Tag) = 
        result
            {
                do! 
                    this.Tags
                    |> List.exists (fun t -> t = tag)
                    |> Result.ofBool $"Tag {tag} does not exist in {this.TagsId}"
                
                return 
                    { this with Tags = this.Tags |> List.filter (fun t -> t <> tag) }
            }
    member this.ReplaceTag (oldTag: Tag, newTag: Tag) = 
        result
            {
                do!
                    this.Tags
                    |> List.exists (fun t -> t = oldTag)
                    |> Result.ofBool $"Tag {oldTag} does not exist in {this.TagsId}"

                let existingTagsExceptTheOneToBeReplaced =
                    this.Tags
                    |> List.filter (fun t -> t <> oldTag)

                do!
                    existingTagsExceptTheOneToBeReplaced
                    |> List.exists (fun t -> t = newTag)
                    |> not
                    |> Result.ofBool $"Tag {newTag} already exists in {this.TagsId}"
                
                return 
                    { this with Tags = this.Tags |> List.map (fun t -> if t = oldTag then newTag else t) }
            }

    member this.Id =
        this.TagsId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Tags"
    static member Version = "_01"

    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)

    static member Deserialize (json: string) = 
        try
            JsonSerializer.Deserialize<Tags>(json, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message

    
    