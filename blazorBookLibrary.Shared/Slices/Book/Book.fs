namespace BookLibrary.Domain

open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization

type BulkBookEdit = 
    {
        YearEdit: Option<Year>
        MainCategoryEdit: Option<Category>
        AdditionalCategoriesEdit: Option<List<Category>>
        AvailabilityEdit: Option<Availability>
        DistributionPointEdit: Option<DistributionPointId>
        AdditionalAuthorsEdit: Option<List<AuthorId>>
        AdditionalTagsEdit: Option<List<Tag>>
        RemoveTagsEdit: Option<List<Tag>>
    }
    with
        static member 
            Empty =
                { YearEdit = None; MainCategoryEdit = None; AdditionalCategoriesEdit = None; AvailabilityEdit = None; DistributionPointEdit = None; 
                AdditionalAuthorsEdit = None; AdditionalTagsEdit = None; RemoveTagsEdit = None }
        member 
            this.SetYearIfCondition (year, switch) =
                if switch then { this with YearEdit = Some year } else this
        member 
            this.SetMainCategoryIfCondition (category, switch) =
                if switch then { this with MainCategoryEdit = Some category } else this
        member 
            this.SetAdditionalCategoriesIfCondition (categories, switch) =
                if switch then { this with AdditionalCategoriesEdit = Some categories } else this
        member 
            this.SetAvailabilityIfCondition (availability, switch) =
                if switch then { this with AvailabilityEdit = Some availability } else this
        member 
            this.SetDistributionPointIfCondition (distributionPointId, switch) =
                if switch then { this with DistributionPointEdit = Some distributionPointId } else this
        member 
            this.SetAdditionalAuthorsIfCondition (authors, switch) =
                if switch then { this with AdditionalAuthorsEdit = Some authors} else this
        member 
            this.SetAdditionalTagsIfCondition (tags, switch) =
                if switch then { this with AdditionalTagsEdit = Some tags} else this
        member
            this.SetRemoveTagsIfCondition (tags, switch) =
                if switch then { this with RemoveTagsEdit = Some tags} else this

type Paper =
    { 
        PaperId: PaperId
        Title: Title
        Description: Option<string>
        OptionalEmbedding: Option<EmbeddingDataId>
        Authors: List<AuthorId>
        Categories: List<Category>
        Tags: List<Tag>
    }
    static member New(title: Title, description: Option<string>, authors: List<AuthorId>, categories: List<Category>, tags: List<Tag>) =
        {
            PaperId = PaperId.New()
            Title = title
            Description = description
            OptionalEmbedding = None
            Authors = authors
            Categories = categories
            Tags = tags
        }

type Book001 =
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
      SbnCode: Option<SbnCode>
      Sealed: Sealed }

    with
        member this.Upcast(): Book =
            {
                TenantId = this.TenantId
                BookId = this.BookId
                Title = this.Title
                Papers = []
                ImageUrl = this.ImageUrl
                Description = this.Description
                OptionalEmbedding = this.OptionalEmbedding
                Availability = this.Availability
                DistributionPoint = this.DistributionPoint
                Authors = this.Authors
                Translators = this.Translators
                Languages = this.Languages
                CurrentLoan = this.CurrentLoan
                Editor = this.Editor
                MainCategory = this.MainCategory
                AdditionalCategories = this.AdditionalCategories
                Tags = this.Tags
                Year = this.Year
                Isbn = this.Isbn
                SbnCode = None
                Sealed = this.Sealed
            }

