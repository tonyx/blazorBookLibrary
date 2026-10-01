
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

    let searchByTitleWithPapers (title: string) (includePapers: bool) =
        if String.IsNullOrWhiteSpace(title) then
            searchAllBooks
        else
            let cleanTerm = title.Trim()
            BookSearchCriteria(fun book ->
                let bookMatch = 
                    not (String.IsNullOrWhiteSpace(book.Title.Value)) && 
                    book.Title.Value.Contains(cleanTerm, StringComparison.OrdinalIgnoreCase)
                let papers = if isNull (box book.Papers) then [] else book.Papers
                let papersMatch =
                    includePapers &&
                    (papers |> List.exists (fun p ->
                        not (String.IsNullOrWhiteSpace(p.Title.Value)) &&
                        p.Title.Value.Contains(cleanTerm, StringComparison.OrdinalIgnoreCase)))
                bookMatch || papersMatch)

    let searchByTitle (title: string) = searchByTitleWithPapers title false

    let searchByAuthorWithPapers (authorId: AuthorId) (includePapers: bool) =
        BookSearchCriteria(fun book ->
            let bookMatch = book.Authors |> List.contains authorId
            let papers = if isNull (box book.Papers) then [] else book.Papers
            let papersMatch = 
                includePapers && 
                (papers |> List.exists (fun p -> 
                    let pAuthors = if isNull (box p.Authors) then [] else p.Authors
                    pAuthors |> List.contains authorId))
            bookMatch || papersMatch)

    let searchByAuthor (authorId: AuthorId) = searchByAuthorWithPapers authorId false

    let searchByAuthorsWithPapers (authors: seq<AuthorId>) (includePapers: bool) =
        let authorList = authors |> Seq.toList
        if List.isEmpty authorList then
            searchAllBooks
        else
            BookSearchCriteria(fun book ->
                let bookMatch = authorList |> List.exists (fun a -> book.Authors |> List.contains a)
                let papers = if isNull (box book.Papers) then [] else book.Papers
                let papersMatch = 
                    includePapers && 
                    (papers |> List.exists (fun p -> 
                        let pAuthors = if isNull (box p.Authors) then [] else p.Authors
                        authorList |> List.exists (fun a -> pAuthors |> List.contains a)))
                bookMatch || papersMatch)

    let searchByAuthors (authors: seq<AuthorId>) = searchByAuthorsWithPapers authors false

    let searchByCategoryWithPapers (category: Category) (includePapers: bool) =
        BookSearchCriteria(fun book ->
            let bookMatch = book.MainCategory = category || (book.AdditionalCategories |> List.contains category)
            let papers = if isNull (box book.Papers) then [] else book.Papers
            let papersMatch = 
                includePapers && 
                (papers |> List.exists (fun p -> 
                    let pCats = if isNull (box p.Categories) then [] else p.Categories
                    pCats |> List.contains category))
            bookMatch || papersMatch)

    let searchByCategory (category: Category) = searchByCategoryWithPapers category false

    let searchByCategoriesWithPapers (categories: seq<Category>) (includePapers: bool) =
        let catList = categories |> Seq.toList
        if List.isEmpty catList then
            searchAllBooks
        else
            BookSearchCriteria(fun book ->
                let bookMatch = catList |> List.exists (fun c -> book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                let papers = if isNull (box book.Papers) then [] else book.Papers
                let papersMatch = 
                    includePapers && 
                    (papers |> List.exists (fun p -> 
                        let pCats = if isNull (box p.Categories) then [] else p.Categories
                        catList |> List.exists (fun c -> pCats |> List.contains c)))
                bookMatch || papersMatch)

    let searchByCategories (categories: seq<Category>) = searchByCategoriesWithPapers categories false

    let searchByTagsWithPapers (tags: seq<Tag>) (includePapers: bool) =
        let tagList = tags |> Seq.toList
        if List.isEmpty tagList then
            searchAllBooks
        else
            BookSearchCriteria(fun book ->
                let bookMatch = tagList |> List.exists (fun t -> book.Tags |> List.contains t)
                let papers = if isNull (box book.Papers) then [] else book.Papers
                let papersMatch = 
                    includePapers && 
                    (papers |> List.exists (fun p -> 
                        let pTags = if isNull (box p.Tags) then [] else p.Tags
                        tagList |> List.exists (fun t -> pTags |> List.contains t)))
                bookMatch || papersMatch)

    let searchByTags (tags: seq<Tag>) = searchByTagsWithPapers tags false

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
