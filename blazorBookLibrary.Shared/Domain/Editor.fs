
namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Editor = {
    EditorId: EditorId
    Name: Name
    Sealed: Sealed
} with 
    static member New (name: Name) (dateTime: DateTime)= 
        {   
            EditorId = EditorId.New(); 
            Name = name;
            Sealed = Sealed.New(dateTime)
        }
    member this.UpdateName (name: Name) (dateTime: DateTime)= 
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Editor is sealed"
                return { this with Name = name }
            }
    member this.Seal(dateTime: DateTime) =
        result
            {
                do! 
                    this.Sealed.IsSealed(dateTime)
                    |> not
                    |> Result.ofBool "Editor is sealed"
                return { this with Sealed = this.Sealed.Seal(dateTime) } 
            }
    member this.Unseal(dateTime: DateTime) =
        { 
            this 
                with 
                    Sealed = this.Sealed.Unseal(dateTime) 
        } 
        |> Ok
    member this.Id = this.EditorId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Editor"
    static member Version = "_01"
    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
    static member Deserialize (data: string) =
        try
            let editor = JsonSerializer.Deserialize<Editor> (data, jsonOptions)
            Ok editor
        with
            | ex -> Error ex.Message