and Book =
    { TenantId: TenantId
      BookId: BookId
      Title: Title
      Papers: List<Paper>
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
      SbnCode: Option<SbnCode>
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
          Papers = []
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
          SbnCode = None
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
    member this.HasDescription = this.Description.IsSome

    member this.AddTag (tag: Tag) (dateTime: DateTime) =
        result {
            do! tag.IsBookTag |> Result.ofBool $"Tag {tag} is not a book tag"
            return { this with Tags = this.Tags @ [ tag ] }
        }

    member this.RemoveTag (tag: Tag) (dateTime: DateTime) =
        result {
            return
                { this with
                    Tags = this.Tags |> List.filter (fun t -> t <> tag) }
        }
    
    member this.AddTags (tags: List<Tag>) (dateTime: DateTime) =
        result {
            return { this with Tags = this.Tags @ tags |> List.distinct }
        }

    member this.RemoveTags (tags: List<Tag>) (dateTime: DateTime) =
        result {
            return { this with Tags = this.Tags |> List.filter (fun t -> tags |> List.contains t |> not) }
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

    member this.HasEmbedding = this.OptionalEmbedding.IsSome

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

    member this.AddPaper (paper: Paper)  =
        result {
            return
                { this with
                    Papers = this.Papers @ [ paper ] }
        }

    member this.RemovePaper (paperId: PaperId)  =
        result {
            return
                { this with
                    Papers = this.Papers |> List.filter (fun x -> x.PaperId <> paperId) }
        }

    member this.AddPapers (papers: List<Paper>)  =
        result {
            return
                { this with
                    Papers = this.Papers @ papers |> List.distinct }
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

    member this.UnsetSbnCode () : Result<Book, string> =
        result
            {
                return
                    {
                        this with SbnCode = None
                    }

            }

    member this.UpdateSbnCode (sbnCode: SbnCode) : Result<Book, string> =
        result
            {
                
                return
                    {
                        this with SbnCode = Some sbnCode
                    }
            }

    member this.BulkUpdate (bulkBookEdit: BulkBookEdit) (dateTime: DateTime) =
        result {
            let adjustAddTags =
                match bulkBookEdit.AdditionalTagsEdit with
                | Some tags ->
                    { this with Tags = this.Tags @ tags |> List.distinct } 
                | _ -> this 

            let adjustYear =
                match bulkBookEdit.YearEdit with
                | Some year -> 
                    { adjustAddTags with Year = year }
                | _ -> adjustAddTags

            let adjustMainCategory =
                match bulkBookEdit.MainCategoryEdit with
                | Some mainCategory -> 
                    { adjustYear with MainCategory = mainCategory }
                | _ -> adjustYear

            let adjustAdditionalCategories =
                match bulkBookEdit.AdditionalCategoriesEdit with
                | Some additionalCategories -> 
                    { adjustMainCategory with AdditionalCategories = additionalCategories }
                | _ -> adjustMainCategory

            let adjustAvailability =
                match bulkBookEdit.AvailabilityEdit with
                | Some availability -> 
                    { adjustAdditionalCategories with Availability = availability }
                | _ -> adjustAdditionalCategories

            let adjustRemoveTags =
                match bulkBookEdit.RemoveTagsEdit with
                | Some tagsToRemove -> 
                    { adjustAvailability with Tags = adjustAvailability.Tags |> List.filter (fun x -> not (tagsToRemove |> List.contains x)) }
                | _ -> adjustAvailability

            let adjustDistributionPoint =
                match bulkBookEdit.DistributionPointEdit with
                | Some distributionPoint -> 
                    { adjustRemoveTags with DistributionPoint = Some distributionPoint }
                | _ -> adjustRemoveTags

            let adjustAdditionalAuthors =
                match bulkBookEdit.AdditionalAuthorsEdit with
                | Some additionalAuthors -> 
                    { adjustDistributionPoint with Authors = this.Authors @ additionalAuthors |> List.distinct }
                | _ -> adjustDistributionPoint

            return adjustAdditionalAuthors
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
    static member StorageName = "_book"
    static member Version = "_01"
    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            JsonSerializer.Deserialize<Book>(data, jsonOptions) |> Ok
        with ex ->
            try
                let book001 = JsonSerializer.Deserialize<Book001>(data, jsonOptions)
                Ok (book001.Upcast())
            with ex2 ->
                Error $"error deserializing {data}\n
                    ex1: {ex.Message}
                    ex2: {ex2.Message}"
