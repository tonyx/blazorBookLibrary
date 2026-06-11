namespace BookLibrary.Domain

open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Author =
    { TenantId: TenantId
      AuthorId: AuthorId
      Name: Name
      Isni: Isni
      Bio: string
      ImageUri: Option<Uri>
      WikipediaUri: Option<Uri>
      Sealed: Sealed }

    static member New (tenantId: TenantId) (name: Name) (isni: Isni) =
        { TenantId = tenantId
          AuthorId = AuthorId.New()
          Name = name
          Isni = isni
          Bio = ""
          ImageUri = None
          WikipediaUri = None
          Sealed = Sealed.New(DateTime.UtcNow) }

    static member NewWithoutIsni (tenantId: TenantId) (name: Name) =
        { TenantId = tenantId
          AuthorId = AuthorId.New()
          Name = name
          Isni = Isni.EmptyIsni
          Bio = ""
          ImageUri = None
          WikipediaUri = None
          Sealed = Sealed.New(DateTime.UtcNow) }

    static member NewWithOptionalIsniAndImageUrl(tenantId: TenantId, name: Name, ?isni: Isni, ?imageUrl: Uri) =
        { TenantId = tenantId
          AuthorId = AuthorId.New()
          Name = name
          Isni = isni |> Option.defaultValue Isni.EmptyIsni
          Bio = ""
          ImageUri = imageUrl
          WikipediaUri = None
          Sealed = Sealed.New(DateTime.UtcNow) }

    static member NewWithOptionalIsniAndImageUrlAndBio
        (tenatId: TenantId, name: Name, ?isni: Isni, ?imageUrl: Uri, ?bio: string, ?wikipediaUri: Uri)
        =
        { TenantId = tenatId
          AuthorId = AuthorId.New()
          Name = name
          Isni = isni |> Option.defaultValue Isni.EmptyIsni
          Bio = bio |> Option.defaultValue ""
          ImageUri = imageUrl
          WikipediaUri = wikipediaUri
          Sealed = Sealed.New(DateTime.UtcNow) }

    member this.Rename (name: Name) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"
            return { this with Name = name }
        }

    member this.UpdateImageUrl (imageUrl: Uri) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"

            return
                { this with
                    ImageUri = imageUrl |> Some }
        }

    member this.RemoveImageUrl(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"
            return { this with ImageUri = None }
        }

    member this.UpdateIsni (isni: Isni) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"
            return { this with Isni = isni }
        }

    member this.UpdateBio (bio: string) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"
            return { this with Bio = bio }
        }

    member this.UpdateWikipediaUri (wikipediaUri: Uri) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"

            return
                { this with
                    WikipediaUri = wikipediaUri |> Some }
        }

    member this.Seal(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Author is sealed"

            return
                { this with
                    Sealed = this.Sealed.Seal(dateTime) }
        }

    member this.Unseal(dateTime: DateTime) =
        { this with
            Sealed = this.Sealed.Unseal(dateTime) }
        |> Ok

    member this.Editable = not (this.Sealed.IsSealed(DateTime.UtcNow))

    member this.Id = this.AuthorId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Author"
    static member Version = "_01"
    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            let author = JsonSerializer.Deserialize<Author>(data, jsonOptions)
            Ok author
        with ex ->
            Error ex.Message
