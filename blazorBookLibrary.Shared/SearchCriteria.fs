
namespace BookLibrary.Shared

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Shared.Services

module SearchCriteria =
    let searchAllBooks :BookSearchCriteria = (fun _ -> true)
    let sarchImmediatelyAvailable: BookSearchCriteria = fun book -> book.ImmediatelyAvailable
