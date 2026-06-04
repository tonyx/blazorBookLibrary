namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks

open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type BookSearchCriteria = delegate of Book -> bool

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

type IBookService =
    abstract member AddBookAsync : context:UserContext * book: Book * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddBooksAsync : context:UserContext * books: List<Book> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAuthorToBookAsync : context:UserContext * authorId: AuthorId * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAuthorFromBookAsync : context:UserContext * authorId: AuthorId * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveBookAsync : context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetBookAsync : context:UserContext * id: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Book, string>>
    abstract member GetBooksAsync : context:UserContext * bookIds: List<BookId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member RemoveImageUrlAsync: context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SetImageUrlAsync: context:UserContext * bookId: BookId * imageUrl: Uri * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SetAvailabilityAsync: context:UserContext * availability: Availability * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddTagToBookAsync: context:UserContext * tag: Tag * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveTagFromBookAsync: context:UserContext * tag: Tag * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member BulkEditAsync: context:UserContext * bookIds: List<BookId> * editCriteria: BulkBookEdit * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member GetAllAsync : context:UserContext * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Book list, string>>
    abstract member GetAllBooksOfTenantAsync : context:UserContext * tenantId: TenantId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAsync : context:UserContext * title: Title * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAsync : context:UserContext * isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SetDistributionPointAsync: context:UserContext * distributionPointId: DistributionPointId * bookId: BookId * userId: UserId *  [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UnSetDistributionPointAsync: context:UserContext * distributionPointId: DistributionPointId * bookId: BookId * userId: UserId *  [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UnsetAllBookRelatedToDPAsync: context:UserContext * distributionPointId: DistributionPointId * userId: UserId *  [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member MoveFromDpToAnotherDPAsync: context:UserContext * fromPoint: DistributionPointId * toPoint: DistributionPointId * userId: UserId *  [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member SearchByTitleAndIsbnAsync : context:UserContext * title: Title * isbn: Isbn * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member ChangeMainCategoryAsync : context:UserContext * category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AddAdditionalCategoryAsync : context:UserContext * category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveAdditionalCategoryAsync : context:UserContext * category: Category * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member UpdateTitleAsync: context:UserContext * title: Title * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UpdateDescriptionAsync: context:UserContext * description: string * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveDescriptionAsync: context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member EmbedDescriptionAsync: context:UserContext * bookId: BookId * EmbeddingDataId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveEmbeddingAsync: context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member ForceBulkRemoveEmbeddingsAsync : context:UserContext * bookIds: List<BookId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UpdateIsbnAsync: context:UserContext * isbn: Isbn * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member UnsealAsync : context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member SealAsync : context:UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>

    abstract member SearchByYearAsync: context:UserContext * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAndYearAsync: context:UserContext * title: Title * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByIsbnAndYearAsync: context:UserContext * isbn: Isbn * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndIsbnAndYearAsync: context:UserContext * title: Title * isbn: Isbn * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByCategoriesAsync: context:UserContext * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByIsbnOrTitleAsync: context:UserContext * isbn: Isbn * title: Title * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>

    abstract member SearchByTitleAndCategoriesAsync: context:UserContext * title: Title * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByYearAndCategoriesAsync: context:UserContext * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndYearAndCategoriesAsync: context:UserContext * title: Title * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorAsync: context:UserContext * authorId: AuthorId * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAsync: context:UserContext * authors: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAsync: context:UserContext * title: Title * authors: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAsync: context:UserContext * authors: List<AuthorId> * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAsync: context:UserContext * title: Title * authors: List<AuthorId> * year: YearSearch * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndCategoriesAsync: context:UserContext * authors: List<AuthorId> * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndCategoriesAsync: context:UserContext * title: Title * authors: List<AuthorId> * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByAuthorsAndYearAndCategoriesAsync: context:UserContext * authors: List<AuthorId> * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    abstract member SearchByTitleAndAuthorsAndYearAndCategoriesAsync: context:UserContext * title: Title * authors: List<AuthorId> * year: YearSearch * categories: List<Category> * [<Optional; DefaultParameterValue(null)>] ?criteria: BookSearchCriteria * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Book>, string>>
    
    abstract member LoanedByUserAtLeastOnceAsync : context:UserContext * bookId: BookId * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<bool, string>>

