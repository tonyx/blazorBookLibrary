
namespace BookLibrary.Shared

open BookLibrary.Shared.Services

module SearchCriteria =
    let searchAllBooks = BookSearchCriteria(fun _ -> true)
    let searchImmediatelyAvailable = BookSearchCriteria(fun book -> book.ImmediatelyAvailable)
