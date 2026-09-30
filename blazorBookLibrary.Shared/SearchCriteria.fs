
namespace BookLibrary.Shared

open System
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services

[<AutoOpen>]
module SearchCriteriaExtensions =
    type BookSearchCriteria with
        member this.And(other: BookSearchCriteria) : BookSearchCriteria =
            BookSearchCriteria(fun b -> this.Invoke(b) && other.Invoke(b))

        member this.Or(other: BookSearchCriteria) : BookSearchCriteria =
            BookSearchCriteria(fun b -> this.Invoke(b) || other.Invoke(b))

        member this.Not() : BookSearchCriteria =
            BookSearchCriteria(fun b -> not (this.Invoke(b)))

    let (&&&) (a: BookSearchCriteria) (b: BookSearchCriteria) =
        BookSearchCriteria(fun b' -> a.Invoke(b') && b.Invoke(b'))

    let (|||) (a: BookSearchCriteria) (b: BookSearchCriteria) =
        BookSearchCriteria(fun b' -> a.Invoke(b') || b.Invoke(b'))

module SearchCriteria =
    let searchAllBooks = BookSearchCriteria(fun _ -> true)
    let searchNoneBooks = BookSearchCriteria(fun _ -> false)
    let searchImmediatelyAvailable = BookSearchCriteria(fun book -> book.ImmediatelyAvailable)

    let searchCirculating = BookSearchCriteria(fun book -> book.Availability.IsCirculating)
    let searchReferenceOnly = BookSearchCriteria(fun book -> book.Availability.IsReferenceOnly)
    let searchUnspecifiedAvailability = BookSearchCriteria(fun book -> book.Availability.IsUnspecified)

    let searchByAvailability (availability: string) =
        match availability with
        | "ImmediatelyAvailable" -> searchImmediatelyAvailable
        | "Circulating" -> searchCirculating
        | "ReferenceOnly" -> searchReferenceOnly
        | "Unspecified" -> searchUnspecifiedAvailability
        | _ -> searchAllBooks

    let searchByTags (tags: seq<Tag>) =
        let tagList = tags |> Seq.toList
        if List.isEmpty tagList then
            searchAllBooks
        else
            BookSearchCriteria(fun book -> tagList |> List.exists (fun t -> book.Tags |> List.contains t))

    let searchByDistributionPoints (distributionPointIds: seq<DistributionPointId>) =
        let dpList = distributionPointIds |> Seq.toList
        if List.isEmpty dpList then
            searchAllBooks
        else
            BookSearchCriteria(fun book ->
                match book.DistributionPoint with
                | Some dp -> dpList |> List.exists (fun d -> d = dp)
                | None -> false)

    let searchBySbnCode (sbnCodeSubstring: string) =
        if String.IsNullOrWhiteSpace(sbnCodeSubstring) then
            searchAllBooks
        else
            let cleanTerm = sbnCodeSubstring.Trim()
            BookSearchCriteria(fun book ->
                match book.SbnCode with
                | Some sbn -> sbn.Value.Contains(cleanTerm, StringComparison.OrdinalIgnoreCase)
                | None -> false)

    let searchByExactSbnCode (sbnCode: string) =
        if String.IsNullOrWhiteSpace(sbnCode) then
            searchAllBooks
        else
            let cleanTerm = sbnCode.Trim()
            BookSearchCriteria(fun book ->
                match book.SbnCode with
                | Some sbn -> String.Equals(sbn.Value, cleanTerm, StringComparison.OrdinalIgnoreCase)
                | None -> false)

    let searchHasSbnCode =
        BookSearchCriteria(fun book -> Option.isSome book.SbnCode)

    let searchMissingSbnCode =
        BookSearchCriteria(fun book -> Option.isNone book.SbnCode)

    let combine (c1: BookSearchCriteria) (c2: BookSearchCriteria) : BookSearchCriteria =
        if isNull (box c1) then c2
        elif isNull (box c2) then c1
        else BookSearchCriteria(fun b -> c1.Invoke(b) && c2.Invoke(b))

    let orElse (c1: BookSearchCriteria) (c2: BookSearchCriteria) : BookSearchCriteria =
        if isNull (box c1) then c2
        elif isNull (box c2) then c1
        else BookSearchCriteria(fun b -> c1.Invoke(b) || c2.Invoke(b))

    let notCriteria (c: BookSearchCriteria) : BookSearchCriteria =
        if isNull (box c) then searchNoneBooks
        else BookSearchCriteria(fun b -> not (c.Invoke(b)))

    /// Combines multiple criteria using logical AND
    let composeAll (criterias: seq<BookSearchCriteria>) : BookSearchCriteria =
        let criteriaList = 
            if isNull (box criterias) then []
            else criterias |> Seq.filter (fun c -> not (isNull (box c))) |> Seq.toList
        match criteriaList with
        | [] -> searchAllBooks
        | [single] -> single
        | head :: tail ->
            BookSearchCriteria(fun book ->
                head.Invoke(book) && (tail |> List.forall (fun c -> c.Invoke(book))))

    /// Combines multiple criteria using logical OR
    let composeAny (criterias: seq<BookSearchCriteria>) : BookSearchCriteria =
        let criteriaList = 
            if isNull (box criterias) then []
            else criterias |> Seq.filter (fun c -> not (isNull (box c))) |> Seq.toList
        match criteriaList with
        | [] -> searchNoneBooks
        | [single] -> single
        | head :: tail ->
            BookSearchCriteria(fun book ->
                head.Invoke(book) || (tail |> List.exists (fun c -> c.Invoke(book))))
