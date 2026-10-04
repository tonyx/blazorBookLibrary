namespace BookLibrary.Shared
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type BookSearchResult =
    {
        Book: Book
        Score: Option<double>
        Explanation: Option<string>
    }

type AutocompleteSuggestion =
    {
        BookId: System.Guid
        Title: string
        Authors: string
        Year: int option
        Isbn: string
        MainCategory: string
        ImageUrl: string option
        AvailabilityStatus: AvailabilityStatus
    }

