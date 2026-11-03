module FormatParsersTests

open System
open System.Globalization
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Shared.Formats

let private makeSampleBookDetails () =
    let author1 = Author.NewWithoutIsni TenantId.Default (Name.New "Isaac Asimov")
    let author2 = Author.NewWithoutIsni TenantId.Default (Name.New "Robert Silverberg")
    let editor = Editor.NewWithoutDate TenantId.Default (Name.New "Spectra Books")

    let isbn = "9780553293357"
    let book =
        { Book.New
            TenantId.Default
            (Title.New "Nightfall")
            [ author1.AuthorId; author2.AuthorId ]
            []
            [ CultureInfo.GetCultureInfo("en") ]
            (Some editor.EditorId)
            Category.ScienceFiction
            [ Category.Fiction ]
            (Year.New 1990)
            (Isbn isbn)
            None
          with
            Description = Some "A classic expanded science fiction novel."
            SbnCode = Some (ValidSbn "CFI0001234")
            Tags = [ BookTag "Classic"; BookTag "Sci-Fi" ] }

    let bd: BookDetails =
        { Book = book
          Authors = [ author1; author2 ]
          Tenant = None
          DistributionPoint = None
          CurrentLoan = None
          ReservationsDetails = []
          ApprovedVisibleReviews = [] }
    bd, editor

