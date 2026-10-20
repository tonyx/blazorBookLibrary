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

    let private escapeXml (str: string) =
        if String.IsNullOrEmpty str then ""
        else
            str.Replace("&", "&amp;")
               .Replace("<", "&lt;")
               .Replace(">", "&gt;")
               .Replace("\"", "&quot;")
               .Replace("'", "&apos;")

    let private formatDublinCoreElements (bd: BookDetails) (publisher: string option) (indent: string) : string list =
        let lines = ResizeArray<string>()
        lines.Add(sprintf "%s<dc:title>%s</dc:title>" indent (escapeXml bd.Book.Title.Value))
        for a in bd.Authors do
            lines.Add(sprintf "%s<dc:creator>%s</dc:creator>" indent (escapeXml a.Name.Value))
        match publisher with
        | Some p when not (String.IsNullOrWhiteSpace p) ->
            lines.Add(sprintf "%s<dc:publisher>%s</dc:publisher>" indent (escapeXml p))
        | _ -> ()
        if bd.Book.Year.Value > 0 then
            lines.Add(sprintf "%s<dc:date>%d</dc:date>" indent bd.Book.Year.Value)
        if not (String.IsNullOrWhiteSpace bd.Book.Isbn.Value) then
            lines.Add(sprintf "%s<dc:identifier>urn:isbn:%s</dc:identifier>" indent (escapeXml bd.Book.Isbn.Value))
        match bd.Book.SbnCode with
        | Some s when not (String.IsNullOrWhiteSpace s.Value) ->
            lines.Add(sprintf "%s<dc:identifier>urn:sbn:%s</dc:identifier>" indent (escapeXml s.Value))
        | _ -> ()
        for l in bd.Book.Languages |> List.map (fun x -> x.TwoLetterISOLanguageName) |> List.distinct do
            lines.Add(sprintf "%s<dc:language>%s</dc:language>" indent (escapeXml l))
        let allKeywords =
            [ bd.Book.MainCategory.ToString() ]
            @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
            @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
            |> List.filter (not << String.IsNullOrWhiteSpace)
            |> List.distinct
        for k in allKeywords do
            lines.Add(sprintf "%s<dc:subject>%s</dc:subject>" indent (escapeXml k))
        match bd.Book.Description with
        | Some d when not (String.IsNullOrWhiteSpace d) ->
            lines.Add(sprintf "%s<dc:description>%s</dc:description>" indent (escapeXml d))
        | _ -> ()
        lines.Add(sprintf "%s<dc:type>Text</dc:type>" indent)
        lines |> Seq.toList

    let formatDublinCoreBook (bd: BookDetails) (publisher: string option) : string =
        let inner = formatDublinCoreElements bd publisher "  " |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n%s\n</metadata>" inner

    let formatDublinCoreBulk (items: (BookDetails * string option) list) : string =
        let records =
            items
            |> List.map (fun (bd, pub) ->
                let inner = formatDublinCoreElements bd pub "    " |> String.concat "\n"
                sprintf "  <metadata>\n%s\n  </metadata>" inner)
            |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<records xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n%s\n</records>" records

    let private mapMarcLanguage (langCode: string) =
        match langCode.ToLowerInvariant() with
        | "en" -> "eng"
        | "it" -> "ita"
        | "fr" -> "fre"
        | "de" -> "ger"
        | "es" -> "spa"
        | other when other.Length = 3 -> other
        | _ -> "und"

    let private formatMarcXmlElements (bd: BookDetails) (publisher: string option) (indent: string) : string list =
        let lines = ResizeArray<string>()
        lines.Add(sprintf "%s<leader>00000nam a2200000 a 4500</leader>" indent)
        let idVal = bd.Book.BookId.Value.ToString()
        lines.Add(sprintf "%s<controlfield tag=\"001\">%s</controlfield>" indent (escapeXml idVal))

        let yearStr =
            if bd.Book.Year.Value > 0 && bd.Book.Year.Value <= 9999 then
                sprintf "%04d" bd.Book.Year.Value
            else "    "
        let langStr =
            match bd.Book.Languages with
            | l :: _ -> mapMarcLanguage l.TwoLetterISOLanguageName
            | [] -> "und"
        let field008 = sprintf "240101s%s    xx                  %s d" yearStr langStr
        lines.Add(sprintf "%s<controlfield tag=\"008\">%s</controlfield>" indent (escapeXml field008))

        if not (String.IsNullOrWhiteSpace bd.Book.Isbn.Value) then
            lines.Add(sprintf "%s<datafield tag=\"020\" ind1=\" \" ind2=\" \">\n%s  <subfield code=\"a\">%s</subfield>\n%s</datafield>" indent indent (escapeXml bd.Book.Isbn.Value) indent)

        match bd.Book.SbnCode with
        | Some s when not (String.IsNullOrWhiteSpace s.Value) ->
            lines.Add(sprintf "%s<datafield tag=\"024\" ind1=\"7\" ind2=\" \">\n%s  <subfield code=\"a\">%s</subfield>\n%s  <subfield code=\"2\">sbn</subfield>\n%s</datafield>" indent indent (escapeXml s.Value) indent indent)
        | _ -> ()

        match bd.Authors with
        | primaryAuthor :: remainingAuthors ->
            lines.Add(sprintf "%s<datafield tag=\"100\" ind1=\"1\" ind2=\" \">\n%s  <subfield code=\"a\">%s</subfield>\n%s</datafield>" indent indent (escapeXml primaryAuthor.Name.Value) indent)
            for a in remainingAuthors do
                lines.Add(sprintf "%s<datafield tag=\"700\" ind1=\"1\" ind2=\" \">\n%s  <subfield code=\"a\">%s</subfield>\n%s</datafield>" indent indent (escapeXml a.Name.Value) indent)
        | [] -> ()

        lines.Add(sprintf "%s<datafield tag=\"245\" ind1=\"1\" ind2=\"0\">\n%s  <subfield code=\"a\">%s</subfield>\n%s</datafield>" indent indent (escapeXml bd.Book.Title.Value) indent)

        let pubSubfields = ResizeArray<string>()
        match publisher with
        | Some p when not (String.IsNullOrWhiteSpace p) ->
            pubSubfields.Add(sprintf "%s  <subfield code=\"b\">%s</subfield>" indent (escapeXml p))
        | _ -> ()
        if bd.Book.Year.Value > 0 then
            pubSubfields.Add(sprintf "%s  <subfield code=\"c\">%d</subfield>" indent bd.Book.Year.Value)
        if pubSubfields.Count > 0 then
            let sfStr = pubSubfields |> String.concat "\n"
            lines.Add(sprintf "%s<datafield tag=\"264\" ind1=\" \" ind2=\"1\">\n%s\n%s</datafield>" indent sfStr indent)

        match bd.Book.Description with
        | Some d when not (String.IsNullOrWhiteSpace d) ->
            lines.Add(sprintf "%s<datafield tag=\"520\" ind1=\" \" ind2=\" \">\n%s  <subfield code=\"a\">%s</subfield>\n%s</datafield>" indent indent (escapeXml d) indent)
        | _ -> ()

        let allKeywords =
            [ bd.Book.MainCategory.ToString() ]
            @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
            @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
            |> List.filter (not << String.IsNullOrWhiteSpace)
            |> List.distinct
        for k in allKeywords do
            lines.Add(sprintf "%s<datafield tag=\"650\" ind1=\" \" ind2=\"7\">\n%s  <subfield code=\"a\">%s</subfield>\n%s  <subfield code=\"2\">local</subfield>\n%s</datafield>" indent indent (escapeXml k) indent indent)

        lines |> Seq.toList

    let formatMarcXmlBook (bd: BookDetails) (publisher: string option) : string =
        let inner = formatMarcXmlElements bd publisher "  " |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<record xmlns=\"http://www.loc.gov/MARC21/slim\">\n%s\n</record>" inner

    let formatMarcXmlBulk (items: (BookDetails * string option) list) : string =
        let records =
            items
            |> List.map (fun (bd, pub) ->
                let inner = formatMarcXmlElements bd pub "    " |> String.concat "\n"
                sprintf "  <record>\n%s\n  </record>" inner)
            |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<collection xmlns=\"http://www.loc.gov/MARC21/slim\">\n%s\n</collection>" records

    let private formatModsElements (bd: BookDetails) (publisher: string option) (indent: string) : string list =
        let lines = ResizeArray<string>()
        lines.Add(sprintf "%s<titleInfo>\n%s  <title>%s</title>\n%s</titleInfo>" indent indent (escapeXml bd.Book.Title.Value) indent)

        for a in bd.Authors do
            lines.Add(sprintf "%s<name type=\"personal\">\n%s  <namePart>%s</namePart>\n%s  <role>\n%s    <roleTerm type=\"text\">author</roleTerm>\n%s  </role>\n%s</name>" indent indent (escapeXml a.Name.Value) indent indent indent indent)

        lines.Add(sprintf "%s<typeOfResource>text</typeOfResource>" indent)
        lines.Add(sprintf "%s<genre authority=\"marcgt\">book</genre>" indent)

        let originSubfields = ResizeArray<string>()
        match publisher with
        | Some p when not (String.IsNullOrWhiteSpace p) ->
            originSubfields.Add(sprintf "%s  <publisher>%s</publisher>" indent (escapeXml p))
        | _ -> ()
        if bd.Book.Year.Value > 0 then
            originSubfields.Add(sprintf "%s  <dateIssued keyDate=\"yes\">%d</dateIssued>" indent bd.Book.Year.Value)
        if originSubfields.Count > 0 then
            let origStr = originSubfields |> String.concat "\n"
            lines.Add(sprintf "%s<originInfo>\n%s\n%s</originInfo>" indent origStr indent)

        for l in bd.Book.Languages |> List.map (fun x -> x.TwoLetterISOLanguageName) |> List.distinct do
            let lang3 = mapMarcLanguage l
            lines.Add(sprintf "%s<language>\n%s  <languageTerm type=\"code\" authority=\"iso639-2b\">%s</languageTerm>\n%s</language>" indent indent (escapeXml lang3) indent)

        if not (String.IsNullOrWhiteSpace bd.Book.Isbn.Value) then
            lines.Add(sprintf "%s<identifier type=\"isbn\">%s</identifier>" indent (escapeXml bd.Book.Isbn.Value))
        match bd.Book.SbnCode with
        | Some s when not (String.IsNullOrWhiteSpace s.Value) ->
            lines.Add(sprintf "%s<identifier type=\"sbn\">%s</identifier>" indent (escapeXml s.Value))
        | _ -> ()

        match bd.Book.Description with
        | Some d when not (String.IsNullOrWhiteSpace d) ->
            lines.Add(sprintf "%s<abstract>%s</abstract>" indent (escapeXml d))
        | _ -> ()

        let allKeywords =
            [ bd.Book.MainCategory.ToString() ]
            @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
            @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
            |> List.filter (not << String.IsNullOrWhiteSpace)
            |> List.distinct
        for k in allKeywords do
            lines.Add(sprintf "%s<subject>\n%s  <topic>%s</topic>\n%s</subject>" indent indent (escapeXml k) indent)

        lines |> Seq.toList

    let formatModsBook (bd: BookDetails) (publisher: string option) : string =
        let inner = formatModsElements bd publisher "  " |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<mods xmlns=\"http://www.loc.gov/mods/v3\" version=\"3.7\">\n%s\n</mods>" inner

    let formatModsBulk (items: (BookDetails * string option) list) : string =
        let records =
            items
            |> List.map (fun (bd, pub) ->
                let inner = formatModsElements bd pub "    " |> String.concat "\n"
                sprintf "  <mods version=\"3.7\">\n%s\n  </mods>" inner)
            |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<modsCollection xmlns=\"http://www.loc.gov/mods/v3\">\n%s\n</modsCollection>" records

    let private formatOnixProduct (bd: BookDetails) (publisher: string option) (indent: string) : string list =
        let lines = ResizeArray<string>()
        lines.Add(sprintf "%s<Product>" indent)
        let sub = indent + "  "
        let sub2 = indent + "    "
        let sub3 = indent + "      "
        let sub4 = indent + "        "

        let idVal = bd.Book.BookId.Value.ToString()
        lines.Add(sprintf "%s<RecordReference>%s</RecordReference>" sub (escapeXml idVal))
        lines.Add(sprintf "%s<NotificationType>03</NotificationType>" sub)
        lines.Add(sprintf "%s<RecordSourceType>01</RecordSourceType>" sub)

        if not (String.IsNullOrWhiteSpace bd.Book.Isbn.Value) then
            lines.Add(sprintf "%s<ProductIdentifier>\n%s<ProductIDType>15</ProductIDType>\n%s<IDValue>%s</IDValue>\n%s</ProductIdentifier>" sub sub2 sub2 (escapeXml bd.Book.Isbn.Value) sub)

        match bd.Book.SbnCode with
        | Some s when not (String.IsNullOrWhiteSpace s.Value) ->
            lines.Add(sprintf "%s<ProductIdentifier>\n%s<ProductIDType>01</ProductIDType>\n%s<IDTypeName>SBN</IDTypeName>\n%s<IDValue>%s</IDValue>\n%s</ProductIdentifier>" sub sub2 sub2 sub2 (escapeXml s.Value) sub)
        | _ -> ()

        lines.Add(sprintf "%s<DescriptiveDetail>" sub)
        lines.Add(sprintf "%s<ProductComposition>00</ProductComposition>" sub2)
        lines.Add(sprintf "%s<ProductForm>00</ProductForm>" sub2)

        lines.Add(sprintf "%s<TitleDetail>\n%s<TitleType>01</TitleType>\n%s<TitleElement>\n%s<TitleElementLevel>01</TitleElementLevel>\n%s<TitleText>%s</TitleText>\n%s</TitleElement>\n%s</TitleDetail>" sub2 sub3 sub3 sub4 sub4 (escapeXml bd.Book.Title.Value) sub3 sub2)

        let mutable seqNum = 1
        for a in bd.Authors do
            lines.Add(sprintf "%s<Contributor>\n%s<SequenceNumber>%d</SequenceNumber>\n%s<ContributorRole>A01</ContributorRole>\n%s<PersonName>%s</PersonName>\n%s</Contributor>" sub2 sub3 seqNum sub3 sub3 (escapeXml a.Name.Value) sub2)
            seqNum <- seqNum + 1

        for l in bd.Book.Languages |> List.map (fun x -> x.TwoLetterISOLanguageName) |> List.distinct do
            let lang3 = mapMarcLanguage l
            lines.Add(sprintf "%s<Language>\n%s<LanguageRole>01</LanguageRole>\n%s<LanguageCode>%s</LanguageCode>\n%s</Language>" sub2 sub3 sub3 (escapeXml lang3) sub2)

        let allKeywords =
            [ bd.Book.MainCategory.ToString() ]
            @ (bd.Book.AdditionalCategories |> List.map (fun c -> c.ToString()))
            @ (bd.Book.Tags |> List.map (fun t -> t.TagName))
            |> List.filter (not << String.IsNullOrWhiteSpace)
            |> List.distinct
        for k in allKeywords do
            lines.Add(sprintf "%s<Subject>\n%s<SubjectSchemeIdentifier>20</SubjectSchemeIdentifier>\n%s<SubjectHeadingText>%s</SubjectHeadingText>\n%s</Subject>" sub2 sub3 sub3 (escapeXml k) sub2)

        lines.Add(sprintf "%s</DescriptiveDetail>" sub)

        match bd.Book.Description with
        | Some d when not (String.IsNullOrWhiteSpace d) ->
            lines.Add(sprintf "%s<CollateralDetail>\n%s<TextContent>\n%s<TextType>03</TextType>\n%s<ContentAudience>00</ContentAudience>\n%s<Text>%s</Text>\n%s</TextContent>\n%s</CollateralDetail>" sub sub2 sub3 sub3 sub3 (escapeXml d) sub2 sub)
        | _ -> ()

        let pubElements = ResizeArray<string>()
        match publisher with
        | Some p when not (String.IsNullOrWhiteSpace p) ->
            pubElements.Add(sprintf "%s<Publisher>\n%s<PublishingRole>01</PublishingRole>\n%s<PublisherName>%s</PublisherName>\n%s</Publisher>" sub2 sub3 sub3 (escapeXml p) sub2)
        | _ -> ()
        if bd.Book.Year.Value > 0 then
            pubElements.Add(sprintf "%s<PublishingDate>\n%s<PublishingDateRole>01</PublishingDateRole>\n%s<Date dateformat=\"05\">%d</Date>\n%s</PublishingDate>" sub2 sub3 sub3 bd.Book.Year.Value sub2)
        if pubElements.Count > 0 then
            let peStr = pubElements |> String.concat "\n"
            lines.Add(sprintf "%s<PublishingDetail>\n%s\n%s</PublishingDetail>" sub peStr sub)

        lines.Add(sprintf "%s</Product>" indent)
        lines |> Seq.toList

    let formatOnixBook (bd: BookDetails) (publisher: string option) : string =
        let productStr = formatOnixProduct bd publisher "  " |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ONIXMessage release=\"3.0\" xmlns=\"http://ns.editeur.org/onix/3.0/reference\">\n  <Header>\n    <Sender>\n      <SenderName>BiblioNet</SenderName>\n    </Sender>\n    <SentDateTime>20260101T000000Z</SentDateTime>\n  </Header>\n%s\n</ONIXMessage>" productStr

    let formatOnixBulk (items: (BookDetails * string option) list) : string =
        let products =
            items
            |> List.map (fun (bd, pub) ->
                formatOnixProduct bd pub "  " |> String.concat "\n")
            |> String.concat "\n"
        sprintf "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ONIXMessage release=\"3.0\" xmlns=\"http://ns.editeur.org/onix/3.0/reference\">\n  <Header>\n    <Sender>\n      <SenderName>BiblioNet</SenderName>\n    </Sender>\n    <SentDateTime>20260101T000000Z</SentDateTime>\n  </Header>\n%s\n</ONIXMessage>" products
