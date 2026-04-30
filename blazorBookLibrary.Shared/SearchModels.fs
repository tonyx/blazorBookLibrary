namespace BookLibrary.Shared
open BookLibrary.Domain

type BookSearchResult =
    {
        Book: Book
        Score: Option<double>
        Explanation: Option<string>
    }
