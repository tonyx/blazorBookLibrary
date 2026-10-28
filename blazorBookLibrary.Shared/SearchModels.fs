namespace BookLibrary.Shared
open BookLibrary.Domain
open BookLibrary.Shared.Commons

[<CLIMutable>]
type TenantSearchInfo =
    {
        TenantId: TenantId
        TenantName: string
        Address: string
        DistanceInMeters: Option<double>
        Latitude: Option<double>
        Longitude: Option<double>
        IsUserPatron: bool
        CanBorrowNow: bool
        CanReserveHold: bool
        RequiresMembership: bool
    }

[<CLIMutable>]
type BookSearchResult =
    {
        Book: Book
        Score: Option<double>
        Explanation: Option<string>
        TenantInfo: Option<TenantSearchInfo>
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

