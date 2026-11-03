namespace BookLibrary.Shared.Formats

open System
open System.Text
open System.Text.RegularExpressions
open System.Text.Json
open System.Xml.Linq
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type CatalogImportDraft =
    {
        Title: string
        Authors: string list
        Publisher: string option
        Year: int option
        Isbn: string option
        SbnCode: string option
        Categories: string list
        Tags: string list
        Description: string option
        ImageUrl: string option
        Language: string option
        RawFormat: ExportFormat
    }
    static member Empty(format: ExportFormat) =
        {
            Title = ""
            Authors = []
            Publisher = None
            Year = None
            Isbn = None
            SbnCode = None
            Categories = []
            Tags = []
            Description = None
            ImageUrl = None
            Language = None
            RawFormat = format
        }

type CatalogImportStatus =
    | Imported of bookId: Guid * title: string
    | SkippedDuplicate of isbn: string * title: string
    | Failed of title: string option * error: string
    | CatalogImportInterrupted of title: string option

type CatalogImportOptions =
    {
        PreventDuplicates: bool
        CreateMissingAuthors: bool
        EnrichAuthorsWithIsniAndPhoto: bool
        CreateMissingPublishers: bool
        GenerateMissingDescriptions: bool
        GenerateEmbeddings: bool
        EnrichMissingMetadataViaIsbn: bool
        MaxBatchSize: int option
    }
    static member Default =
        {
            PreventDuplicates = true
            CreateMissingAuthors = true
            EnrichAuthorsWithIsniAndPhoto = true
            CreateMissingPublishers = true
            GenerateMissingDescriptions = false
            GenerateEmbeddings = false
            EnrichMissingMetadataViaIsbn = false
            MaxBatchSize = Some 250
        }

type CatalogImportResult =
    {
        TotalProcessed: int
        ImportedCount: int
        SkippedCount: int
        FailedCount: int
        InterruptedCount: int
        Items: CatalogImportStatus list
    }
    static member Empty =
        {
            TotalProcessed = 0
            ImportedCount = 0
            SkippedCount = 0
            FailedCount = 0
            InterruptedCount = 0
            Items = []
        }

