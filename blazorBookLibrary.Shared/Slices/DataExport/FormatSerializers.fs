namespace BookLibrary.Shared.Formats

open System
open System.Text.Json.Serialization
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type SchemaOrgEntity =
    {
        [<JsonPropertyName("@type")>]
        Type: string
        [<JsonPropertyName("name")>]
        Name: string
    }

type SchemaOrgOffer =
    {
        [<JsonPropertyName("@type")>]
        Type: string
        [<JsonPropertyName("availability")>]
        Availability: string
        [<JsonPropertyName("itemCondition")>]
        ItemCondition: string
    }

type SchemaOrgBook =
    {
        [<JsonPropertyName("@context")>]
        Context: string option
        [<JsonPropertyName("@type")>]
        Type: string
        [<JsonPropertyName("name")>]
        Name: string
        [<JsonPropertyName("isbn")>]
        Isbn: string option
        [<JsonPropertyName("author")>]
        Author: SchemaOrgEntity list
        [<JsonPropertyName("datePublished")>]
        DatePublished: string option
        [<JsonPropertyName("publisher")>]
        Publisher: SchemaOrgEntity option
        [<JsonPropertyName("inLanguage")>]
        InLanguage: string option
        [<JsonPropertyName("genre")>]
        Genre: string option
        [<JsonPropertyName("keywords")>]
        Keywords: string option
        [<JsonPropertyName("description")>]
        Description: string option
        [<JsonPropertyName("image")>]
        Image: string option
        [<JsonPropertyName("offers")>]
        Offers: SchemaOrgOffer option
    }

type SchemaOrgGraph =
    {
        [<JsonPropertyName("@context")>]
        Context: string
        [<JsonPropertyName("@graph")>]
        Graph: SchemaOrgBook list
    }

