namespace BookLibrary.Domain

open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization

type Book =
    { TenantId: TenantId
      BookId: BookId
      Title: Title
      ImageUrl: Option<Uri>
      Description: Option<string>
      OptionalEmbedding: Option<EmbeddingDataId>
      Availability: Availability
      DistributionPoint: Option<DistributionPointId>

      Authors: List<AuthorId>
      Translators: List<AuthorId>
      Languages: List<CultureInfo>
      CurrentLoan: Option<LoanId>
      Editor: Option<EditorId>
      MainCategory: Category
      AdditionalCategories: List<Category>
      Tags: List<Tag>
      Year: Year
      Isbn: Isbn
      Sealed: Sealed }

    static member New
        (tenantId: TenantId)
        (title: Title)
        (authors: list<AuthorId>)
        (translators: list<AuthorId>)
        (languages: list<CultureInfo>)
        (editor: Option<EditorId>)
        (mainCategory: Category)
        (additionalCategories: list<Category>)
        (year: Year)
        (isbn: Isbn)
        (imageUrl: Option<Uri>)
        =
        { TenantId = tenantId
          BookId = BookId.New()
          Title = title
          Description = None
          OptionalEmbedding = None
          ImageUrl = imageUrl
          Availability = Availability.Circulating
          DistributionPoint = None
          Authors = authors
          Translators = translators
          Languages = languages
          CurrentLoan = None
          Editor = editor
          MainCategory = mainCategory
          AdditionalCategories = additionalCategories
          Tags = []
          Year = year
          Isbn = isbn
          Sealed = Sealed.New(DateTime.UtcNow) }

    static member NewWithAvailability
        (tenantId: TenantId)
        (title: Title)
        (authors: list<AuthorId>)
        (translators: list<AuthorId>)
        (languages: list<CultureInfo>)
        (editor: Option<EditorId>)
        (mainCategory: Category)
        (additionalCategories: list<Category>)
        (tags: list<Tag>)
        (year: Year)
        (isbn: Isbn)
        (imageUrl: Option<Uri>)
        (availability: Availability)
        =
        { Book.New
              tenantId
              title
              authors
              translators
              languages
              editor
              mainCategory
              additionalCategories
              year
              isbn
              imageUrl with
            Availability = availability
            Tags = tags }

    static member NewWithAvailabilityAndDistributionPoint
        (tenantId: TenantId)
        (title: Title)
        (authors: list<AuthorId>)
        (translators: list<AuthorId>)
        (languages: list<CultureInfo>)
        (editor: Option<EditorId>)
        (mainCategory: Category)
        (additionalCategories: list<Category>)
        (tags: list<Tag>)
        (year: Year)
        (isbn: Isbn)
        (imageUrl: Option<Uri>)
        (availability: Availability)
        (distributionPoint: DistributionPointId)
        =
        { Book.New
              tenantId
              title
              authors
              translators
              languages
              editor
              mainCategory
              additionalCategories
              year
              isbn
              imageUrl with
            Availability = availability
            Tags = tags
            DistributionPoint = Some distributionPoint }

    member this.UpdateTitle (title: Title) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with Title = title }
        }

    member this.UpdateDescription (description: string) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"

            return
                { this with
                    Description = Some description }
        }

    member this.RemoveDescription(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with Description = None }
        }

    member this.AddTag (tag: Tag) (dateTime: DateTime) =
        result {
            do! tag.IsBookTag |> Result.ofBool $"Tag {tag} is not a book tag"
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            do! this.Tags |> List.contains tag |> not |> Result.ofBool "Tag already in book"
            return { this with Tags = this.Tags @ [ tag ] }
        }

    member this.RemoveTag (tag: Tag) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            do! this.Tags |> List.contains tag |> Result.ofBool "Tag not found in book"

            return
                { this with
                    Tags = this.Tags |> List.filter (fun t -> t <> tag) }
        }

    member this.ClearTags(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with Tags = [] }
        }

    member this.SetDistributionPoint (distributionPoint: DistributionPointId) (user: UserId) (dateTime: DateTime) =
        result {
            return
                { this with
                    DistributionPoint = Some distributionPoint }
        }

    member this.UnsetDistributionPoint (user: UserId) (dateTime: DateTime) =
        result { return { this with DistributionPoint = None } }

    member this.EmbedDescription (embeddingId: EmbeddingDataId) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"

            return
                { this with
                    OptionalEmbedding = Some embeddingId }
        }

    member this.RemoveEmbedding(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with OptionalEmbedding = None }
        }

    member this.ForceRemoveEmbedding() =
        { this with OptionalEmbedding = None } |> Ok

    member this.SetAvailability (availability: Availability) (dateTime: DateTime) =
        result {
            return
                { this with
                    Availability = availability }
        }

    member this.UpdateAuthors (authors: List<AuthorId>) (dateTime: DateTime) =
        result { return { this with Authors = authors } }

    member this.AddAuthors (additionalAuthors: List<AuthorId>) (dateTime: DateTime) =
        result {
            return
                { this with
                    Authors = this.Authors @ additionalAuthors |> List.distinct }
        }

    member this.AddAuthor (author: AuthorId) (dateTime: DateTime) =
        result {
            do!
                this.Authors
                |> List.contains author
                |> not
                |> Result.ofBool "Author already in book"

            return
                { this with
                    Authors = this.Authors @ [ author ] }
        }

    member this.AddTranslator (translator: AuthorId) (dateTime: DateTime) =
        result {
            do!
                this.Translators
                |> List.contains translator
                |> not
                |> Result.ofBool "Translator already in book"

            return
                { this with
                    Translators = this.Translators @ [ translator ] }
        }

    member this.RemoveTranslator (translator: AuthorId) (dateTime: DateTime) =
        result {
            do!
                this.Translators
                |> List.contains translator
                |> Result.ofBool "Translator not in book"

            return
                { this with
                    Translators = this.Translators |> List.filter (fun x -> x <> translator) }
        }

    member this.AddLanguage (language: CultureInfo) (dateTime: DateTime) =
        result {
            do!
                this.Languages
                |> List.contains language
                |> not
                |> Result.ofBool "Language already in book"

            return
                { this with
                    Languages = this.Languages @ [ language ] }
        }

    member this.RemoveLanguage (language: CultureInfo) (dateTime: DateTime) =
        result {
            do! this.Languages |> List.contains language |> Result.ofBool "Language not in book"

            return
                { this with
                    Languages = this.Languages |> List.filter (fun x -> x <> language) }
        }

    member this.RemoveAuthor (author: AuthorId) (dateTime: DateTime) =
        result {
            do! this.Authors |> List.contains author |> Result.ofBool "Author not in book"

            return
                { this with
                    Authors = this.Authors |> List.filter (fun x -> x <> author) }
        }

    member this.SetImageUrl (imageUrl: Uri) (dateTime: DateTime) =
        result { return { this with ImageUrl = Some imageUrl } }

    member this.RemoveImageUrl(dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with ImageUrl = None }
        }

    member this.SetCurrentLoan (loanId: LoanId) (dateTime: DateTime) =
        result {
            do!
                this.CurrentLoan
                |> Option.isSome
                |> not
                |> Result.ofBool "Book is already on loan"

            return { this with CurrentLoan = Some loanId }
        }

    member this.SetCurrentLoanFromReservation (reservationId: ReservationId) (loanId: LoanId) (dateTime: DateTime) =
        result {
            do! this.CurrentLoan |> Option.isNone |> Result.ofBool "Book is already on loan"
            return { this with CurrentLoan = Some loanId }
        }

    member this.ReleaseLoan (loanId: LoanId) (dateTime: DateTime) =
        result {
            let! currentLoan = this.CurrentLoan |> Result.ofOption "Book is not on loan"
            do! currentLoan = loanId |> Result.ofBool "Book is not on the specified loan"
            return { this with CurrentLoan = None }
        }

    member this.ReturnFromLoan(dateTime: DateTime) =
        result {
            do! this.CurrentLoan |> Option.isSome |> Result.ofBool "Book is not on loan"
            return { this with CurrentLoan = None }
        }

    member this.UpdateEditor (editor: EditorId) (dateTime: DateTime) =
        result { return { this with Editor = Some editor } }

    member this.ChangeMainCategory (mainCategory: Category) (dateTime: DateTime) =
        result {
            do!
                this.AdditionalCategories
                |> List.contains mainCategory
                |> not
                |> Result.ofBool "Main category already in additional categories"

            return
                { this with
                    MainCategory = mainCategory }
        }

    member this.AddAdditionalCategory (category: Category) (dateTime: DateTime) =
        result {
            do!
                this.AdditionalCategories
                |> List.contains category
                |> not
                |> Result.ofBool "Category already in additional categories"

            do!
                this.MainCategory
                |> fun c -> c <> category
                |> Result.ofBool "Category already in additional categories"

            return
                { this with
                    AdditionalCategories = this.AdditionalCategories @ [ category ] }
        }

    member this.RemoveAdditionalCategory (category: Category) (dateTime: DateTime) =
        result {
            do!
                this.AdditionalCategories
                |> List.contains category
                |> Result.ofBool "Category not in additional categories"

            return
                { this with
                    AdditionalCategories = this.AdditionalCategories |> List.filter (fun x -> x <> category) }
        }

    member this.ReplaceAdditionalCategories (additionalCategories: List<Category>) (dateTime: DateTime) =
        result {
            return
                { this with
                    AdditionalCategories = additionalCategories }
        }

    member this.RemoveEditor(dateTime: DateTime) =
        result { return { this with Editor = None } }

    member this.UpdateYear (year: Year) (dateTime: DateTime) =
        result { return { this with Year = year } }

    member this.UpdateIsbn (isbn: Isbn) (dateTime: DateTime) =
        result {
            do! this.Sealed.IsSealed(dateTime) |> not |> Result.ofBool "Book is sealed"
            return { this with Isbn = isbn }
        }

    member this.Seal(dateTime: DateTime) =
        { this with
            Sealed = this.Sealed.Seal(dateTime) }
        |> Ok

    member this.Unseal(dateTime: DateTime) =
        { this with
            Sealed = this.Sealed.Unseal(dateTime) }
        |> Ok

    member this.Editable =
        not (this.Sealed.IsSealed(DateTime.UtcNow))
        && this.NoLoan
        && this.NoReservations

    member this.NoLoan = this.CurrentLoan |> Option.isNone

    member this.NoReservations = true

    member this.Available = this.CurrentLoan |> Option.isNone

    member this.ImmediatelyAvailable =
        this.Availability = Availability.Circulating && this.Available

    member this.AvailabilityStatus =
        if
            this.NoLoan
            && this.NoReservations
            && this.Availability = Availability.Circulating
        then
            Available
        else if this.NoLoan && this.Availability = Availability.Circulating then
            Reserved
        else if this.Availability = Availability.ReferenceOnly then
            Consultable
        else
            NotAvailable

    member this.Id = this.BookId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Book"
    static member Version = "_01"
    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            JsonSerializer.Deserialize<Book>(data, jsonOptions) |> Ok
        with ex ->
            Error ex.Message