module FormatParsers =

    let private normalizeAuthorName (name: string) : string =
        if String.IsNullOrWhiteSpace name then ""
        else
            let trimmed = name.Trim().TrimEnd(',', '.', ';')
            // If formatted as "Last, First" (e.g. "Asimov, Isaac"), convert to "First Last"
            if trimmed.Contains(",") then
                let parts = trimmed.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
                if parts.Length = 2 then
                    $"{parts.[1].Trim()} {parts.[0].Trim()}"
                else trimmed
            else trimmed

    let private extract4DigitYear (str: string) : int option =
        if String.IsNullOrWhiteSpace str then None
        else
            let m = Regex.Match(str, @"\b(1[0-9]{3}|20[0-9]{2})\b")
            if m.Success then
                match Int32.TryParse(m.Value) with
                | true, y when y > 0 -> Some y
                | _ -> None
            else None

    // ==========================================
    // JSON Parser
    // ==========================================

    let private parseSingleJsonElement (elem: JsonElement) : Result<CatalogImportDraft, string> =
        try
            let getProp (names: string list) =
                names
                |> List.tryPick (fun n ->
                    match elem.TryGetProperty(n) with
                    | true, p when p.ValueKind <> JsonValueKind.Null && p.ValueKind <> JsonValueKind.Undefined -> Some p
                    | _ -> None)

            let title =
                match getProp [ "Title"; "title"; "name"; "Name" ] with
                | Some p -> p.GetString()
                | None -> ""

            let authors =
                match getProp [ "Authors"; "authors"; "author"; "Author" ] with
                    | Some p when p.ValueKind = JsonValueKind.Array ->
                        p.EnumerateArray()
                        |> Seq.map (fun a ->
                            if a.ValueKind = JsonValueKind.String then a.GetString()
                            elif a.ValueKind = JsonValueKind.Object then
                                match a.TryGetProperty("name") with
                                | true, np -> np.GetString()
                                | false, _ ->
                                    match a.TryGetProperty("Name") with
                                    | true, np -> np.GetString()
                                    | false, _ -> a.ToString()
                            else a.ToString())
                        |> Seq.map normalizeAuthorName
                        |> Seq.filter (not << String.IsNullOrWhiteSpace)
                        |> Seq.toList
                    | Some p when p.ValueKind = JsonValueKind.String ->
                        let s = p.GetString()
                        if String.IsNullOrWhiteSpace s then [] else [ normalizeAuthorName s ]
                    | _ -> []

            let publisher =
                match getProp [ "Publisher"; "publisher" ] with
                | Some p when p.ValueKind = JsonValueKind.String ->
                    let s = p.GetString()
                    if String.IsNullOrWhiteSpace s then None else Some (s.Trim())
                | Some p when p.ValueKind = JsonValueKind.Object ->
                    match p.TryGetProperty("name") with
                    | true, np -> Some (np.GetString().Trim())
                    | false, _ -> None
                | _ -> None

            let year =
                match getProp [ "Year"; "year"; "datePublished" ] with
                | Some p when p.ValueKind = JsonValueKind.Number ->
                    match p.TryGetInt32() with
                    | true, y when y > 0 -> Some y
                    | _ -> None
                | Some p when p.ValueKind = JsonValueKind.String ->
                    extract4DigitYear (p.GetString())
                | _ -> None

            let isbn =
                match getProp [ "Isbn"; "isbn" ] with
                | Some p when p.ValueKind = JsonValueKind.String ->
                    let s = p.GetString().Trim()
                    if String.IsNullOrWhiteSpace s then None else Some s
                | _ -> None

            let sbnCode =
                match getProp [ "SbnCode"; "sbnCode"; "sbn"; "Sbn" ] with
                | Some p when p.ValueKind = JsonValueKind.String ->
                    let s = p.GetString().Trim()
                    if String.IsNullOrWhiteSpace s then None else Some s
                | _ -> None

            let categories =
                let mainCat =
                    match getProp [ "MainCategory"; "mainCategory"; "genre"; "Genre" ] with
                    | Some p when p.ValueKind = JsonValueKind.String -> [ p.GetString().Trim() ]
                    | _ -> []
                let addCats =
                    match getProp [ "AdditionalCategories"; "categories"; "Categories" ] with
                    | Some p when p.ValueKind = JsonValueKind.Array ->
                        p.EnumerateArray()
                        |> Seq.filter (fun c -> c.ValueKind = JsonValueKind.String)
                        |> Seq.map (fun c -> c.GetString().Trim())
                        |> Seq.toList
                    | _ -> []
                mainCat @ addCats |> List.filter (not << String.IsNullOrWhiteSpace) |> List.distinct

            let tags =
                match getProp [ "Tags"; "tags"; "keywords"; "Keywords" ] with
                | Some p when p.ValueKind = JsonValueKind.Array ->
                    p.EnumerateArray()
                    |> Seq.map (fun t ->
                        if t.ValueKind = JsonValueKind.String then t.GetString().Trim()
                        elif t.ValueKind = JsonValueKind.Object then
                            match t.TryGetProperty("TagName") with
                            | true, tp -> tp.GetString().Trim()
                            | false, _ -> t.ToString()
                        else t.ToString())
                    |> Seq.filter (not << String.IsNullOrWhiteSpace)
                    |> Seq.toList
                | Some p when p.ValueKind = JsonValueKind.String ->
                    p.GetString().Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (not << String.IsNullOrWhiteSpace)
                    |> Array.toList
                | _ -> []

            let description =
                match getProp [ "Description"; "description" ] with
                | Some p when p.ValueKind = JsonValueKind.String ->
                    let s = p.GetString().Trim()
                    if String.IsNullOrWhiteSpace s then None else Some s
                | _ -> None

            let imageUrl =
                match getProp [ "ImageUrl"; "imageUrl"; "image"; "Image" ] with
                | Some p when p.ValueKind = JsonValueKind.String ->
                    let s = p.GetString().Trim()
                    if String.IsNullOrWhiteSpace s then None else Some s
                | _ -> None

            Ok {
                Title = title.Trim()
                Authors = authors
                Publisher = publisher
                Year = year
                Isbn = isbn
                SbnCode = sbnCode
                Categories = categories
                Tags = tags
                Description = description
                ImageUrl = imageUrl
                Language = None
                RawFormat = Json
            }
        with ex ->
            Error $"Error parsing JSON element: {ex.Message}"

    let parseJson (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty JSON payload"
        else
            try
                use doc = JsonDocument.Parse(payload)
                let root = doc.RootElement
                match root.ValueKind with
                | JsonValueKind.Array ->
                    let results =
                        root.EnumerateArray()
                        |> Seq.map parseSingleJsonElement
                        |> Seq.toList
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
                | JsonValueKind.Object ->
                    match parseSingleJsonElement root with
                    | Ok item -> Ok [ item ]
                    | Error err -> Error err
                | other ->
                    Error $"Expected JSON Object or Array, but found {other}"
            with ex ->
                Error $"Invalid JSON payload: {ex.Message}"

    // ==========================================
    // BibTeX Parser
    // ==========================================

    let private unescapeBibTeX (str: string) : string =
        if String.IsNullOrEmpty str then ""
        else
            str.Replace(@"\\", @"\")
               .Replace(@"\{", "{")
               .Replace(@"\}", "}")
               .Replace(@"\%", "%")
               .Replace(@"\&", "&")
               .Replace(@"\_", "_")
               .Replace(@"\#", "#")
               .Replace(@"\""", "\"")

    let private parseBibTeXFields (body: string) : Map<string, string> =
        let fields = ResizeArray<string * string>()
        let len = body.Length
        let mutable i = 0

        let skipWhitespaceAndCommas () =
            while i < len && (Char.IsWhiteSpace(body.[i]) || body.[i] = ',') do
                i <- i + 1

        while i < len do
            skipWhitespaceAndCommas ()
            if i < len then
                // Read field name
                let keyStart = i
                while i < len && (Char.IsLetterOrDigit(body.[i]) || body.[i] = '_' || body.[i] = '-') do
                    i <- i + 1
                let key = body.Substring(keyStart, i - keyStart).Trim().ToLowerInvariant()

                // Expect '='
                while i < len && Char.IsWhiteSpace(body.[i]) do
                    i <- i + 1
                if i < len && body.[i] = '=' then
                    i <- i + 1 // skip '='
                    while i < len && Char.IsWhiteSpace(body.[i]) do
                        i <- i + 1

                    if i < len then
                        // Determine value enclosure: '{', '"', or bare
                        let value =
                            if body.[i] = '{' then
                                i <- i + 1 // skip '{'
                                let valStart = i
                                let mutable braceDepth = 1
                                while i < len && braceDepth > 0 do
                                    if body.[i] = '{' && (i = 0 || body.[i - 1] <> '\\') then
                                        braceDepth <- braceDepth + 1
                                    elif body.[i] = '}' && (i = 0 || body.[i - 1] <> '\\') then
                                        braceDepth <- braceDepth - 1
                                    i <- i + 1
                                let valEnd = if braceDepth = 0 then i - 1 else i
                                body.Substring(valStart, valEnd - valStart)
                            elif body.[i] = '"' then
                                i <- i + 1 // skip '"'
                                let valStart = i
                                while i < len && (body.[i] <> '"' || (i > 0 && body.[i - 1] = '\\')) do
                                    i <- i + 1
                                let valEnd = i
                                if i < len then i <- i + 1 // skip closing '"'
                                body.Substring(valStart, valEnd - valStart)
                            else
                                // Bare word or number up to ',' or '}' or newline
                                let valStart = i
                                while i < len && body.[i] <> ',' && body.[i] <> '}' && body.[i] <> '\n' && body.[i] <> '\r' do
                                    i <- i + 1
                                body.Substring(valStart, i - valStart)

                        if not (String.IsNullOrWhiteSpace key) then
                            fields.Add(key, unescapeBibTeX (value.Trim()))
                else
                    // advance if malformed to prevent infinite loop
                    i <- i + 1

        fields |> Seq.map (fun (k, v) -> k, v) |> Map.ofSeq

    let parseBibTex (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty BibTeX payload"
        else
            try
                let drafts = ResizeArray<CatalogImportDraft>()
                let entryRegex = Regex(@"@([a-zA-Z]+)\s*[\{]", RegexOptions.Compiled)
                let matches = entryRegex.Matches(payload)

                for m in matches do
                    let entryType = m.Groups.[1].Value.ToLowerInvariant()
                    let openBracePos = m.Index + m.Length - 1
                    // Track braces to find end of entry
                    let mutable depth = 1
                    let mutable pos = openBracePos + 1
                    while pos < payload.Length && depth > 0 do
                        if payload.[pos] = '{' && (pos = 0 || payload.[pos - 1] <> '\\') then
                            depth <- depth + 1
                        elif payload.[pos] = '}' && (pos = 0 || payload.[pos - 1] <> '\\') then
                            depth <- depth - 1
                        pos <- pos + 1

                    let closeBracePos = if depth = 0 then pos - 1 else payload.Length - 1
                    let entryContent = payload.Substring(openBracePos + 1, closeBracePos - (openBracePos + 1))

                    // Separate citekey (up to first comma) and fields
                    let firstComma = entryContent.IndexOf(',')
                    let fieldsBody =
                        if firstComma >= 0 then
                            entryContent.Substring(firstComma + 1)
                        else
                            entryContent

                    let fields = parseBibTeXFields fieldsBody

                    let title =
                        fields |> Map.tryFind "title" |> Option.defaultValue ""

                    if not (String.IsNullOrWhiteSpace title) then
                        let authors =
                            match fields |> Map.tryFind "author" with
                            | Some aStr when not (String.IsNullOrWhiteSpace aStr) ->
                                Regex.Split(aStr, @"\s+and\s+", RegexOptions.IgnoreCase)
                                |> Array.map normalizeAuthorName
                                |> Array.filter (not << String.IsNullOrWhiteSpace)
                                |> Array.toList
                            | _ -> []

                        let year =
                            fields |> Map.tryFind "year" |> Option.bind extract4DigitYear

                        let publisher =
                            fields |> Map.tryFind "publisher" |> Option.filter (not << String.IsNullOrWhiteSpace)

                        let isbn =
                            fields |> Map.tryFind "isbn" |> Option.map (fun s -> s.Trim()) |> Option.filter (not << String.IsNullOrWhiteSpace)

                        let language =
                            fields |> Map.tryFind "language" |> Option.filter (not << String.IsNullOrWhiteSpace)

                        let keywords =
                            match fields |> Map.tryFind "keywords" with
                            | Some kStr when not (String.IsNullOrWhiteSpace kStr) ->
                                kStr.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries)
                                |> Array.map (fun s -> s.Trim())
                                |> Array.filter (not << String.IsNullOrWhiteSpace)
                                |> Array.toList
                            | _ -> []

                        let sbnCode =
                            match fields |> Map.tryFind "sbn" with
                            | Some s when not (String.IsNullOrWhiteSpace s) -> Some (s.Trim())
                            | _ ->
                                match fields |> Map.tryFind "note" with
                                | Some n when n.Contains("SBN:") ->
                                    let idx = n.IndexOf("SBN:") + 4
                                    let code = n.Substring(idx).Trim().TrimEnd('}')
                                    if String.IsNullOrWhiteSpace code then None else Some code
                                | _ -> None

                        let description =
                            match fields |> Map.tryFind "abstract" with
                            | Some a when not (String.IsNullOrWhiteSpace a) -> Some a
                            | _ -> fields |> Map.tryFind "description" |> Option.filter (not << String.IsNullOrWhiteSpace)

                        drafts.Add({
                            Title = title.Trim()
                            Authors = authors
                            Publisher = publisher
                            Year = year
                            Isbn = isbn
                            SbnCode = sbnCode
                            Categories = keywords
                            Tags = keywords
                            Description = description
                            ImageUrl = None
                            Language = language
                            RawFormat = BibTex
                        })

                if drafts.Count = 0 then
                    Error "No valid BibTeX entries found in payload"
                else
                    Ok (List.ofSeq drafts)
            with ex ->
                Error $"Invalid BibTeX payload: {ex.Message}"

    // ==========================================
    // MARCXML Parser
    // ==========================================

    let private parseSingleMarcRecord (recordEl: XElement) : Result<CatalogImportDraft, string> =
        try
            let datafields =
                recordEl.Elements()
                |> Seq.filter (fun e -> e.Name.LocalName = "datafield")
                |> Seq.toList

            let controlfields =
                recordEl.Elements()
                |> Seq.filter (fun e -> e.Name.LocalName = "controlfield")
                |> Seq.toList

            let getSubfields (tag: string) (code: string) =
                datafields
                |> List.filter (fun df ->
                    match df.Attribute(XName.Get("tag")) with
                    | null -> false
                    | a -> a.Value = tag)
                |> List.collect (fun df ->
                    df.Elements()
                    |> Seq.filter (fun sf ->
                        sf.Name.LocalName = "subfield" &&
                        match sf.Attribute(XName.Get("code")) with
                        | null -> false
                        | a -> a.Value = code)
                    |> Seq.map (fun sf -> sf.Value.Trim())
                    |> Seq.toList)

            let getFirstSubfield (tag: string) (code: string) =
                match getSubfields tag code with
                | head :: _ when not (String.IsNullOrWhiteSpace head) -> Some head
                | _ -> None

            // 245 $a (Title) and optional $b (Subtitle)
            let title =
                match getFirstSubfield "245" "a" with
                | Some t ->
                    let cleanT = t.TrimEnd('/', ':', ' ', ',')
                    match getFirstSubfield "245" "b" with
                    | Some sub when not (String.IsNullOrWhiteSpace sub) ->
                        let cleanSub = sub.TrimEnd('/', ':', ' ', ',')
                        $"{cleanT}: {cleanSub}"
                    | _ -> cleanT
                | None -> ""

            if String.IsNullOrWhiteSpace title then
                Error "Missing or empty 245$a Title in MARCXML record"
            else
                // 100 $a (Primary author) and 700 $a (Additional contributors)
                let primaryAuthor = getSubfields "100" "a"
                let additionalAuthors = getSubfields "700" "a"
                let authors =
                    primaryAuthor @ additionalAuthors
                    |> List.map normalizeAuthorName
                    |> List.filter (not << String.IsNullOrWhiteSpace)
                    |> List.distinct

                // 260 / 264 $b (Publisher)
                let publisher =
                    match getFirstSubfield "264" "b" with
                    | Some p -> Some (p.TrimEnd(',', ';', ' '))
                    | None ->
                        match getFirstSubfield "260" "b" with
                        | Some p -> Some (p.TrimEnd(',', ';', ' '))
                        | None -> None

                // Year: from 264$c, 260$c, or 008 pos 7-10
                let year =
                    let pubYear =
                        match getFirstSubfield "264" "c" with
                        | Some y -> extract4DigitYear y
                        | None ->
                            match getFirstSubfield "260" "c" with
                            | Some y -> extract4DigitYear y
                            | None -> None
                    match pubYear with
                    | Some y -> Some y
                    | None ->
                        // Fallback: check 008
                        controlfields
                        |> List.tryFind (fun cf ->
                            match cf.Attribute(XName.Get("tag")) with
                            | null -> false
                            | a -> a.Value = "008")
                        |> Option.bind (fun cf ->
                            let v = cf.Value
                            if v.Length >= 11 then
                                extract4DigitYear (v.Substring(7, 4))
                            else None)

                // 020 $a (ISBN)
                let isbn =
                    match getFirstSubfield "020" "a" with
                    | Some rawIsbn ->
                        // Strip qualifiers like (pbk.) or (hardcover)
                        let clean = rawIsbn.Split([| ' '; '(' |], StringSplitOptions.RemoveEmptyEntries).[0].Trim()
                        if String.IsNullOrWhiteSpace clean then None else Some clean
                    | None -> None

                // 024 $a with $2 = sbn
                let sbnCode =
                    datafields
                    |> List.filter (fun df ->
                        match df.Attribute(XName.Get("tag")) with
                        | null -> false
                        | a -> a.Value = "024")
                    |> List.tryPick (fun df ->
                        let sfs = df.Elements() |> Seq.filter (fun e -> e.Name.LocalName = "subfield") |> Seq.toList
                        let isSbn =
                            sfs
                            |> List.exists (fun sf ->
                                match sf.Attribute(XName.Get("code")) with
                                | null -> false
                                | a -> a.Value = "2" && sf.Value.Trim().ToLowerInvariant() = "sbn")
                        if isSbn then
                            sfs
                            |> List.tryPick (fun sf ->
                                match sf.Attribute(XName.Get("code")) with
                                | null -> None
                                | a -> if a.Value = "a" then Some (sf.Value.Trim()) else None)
                        else None)

                // 650 $a (Subject / Categories)
                let categories =
                    getSubfields "650" "a"
                    |> List.map (fun s -> s.TrimEnd('.'))
                    |> List.filter (not << String.IsNullOrWhiteSpace)
                    |> List.distinct

                // 520 $a (Description)
                let description = getFirstSubfield "520" "a"

                // Language from 008 pos 35-37 or 041 $a
                let language =
                    match getFirstSubfield "041" "a" with
                    | Some lang -> Some lang
                    | None ->
                        controlfields
                        |> List.tryFind (fun cf ->
                            match cf.Attribute(XName.Get("tag")) with
                            | null -> false
                            | a -> a.Value = "008")
                        |> Option.bind (fun cf ->
                            let v = cf.Value
                            if v.Length >= 38 then
                                let langCode = v.Substring(35, 3).Trim()
                                if langCode = "und" || String.IsNullOrWhiteSpace langCode then None
                                else Some langCode
                            else None)

                Ok {
                    Title = title
                    Authors = authors
                    Publisher = publisher
                    Year = year
                    Isbn = isbn
                    SbnCode = sbnCode
                    Categories = categories
                    Tags = categories
                    Description = description
                    ImageUrl = None
                    Language = language
                    RawFormat = MarcXml
                }
        with ex ->
            Error $"Error parsing MARCXML record: {ex.Message}"

    let parseMarcXml (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty MARCXML payload"
        else
            try
                let doc = XDocument.Parse(payload)
                let records =
                    doc.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName = "record")
                    |> Seq.toList

                if records.IsEmpty then
                    Error "No <record> elements found in MARCXML payload"
                else
                    let results = records |> List.map parseSingleMarcRecord
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
            with ex ->
                Error $"Invalid MARCXML XML payload: {ex.Message}"

    // ==========================================
    // CSV Parser
    // ==========================================

    let private parseCsvLine (line: string) : string list =
        let fields = ResizeArray<string>()
        let sb = StringBuilder()
        let mutable inQuotes = false
        let mutable i = 0
        let len = line.Length
        while i < len do
            let c = line.[i]
            if c = '"' then
                if inQuotes && i + 1 < len && line.[i + 1] = '"' then
                    sb.Append('"') |> ignore
                    i <- i + 1
                else
                    inQuotes <- not inQuotes
            elif c = ',' && not inQuotes then
                fields.Add(sb.ToString().Trim())
                sb.Clear() |> ignore
            else
                sb.Append(c) |> ignore
            i <- i + 1
        fields.Add(sb.ToString().Trim())
        fields |> Seq.toList

    let parseCsv (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty CSV payload"
        else
            try
                let lines =
                    payload.Split([| "\r\n"; "\n"; "\r" |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace s) && not (s.StartsWith("#")))
                    |> Array.toList

                if lines.IsEmpty then
                    Error "CSV payload contains no data lines"
                else
                    let headerLine = lines.Head
                    let headerCols = parseCsvLine headerLine |> List.map (fun s -> s.ToLowerInvariant())

                    let hasHeader =
                        headerCols |> List.exists (fun col -> col = "title" || col = "isbn" || col = "id")

                    let (headers, dataLines) =
                        if hasHeader then
                            (headerCols, lines.Tail)
                        else
                            ([ "id"; "title"; "isbn"; "year"; "availability"; "maincategory" ], lines)

                    let titleIdx = headers |> List.tryFindIndex (fun h -> h = "title")
                    let isbnIdx = headers |> List.tryFindIndex (fun h -> h = "isbn")
                    let yearIdx = headers |> List.tryFindIndex (fun h -> h = "year")
                    let categoryIdx = headers |> List.tryFindIndex (fun h -> h = "maincategory" || h = "category" || h = "categories")
                    let authorsIdx = headers |> List.tryFindIndex (fun h -> h = "author" || h = "authors")
                    let publisherIdx = headers |> List.tryFindIndex (fun h -> h = "publisher" || h = "editor")
                    let descIdx = headers |> List.tryFindIndex (fun h -> h = "description")

                    let getField (cols: string list) (idxOpt: int option) =
                        match idxOpt with
                        | Some idx when idx < cols.Length && not (String.IsNullOrWhiteSpace cols.[idx]) ->
                            Some (cols.[idx].Trim('"', ' '))
                        | _ -> None

                    let drafts = ResizeArray<CatalogImportDraft>()
                    for line in dataLines do
                        let cols = parseCsvLine line
                        if cols.Length > 0 then
                            let titleOpt = getField cols titleIdx
                            let isbnOpt = getField cols isbnIdx
                            let yearOpt = getField cols yearIdx |> Option.bind extract4DigitYear
                            let publisherOpt = getField cols publisherIdx
                            let descOpt = getField cols descIdx
                            let authors =
                                match getField cols authorsIdx with
                                | Some a ->
                                    a.Split([| ';'; '|' |], StringSplitOptions.RemoveEmptyEntries)
                                    |> Array.map normalizeAuthorName
                                    |> Array.filter (not << String.IsNullOrWhiteSpace)
                                    |> Array.toList
                                | None -> []
                            let categories =
                                match getField cols categoryIdx with
                                | Some c when not (String.IsNullOrWhiteSpace c) -> [ c ]
                                | _ -> []

                            if titleOpt.IsSome || isbnOpt.IsSome then
                                let resolvedTitle =
                                    match titleOpt with
                                    | Some t when not (String.IsNullOrWhiteSpace t) -> t
                                    | _ -> isbnOpt |> Option.defaultValue "Untitled"

                                drafts.Add({
                                    Title = resolvedTitle
                                    Authors = authors
                                    Publisher = publisherOpt
                                    Year = yearOpt
                                    Isbn = isbnOpt
                                    SbnCode = None
                                    Categories = categories
                                    Tags = categories
                                    Description = descOpt
                                    ImageUrl = None
                                    Language = None
                                    RawFormat = Csv
                                })

                    if drafts.Count = 0 then
                        Error "No valid records found in CSV payload"
                    else
                        Ok (drafts |> Seq.toList)
            with ex ->
                Error $"Invalid CSV payload: {ex.Message}"

    // ==========================================
    // Schema.org (JSON-LD) Parser
    // ==========================================

    let parseSchemaOrg (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty Schema.org payload"
        else
            try
                use doc = JsonDocument.Parse(payload)
                let root = doc.RootElement
                let elementsToParse =
                    match root.ValueKind with
                    | JsonValueKind.Object ->
                        match root.TryGetProperty("@graph") with
                        | true, graphElem when graphElem.ValueKind = JsonValueKind.Array ->
                            graphElem.EnumerateArray() |> Seq.toList
                        | _ -> [ root ]
                    | JsonValueKind.Array ->
                        root.EnumerateArray() |> Seq.toList
                    | other -> []

                if elementsToParse.IsEmpty then
                    Error "No JSON elements found in Schema.org payload"
                else
                    let results =
                        elementsToParse
                        |> List.map (fun elem ->
                            match parseSingleJsonElement elem with
                            | Ok draft -> Ok { draft with RawFormat = SchemaOrg }
                            | Error err -> Error err)
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
            with ex ->
                Error $"Invalid Schema.org payload: {ex.Message}"

    // ==========================================
    // Dublin Core (XML) Parser
    // ==========================================

    let private parseSingleDublinCoreRecord (recordEl: XElement) : Result<CatalogImportDraft, string> =
        try
            let findValues (names: string list) =
                recordEl.Descendants()
                |> Seq.filter (fun el ->
                    let local = el.Name.LocalName.ToLowerInvariant()
                    names |> List.exists (fun n -> local = n))
                |> Seq.map (fun el -> el.Value.Trim())
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.toList

            let title =
                match findValues [ "title" ] with
                | t :: _ -> t
                | [] -> ""

            let authors =
                findValues [ "creator"; "contributor" ]
                |> List.map normalizeAuthorName
                |> List.filter (not << String.IsNullOrWhiteSpace)
                |> List.distinct

            let publisher =
                match findValues [ "publisher" ] with
                | p :: _ -> Some p
                | [] -> None

            let year =
                findValues [ "date" ]
                |> List.tryPick extract4DigitYear

            let mutable isbnOpt = None
            let mutable sbnOpt = None
            let identifiers = findValues [ "identifier" ]
            for idVal in identifiers do
                let lower = idVal.ToLowerInvariant()
                if lower.StartsWith("urn:isbn:") then
                    isbnOpt <- Some (idVal.Substring("urn:isbn:".Length).Trim())
                elif lower.StartsWith("urn:sbn:") then
                    sbnOpt <- Some (idVal.Substring("urn:sbn:".Length).Trim())
                elif Regex.IsMatch(idVal, @"^(97[89][0-9]{10}|[0-9]{9}[0-9Xx])$") then
                    if isbnOpt.IsNone then isbnOpt <- Some idVal

            let language =
                match findValues [ "language" ] with
                | l :: _ -> Some l
                | [] -> None

            let subjects = findValues [ "subject" ]
            let description =
                match findValues [ "description" ] with
                | d :: _ -> Some d
                | [] -> None

            if String.IsNullOrWhiteSpace title && isbnOpt.IsNone then
                Error "Dublin Core record has neither title nor ISBN"
            else
                Ok {
                    Title = if String.IsNullOrWhiteSpace title then (isbnOpt |> Option.defaultValue "Untitled") else title
                    Authors = authors
                    Publisher = publisher
                    Year = year
                    Isbn = isbnOpt
                    SbnCode = sbnOpt
                    Categories = subjects
                    Tags = subjects
                    Description = description
                    ImageUrl = None
                    Language = language
                    RawFormat = DublinCore
                }
        with ex ->
            Error $"Error parsing Dublin Core record: {ex.Message}"

    let parseDublinCore (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty Dublin Core payload"
        else
            try
                let doc = XDocument.Parse(payload)
                let records =
                    let metaElements =
                        doc.Descendants()
                        |> Seq.filter (fun el ->
                            let name = el.Name.LocalName.ToLowerInvariant()
                            name = "metadata" || name = "record")
                        |> Seq.toList
                    if not metaElements.IsEmpty then metaElements
                    else
                        // If root is itself the metadata element or contains dc:title directly
                        [ doc.Root ]

                if records.IsEmpty then
                    Error "No Dublin Core records found in payload"
                else
                    let results = records |> List.map parseSingleDublinCoreRecord
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
            with ex ->
                Error $"Invalid Dublin Core XML payload: {ex.Message}"

    // ==========================================
    // MODS (XML) Parser
    // ==========================================

    let private parseSingleModsRecord (modsEl: XElement) : Result<CatalogImportDraft, string> =
        try
            let title =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "titleinfo")
                |> Seq.collect (fun ti ->
                    ti.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "title")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead
                |> Option.defaultValue ""

            let authors =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "name")
                |> Seq.collect (fun n ->
                    n.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "namepart")
                    |> Seq.map (fun el -> normalizeAuthorName el.Value))
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.distinct
                |> Seq.toList

            let publisher =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "origininfo")
                |> Seq.collect (fun oi ->
                    oi.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "publisher")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead

            let year =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "origininfo")
                |> Seq.collect (fun oi ->
                    oi.Descendants()
                    |> Seq.filter (fun el ->
                        let name = el.Name.LocalName.ToLowerInvariant()
                        name = "dateissued" || name = "copyrightdate")
                    |> Seq.choose (fun el -> extract4DigitYear el.Value))
                |> Seq.tryHead

            let mutable isbnOpt = None
            let mutable sbnOpt = None
            for idEl in modsEl.Descendants() |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "identifier") do
                let typeAttr =
                    idEl.Attributes()
                    |> Seq.tryFind (fun a -> a.Name.LocalName.ToLowerInvariant() = "type")
                    |> Option.map (fun a -> a.Value.Trim().ToLowerInvariant())
                    |> Option.defaultValue ""
                let valStr = idEl.Value.Trim()
                if typeAttr = "isbn" || Regex.IsMatch(valStr, @"^(97[89][0-9]{10}|[0-9]{9}[0-9Xx])$") then
                    if isbnOpt.IsNone then isbnOpt <- Some valStr
                elif typeAttr = "sbn" then
                    sbnOpt <- Some valStr

            let description =
                modsEl.Descendants()
                |> Seq.filter (fun el ->
                    let name = el.Name.LocalName.ToLowerInvariant()
                    name = "abstract" || name = "tableofcontents" || name = "note")
                |> Seq.map (fun el -> el.Value.Trim())
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.tryHead

            let language =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "language")
                |> Seq.collect (fun langEl ->
                    langEl.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "languageterm")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead

            let subjects =
                modsEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "subject")
                |> Seq.collect (fun s ->
                    s.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "topic")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.distinct
                |> Seq.toList

            if String.IsNullOrWhiteSpace title && isbnOpt.IsNone then
                Error "MODS record has neither title nor ISBN"
            else
                Ok {
                    Title = if String.IsNullOrWhiteSpace title then (isbnOpt |> Option.defaultValue "Untitled") else title
                    Authors = authors
                    Publisher = publisher
                    Year = year
                    Isbn = isbnOpt
                    SbnCode = sbnOpt
                    Categories = subjects
                    Tags = subjects
                    Description = description
                    ImageUrl = None
                    Language = language
                    RawFormat = Mods
                }
        with ex ->
            Error $"Error parsing MODS record: {ex.Message}"

    let parseMods (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty MODS payload"
        else
            try
                let doc = XDocument.Parse(payload)
                let records =
                    let elements =
                        doc.Descendants()
                        |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "mods")
                        |> Seq.toList
                    if not elements.IsEmpty then elements
                    elif doc.Root.Name.LocalName.ToLowerInvariant() = "mods" then [ doc.Root ]
                    else []

                if records.IsEmpty then
                    Error "No <mods> elements found in MODS payload"
                else
                    let results = records |> List.map parseSingleModsRecord
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
            with ex ->
                Error $"Invalid MODS XML payload: {ex.Message}"

    // ==========================================
    // ONIX (3.0 XML) Parser
    // ==========================================

    let private parseSingleOnixProduct (productEl: XElement) : Result<CatalogImportDraft, string> =
        try
            let title =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "titledetail")
                |> Seq.collect (fun td ->
                    td.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "titletext")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead
                |> Option.defaultValue ""

            let authors =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "contributor")
                |> Seq.choose (fun c ->
                    let personName =
                        c.Descendants()
                        |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "personname")
                        |> Seq.map (fun el -> el.Value.Trim())
                        |> Seq.tryHead

                    match personName with
                    | Some pn when not (String.IsNullOrWhiteSpace pn) ->
                        Some (normalizeAuthorName pn)
                    | _ ->
                        let keyNames =
                            c.Descendants()
                            |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "keynames")
                            |> Seq.map (fun el -> el.Value.Trim())
                            |> Seq.tryHead
                        let namesBeforeKey =
                            c.Descendants()
                            |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "namesbeforekey")
                            |> Seq.map (fun el -> el.Value.Trim())
                            |> Seq.tryHead
                        match namesBeforeKey, keyNames with
                        | Some first, Some last -> Some (normalizeAuthorName $"{first} {last}")
                        | None, Some last -> Some (normalizeAuthorName last)
                        | Some first, None -> Some (normalizeAuthorName first)
                        | None, None -> None)
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.distinct
                |> Seq.toList

            let mutable isbnOpt = None
            let mutable sbnOpt = None
            for pidEl in productEl.Descendants() |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "productidentifier") do
                let typeVal =
                    pidEl.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "productidtype")
                    |> Seq.map (fun el -> el.Value.Trim())
                    |> Seq.tryHead
                    |> Option.defaultValue ""
                let typeName =
                    pidEl.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "idtypename")
                    |> Seq.map (fun el -> el.Value.Trim().ToLowerInvariant())
                    |> Seq.tryHead
                    |> Option.defaultValue ""
                let idVal =
                    pidEl.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "idvalue")
                    |> Seq.map (fun el -> el.Value.Trim())
                    |> Seq.tryHead
                    |> Option.defaultValue ""

                if not (String.IsNullOrWhiteSpace idVal) then
                    // 15 = ISBN-13, 02 = ISBN-10
                    if typeVal = "15" || typeVal = "02" || Regex.IsMatch(idVal, @"^(97[89][0-9]{10}|[0-9]{9}[0-9Xx])$") then
                        if isbnOpt.IsNone then isbnOpt <- Some idVal
                    elif typeName = "sbn" then
                        sbnOpt <- Some idVal

            let publisher =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "publisher")
                |> Seq.collect (fun pubEl ->
                    pubEl.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "publishername")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead

            let year =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "publishingdate")
                |> Seq.collect (fun pd ->
                    pd.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "date")
                    |> Seq.choose (fun el -> extract4DigitYear el.Value))
                |> Seq.tryHead

            let description =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "textcontent")
                |> Seq.collect (fun tc ->
                    tc.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "text")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.tryHead

            let language =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "language")
                |> Seq.collect (fun l ->
                    l.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "languagecode")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.tryHead

            let subjects =
                productEl.Descendants()
                |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "subject")
                |> Seq.collect (fun s ->
                    s.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "subjectheadingtext")
                    |> Seq.map (fun el -> el.Value.Trim()))
                |> Seq.filter (not << String.IsNullOrWhiteSpace)
                |> Seq.distinct
                |> Seq.toList

            if String.IsNullOrWhiteSpace title && isbnOpt.IsNone then
                Error "ONIX product has neither title nor ISBN"
            else
                Ok {
                    Title = if String.IsNullOrWhiteSpace title then (isbnOpt |> Option.defaultValue "Untitled") else title
                    Authors = authors
                    Publisher = publisher
                    Year = year
                    Isbn = isbnOpt
                    SbnCode = sbnOpt
                    Categories = subjects
                    Tags = subjects
                    Description = description
                    ImageUrl = None
                    Language = language
                    RawFormat = Onix
                }
        with ex ->
            Error $"Error parsing ONIX product: {ex.Message}"

    let parseOnix (payload: string) : Result<CatalogImportDraft list, string> =
        if String.IsNullOrWhiteSpace payload then
            Error "Empty ONIX payload"
        else
            try
                let doc = XDocument.Parse(payload)
                let records =
                    doc.Descendants()
                    |> Seq.filter (fun el -> el.Name.LocalName.ToLowerInvariant() = "product")
                    |> Seq.toList

                if records.IsEmpty then
                    Error "No <Product> elements found in ONIX payload"
                else
                    let results = records |> List.map parseSingleOnixProduct
                    let errors = results |> List.choose (function Error e -> Some e | _ -> None)
                    let items = results |> List.choose (function Ok item -> Some item | _ -> None)
                    if items.IsEmpty && not errors.IsEmpty then
                        Error (String.concat "; " errors)
                    else
                        Ok items
            with ex ->
                Error $"Invalid ONIX XML payload: {ex.Message}"

    // ==========================================
    // Unified Dispatcher
    // ==========================================

    let parse (format: ExportFormat) (payload: string) : Result<CatalogImportDraft list, string> =
        match format with
        | Json -> parseJson payload
        | Csv -> parseCsv payload
        | BibTex -> parseBibTex payload
        | SchemaOrg -> parseSchemaOrg payload
        | DublinCore -> parseDublinCore payload
        | MarcXml -> parseMarcXml payload
        | Mods -> parseMods payload
        | Onix -> parseOnix payload