[<Tests>]
let formatParsersTests =
    testList "FormatParsers Tests" [

        testCase "JSON parser handles single and array payloads" <| fun _ ->
            let singleJson = """
            {
                "Title": "Foundation",
                "Isbn": "9780553293357",
                "Year": 1951,
                "Authors": ["Isaac Asimov"],
                "Publisher": "Gnome Press",
                "MainCategory": "ScienceFiction",
                "Tags": ["Classic", "Sci-Fi"],
                "Description": "The galactic empire falls."
            }
            """
            let singleResult = FormatParsers.parseJson singleJson
            Expect.isOk singleResult "Single JSON parse should succeed"
            let singleItems = singleResult |> Result.get
            Expect.equal singleItems.Length 1 "Should parse 1 item"
            let item = singleItems.Head
            Expect.equal item.Title "Foundation" "Title should match"
            Expect.equal item.Isbn (Some "9780553293357") "ISBN should match"
            Expect.equal item.Year (Some 1951) "Year should match"
            Expect.equal item.Authors [ "Isaac Asimov" ] "Authors should match"
            Expect.equal item.Publisher (Some "Gnome Press") "Publisher should match"
            Expect.equal item.Description (Some "The galactic empire falls.") "Description should match"

            let arrayJson = """
            [
                { "Title": "Book One", "Authors": ["Author A"], "Year": 2001 },
                { "Title": "Book Two", "Authors": ["Author B"], "Year": 2002 }
            ]
            """
            let arrayResult = FormatParsers.parseJson arrayJson
            Expect.isOk arrayResult "Array JSON parse should succeed"
            let arrayItems = arrayResult |> Result.get
            Expect.equal arrayItems.Length 2 "Should parse 2 items"
            Expect.equal arrayItems.[0].Title "Book One" "First title should match"
            Expect.equal arrayItems.[1].Title "Book Two" "Second title should match"

        testCase "JSON parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            // Format to JSON using existing serializer logic
            let dto =
                {|
                    Id = bd.Book.BookId.Value
                    Title = bd.Book.Title.Value
                    Isbn = bd.Book.Isbn.Value
                    SbnCode = bd.Book.SbnCode |> Option.map (fun s -> s.Value)
                    Year = bd.Book.Year.Value
                    Authors = bd.Authors |> List.map (fun a -> a.Name.Value)
                    Publisher = publisher
                    Availability = bd.Book.Availability.ToString()
                    MainCategory = bd.Book.MainCategory.ToString()
                    Description = bd.Book.Description
                    ImageUrl = bd.Book.ImageUrl |> Option.map (fun u -> u.ToString())
                    Tags = bd.Book.Tags |> List.map (fun t -> t.TagName)
                |}
            let json = System.Text.Json.JsonSerializer.Serialize(dto)

            let parseResult = FormatParsers.parseJson json
            Expect.isOk parseResult "Roundtrip JSON parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"

        testCase "BibTeX parser parses single and multiple entries with author splitting" <| fun _ ->
            let bibtex = """
            @book{asimov1990nightfall,
              title = {Nightfall},
              author = {Asimov, Isaac and Silverberg, Robert},
              year = {1990},
              publisher = {Doubleday},
              isbn = {9780553293357},
              keywords = {ScienceFiction, Classic},
              note = {SBN: CFI0001234},
            }

            @book{clarke1968odyssey,
              title = {2001: A Space Odyssey},
              author = {Arthur C. Clarke},
              year = {1968},
              publisher = {New American Library},
              isbn = {9780451457998},
            }
            """
            let result = FormatParsers.parseBibTex bibtex
            Expect.isOk result "BibTeX parse should succeed"
            let items = result |> Result.get
            Expect.equal items.Length 2 "Should parse 2 BibTeX entries"

            let first = items.[0]
            Expect.equal first.Title "Nightfall" "First title should match"
            Expect.equal first.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should be normalized and split"
            Expect.equal first.Year (Some 1990) "Year should match"
            Expect.equal first.Publisher (Some "Doubleday") "Publisher should match"
            Expect.equal first.Isbn (Some "9780553293357") "ISBN should match"
            Expect.equal first.SbnCode (Some "CFI0001234") "SBN code from note should match"

            let second = items.[1]
            Expect.equal second.Title "2001: A Space Odyssey" "Second title should match"
            Expect.equal second.Authors [ "Arthur C. Clarke" ] "Second author should match"
            Expect.equal second.Year (Some 1968) "Second year should match"

        testCase "BibTeX parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let bibtex = FormatSerializers.formatBibTexBook bd publisher

            let parseResult = FormatParsers.parseBibTex bibtex
            Expect.isOk parseResult "BibTeX roundtrip parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.SbnCode (Some "CFI0001234") "SBN code should roundtrip"

        testCase "MARCXML parser parses record and collection" <| fun _ ->
            let marcxml = """
            <collection xmlns="http://www.loc.gov/MARC21/slim">
              <record>
                <leader>00000nam a2200000 a 4500</leader>
                <controlfield tag="001">rec-1</controlfield>
                <controlfield tag="008">240101s1990    xx                  eng d</controlfield>
                <datafield tag="020" ind1=" " ind2=" ">
                  <subfield code="a">9780553293357 (pbk.)</subfield>
                </datafield>
                <datafield tag="024" ind1="7" ind2=" ">
                  <subfield code="a">CFI0001234</subfield>
                  <subfield code="2">sbn</subfield>
                </datafield>
                <datafield tag="100" ind1="1" ind2=" ">
                  <subfield code="a">Isaac Asimov</subfield>
                </datafield>
                <datafield tag="700" ind1="1" ind2=" ">
                  <subfield code="a">Robert Silverberg</subfield>
                </datafield>
                <datafield tag="245" ind1="1" ind2="0">
                  <subfield code="a">Nightfall</subfield>
                </datafield>
                <datafield tag="264" ind1=" " ind2="1">
                  <subfield code="b">Spectra Books</subfield>
                  <subfield code="c">1990</subfield>
                </datafield>
                <datafield tag="520" ind1=" " ind2=" ">
                  <subfield code="a">A sci-fi masterpiece.</subfield>
                </datafield>
                <datafield tag="650" ind1=" " ind2="7">
                  <subfield code="a">ScienceFiction</subfield>
                  <subfield code="2">local</subfield>
                </datafield>
              </record>
            </collection>
            """
            let result = FormatParsers.parseMarcXml marcxml
            Expect.isOk result "MARCXML parse should succeed"
            let items = result |> Result.get
            Expect.equal items.Length 1 "Should parse 1 MARCXML record"
            let item = items.Head
            Expect.equal item.Title "Nightfall" "Title should match"
            Expect.equal item.Isbn (Some "9780553293357") "ISBN should be stripped of qualifier"
            Expect.equal item.SbnCode (Some "CFI0001234") "SBN code should match"
            Expect.equal item.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should match"
            Expect.equal item.Publisher (Some "Spectra Books") "Publisher should match"
            Expect.equal item.Year (Some 1990) "Year should match"
            Expect.equal item.Description (Some "A sci-fi masterpiece.") "Description should match"

        testCase "MARCXML parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let marcxml = FormatSerializers.formatMarcXmlBook bd publisher

            let parseResult = FormatParsers.parseMarcXml marcxml
            Expect.isOk parseResult "MARCXML roundtrip parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.SbnCode (Some "CFI0001234") "SBN code should roundtrip"
            Expect.equal parsed.Description bd.Book.Description "Description should roundtrip"

        testCase "CSV parser handles header and rows" <| fun _ ->
            let csv = """Id,Title,Isbn,Year,Availability,MainCategory
11111111-1111-1111-1111-111111111111,"Nightfall",9780553293357,1990,Circulating,ScienceFiction
22222222-2222-2222-2222-222222222222,"Foundation",9780553293358,1951,Circulating,ScienceFiction"""
            let result = FormatParsers.parseCsv csv
            Expect.isOk result "CSV parse should succeed"
            let items = result |> Result.get
            Expect.equal items.Length 2 "Should parse 2 CSV rows"
            Expect.equal items.Head.Title "Nightfall" "First title should match"
            Expect.equal items.Head.Isbn (Some "9780553293357") "First ISBN should match"
            Expect.equal items.Head.Year (Some 1990) "First year should match"
            Expect.equal items.[1].Title "Foundation" "Second title should match"

        testCase "Schema.org parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let jsonLd = FormatSerializers.formatSchemaOrgBook bd publisher

            let parseResult = FormatParsers.parseSchemaOrg jsonLd
            Expect.isOk parseResult "Schema.org parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.Description bd.Book.Description "Description should roundtrip"

        testCase "Schema.org graph roundtrip" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let graphJson = FormatSerializers.formatSchemaOrgGraph [ (bd, publisher) ]

            let parseResult = FormatParsers.parseSchemaOrg graphJson
            Expect.isOk parseResult "Schema.org graph parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item from graph"
            Expect.equal items.Head.Title bd.Book.Title.Value "Title should match"

        testCase "Dublin Core parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let dcXml = FormatSerializers.formatDublinCoreBook bd publisher

            let parseResult = FormatParsers.parseDublinCore dcXml
            Expect.isOk parseResult "Dublin Core parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.SbnCode (Some "CFI0001234") "SBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.Description bd.Book.Description "Description should roundtrip"

        testCase "Dublin Core bulk roundtrip" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let bulkXml = FormatSerializers.formatDublinCoreBulk [ (bd, publisher) ]

            let parseResult = FormatParsers.parseDublinCore bulkXml
            Expect.isOk parseResult "Dublin Core bulk parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            Expect.equal items.Head.Title bd.Book.Title.Value "Title should roundtrip"

        testCase "MODS parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let modsXml = FormatSerializers.formatModsBook bd publisher

            let parseResult = FormatParsers.parseMods modsXml
            Expect.isOk parseResult "MODS parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.SbnCode (Some "CFI0001234") "SBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.Description bd.Book.Description "Description should roundtrip"

        testCase "MODS bulk roundtrip" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let bulkMods = FormatSerializers.formatModsBulk [ (bd, publisher) ]

            let parseResult = FormatParsers.parseMods bulkMods
            Expect.isOk parseResult "MODS bulk parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item from bulk"
            Expect.equal items.Head.Title bd.Book.Title.Value "Title should roundtrip"

        testCase "ONIX parser roundtrip with FormatSerializers" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let onixXml = FormatSerializers.formatOnixBook bd publisher

            let parseResult = FormatParsers.parseOnix onixXml
            Expect.isOk parseResult "ONIX parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item"
            let parsed = items.Head
            Expect.equal parsed.Title bd.Book.Title.Value "Title should roundtrip"
            Expect.equal parsed.Isbn (Some bd.Book.Isbn.Value) "ISBN should roundtrip"
            Expect.equal parsed.SbnCode (Some "CFI0001234") "SBN should roundtrip"
            Expect.equal parsed.Year (Some bd.Book.Year.Value) "Year should roundtrip"
            Expect.equal parsed.Publisher publisher "Publisher should roundtrip"
            Expect.equal parsed.Authors [ "Isaac Asimov"; "Robert Silverberg" ] "Authors should roundtrip"
            Expect.equal parsed.Description bd.Book.Description "Description should roundtrip"

        testCase "ONIX bulk roundtrip" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value
            let bulkOnix = FormatSerializers.formatOnixBulk [ (bd, publisher) ]

            let parseResult = FormatParsers.parseOnix bulkOnix
            Expect.isOk parseResult "ONIX bulk parse should succeed"
            let items = parseResult |> Result.get
            Expect.equal items.Length 1 "Should parse 1 item from bulk"
            Expect.equal items.Head.Title bd.Book.Title.Value "Title should roundtrip"

        testCase "Unified dispatcher routes to all 8 formats" <| fun _ ->
            let bd, editor = makeSampleBookDetails ()
            let publisher = Some editor.Name.Value

            let formats = [
                ExportFormat.Json, FormatSerializers.formatSchemaOrgBook bd publisher // valid json
                ExportFormat.Csv, sprintf "Title,Isbn,Year\n\"%s\",%s,%d" bd.Book.Title.Value bd.Book.Isbn.Value bd.Book.Year.Value
                ExportFormat.BibTex, FormatSerializers.formatBibTexBook bd publisher
                ExportFormat.SchemaOrg, FormatSerializers.formatSchemaOrgBook bd publisher
                ExportFormat.DublinCore, FormatSerializers.formatDublinCoreBook bd publisher
                ExportFormat.MarcXml, FormatSerializers.formatMarcXmlBook bd publisher
                ExportFormat.Mods, FormatSerializers.formatModsBook bd publisher
                ExportFormat.Onix, FormatSerializers.formatOnixBook bd publisher
            ]

            for (fmt, payload) in formats do
                let res = FormatParsers.parse fmt payload
                Expect.isOk res (sprintf "Dispatcher for %s should succeed" fmt.Name)
                let items = res |> Result.get
                Expect.isGreaterThan items.Length 0 (sprintf "Should parse at least 1 item for %s" fmt.Name)
                Expect.equal items.Head.Title bd.Book.Title.Value (sprintf "Title should match for %s" fmt.Name)
    ]