module FormatSerializers =

    let private escapeBibTeX (str: string) =
        if String.IsNullOrEmpty str then ""
        else
            str.Replace("\\", "\\\\")
               .Replace("{", "\\{")
               .Replace("}", "\\}")
               .Replace("%", "\\%")

    let private sanitizeKey (str: string) =
        let clean =
            str
            |> Seq.filter (fun c -> Char.IsLetterOrDigit c || c = '_' || c = '-')
            |> Seq.toArray
            |> String
        if String.IsNullOrWhiteSpace clean then "item" else clean.ToLowerInvariant()

    let generateCiteKey (bd: BookDetails) =
        let authorPart =
            match bd.Authors with
            | a :: _ ->
                let parts = a.Name.Value.Trim().Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length > 0 then parts.[parts.Length - 1] else "book"
            | [] -> "book"
        let titlePart =
            let words = bd.Book.Title.Value.Trim().Split([| ' '; ':'; '-'; ','; '.'; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            let meaningfulWords =
                words
                |> Array.filter (fun w ->
                    let lower = w.ToLowerInvariant()
                    lower <> "the" && lower <> "a" && lower <> "an" && lower <> "il" && lower <> "lo" && lower <> "la" && lower <> "i" && lower <> "gli" && lower <> "le" && lower <> "un" && lower <> "uno" && lower <> "una")
            if meaningfulWords.Length > 0 then meaningfulWords.[0]
            elif words.Length > 0 then words.[0]
            else "ref"
        let yearPart =
            if bd.Book.Year.Value > 0 then string bd.Book.Year.Value else ""
        let rawKey = sprintf "%s%s%s" (sanitizeKey authorPart) yearPart (sanitizeKey titlePart)
        if rawKey.Length > 30 then rawKey.Substring(0, 30) else rawKey

    let formatBibTexBook (bd: BookDetails) (publisher: string option) : string =
        let key = generateCiteKey bd
        let titleLine = sprintf "  title = {%s}," (escapeBibTeX bd.Book.Title.Value)
        let authorLine =
            if bd.Authors.IsEmpty then None
            else
                let authors =
                    bd.Authors
                    |> List.map (fun a -> escapeBibTeX a.Name.Value)
                    |> String.concat " and "
                Some (sprintf "  author = {%s}," authors)
        let yearLine =
            if bd.Book.Year.Value > 0 then
                Some (sprintf "  year = {%d}," bd.Book.Year.Value)
            else None
        let publisherLine =
            publisher |> Option.bind (fun p ->
                if String.IsNullOrWhiteSpace p then None
                else Some (sprintf "  publisher = {%s}," (escapeBibTeX p)))
        let isbnLine =
            if String.IsNullOrWhiteSpace bd.Book.Isbn.Value then None
            else Some (sprintf "  isbn = {%s}," (escapeBibTeX bd.Book.Isbn.Value))
        let languageLine =
            if bd.Book.Languages.IsEmpty then None
            else
                let langs =
                    bd.Book.Languages
                    |> List.map (fun l -> l.TwoLetterISOLanguageName)
                    |> List.distinct
                    |> String.concat ", "
                Some (sprintf "  language = {%s}," (escapeBibTeX langs))
        let keywordsLine =
            let allKeywords =
                [ bd.Book.MainCategory.ToString() ]
                @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
                @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
                |> List.filter (not << String.IsNullOrWhiteSpace)
                |> List.distinct
            if allKeywords.IsEmpty then None
            else
                Some (sprintf "  keywords = {%s}," (escapeBibTeX (allKeywords |> String.concat ", ")))
        let sbnLine =
            bd.Book.SbnCode |> Option.bind (fun s ->
                if String.IsNullOrWhiteSpace s.Value then None
                else Some (sprintf "  note = {SBN: %s}," (escapeBibTeX s.Value)))
        let fields =
            [
                Some titleLine
                authorLine
                yearLine
                publisherLine
                isbnLine
                languageLine
                keywordsLine
                sbnLine
            ]
            |> List.choose id
            |> String.concat "\n"
        sprintf "@book{%s,\n%s\n}" key fields

    let formatBibTexBulk (items: (BookDetails * string option) list) : string =
        items
        |> List.map (fun (bd, pub) -> formatBibTexBook bd pub)
        |> String.concat "\n\n"

    let mapAvailability (bd: BookDetails) =
        if bd.CurrentLoan.IsSome then
            "https://schema.org/OutOfStock"
        else
            match bd.Book.Availability with
            | Availability.Circulating
            | Availability.CirculatingIncludingGuest -> "https://schema.org/InStock"
            | Availability.ReferenceOnly -> "https://schema.org/InStoreOnly"
            | Availability.Unspecified -> "https://schema.org/InStock"

    let formatSchemaOrgBookDto (bd: BookDetails) (publisher: string option) (includeContext: bool) : SchemaOrgBook =
        let authors =
            bd.Authors
            |> List.map (fun a -> { Type = "Person"; Name = a.Name.Value })
        let pub =
            publisher
            |> Option.bind (fun p ->
                if String.IsNullOrWhiteSpace p then None
                else Some { Type = "Organization"; Name = p })
        let isbn =
            if String.IsNullOrWhiteSpace bd.Book.Isbn.Value then None
            else Some bd.Book.Isbn.Value
        let year =
            if bd.Book.Year.Value > 0 then Some (string bd.Book.Year.Value) else None
        let inLang =
            if bd.Book.Languages.IsEmpty then None
            else
                Some (bd.Book.Languages |> List.map (fun l -> l.TwoLetterISOLanguageName) |> List.distinct |> String.concat ", ")
        let keywords =
            let allKeywords =
                [ bd.Book.MainCategory.ToString() ]
                @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
                @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
                |> List.filter (not << String.IsNullOrWhiteSpace)
                |> List.distinct
            if allKeywords.IsEmpty then None else Some (allKeywords |> String.concat ", ")
        let offers =
            Some {
                Type = "Offer"
                Availability = mapAvailability bd
                ItemCondition = "https://schema.org/UsedCondition"
            }
        {
            Context = if includeContext then Some "https://schema.org" else None
            Type = "Book"
            Name = bd.Book.Title.Value
            Isbn = isbn
            Author = authors
            DatePublished = year
            Publisher = pub
            InLanguage = inLang
            Genre = Some (bd.Book.MainCategory.ToString())
            Keywords = keywords
            Description = bd.Book.Description
            Image = bd.Book.ImageUrl |> Option.map string
            Offers = offers
        }

    let private schemaJsonOptions =
        let opts = System.Text.Json.JsonSerializerOptions(WriteIndented = true)
        opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        opts

    let formatSchemaOrgBook (bd: BookDetails) (publisher: string option) : string =
        let dto = formatSchemaOrgBookDto bd publisher true
        System.Text.Json.JsonSerializer.Serialize(dto, schemaJsonOptions)

    let formatSchemaOrgGraph (items: (BookDetails * string option) list) : string =
        let books = items |> List.map (fun (bd, pub) -> formatSchemaOrgBookDto bd pub false)
        let graph = { Context = "https://schema.org"; Graph = books }
        System.Text.Json.JsonSerializer.Serialize(graph, schemaJsonOptions)
