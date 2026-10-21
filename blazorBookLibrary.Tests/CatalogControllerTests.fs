module CatalogControllerTests

open System
open System.Collections.Generic
open System.Threading
open Expecto
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Primitives
open TestSetup
open BookLibrary.Domain
open BookLibrary.Controllers
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open BookLibrary.Shared.Details

let private createController (apiKeyConfig: (string * string) list) =
    let configBuilder = ConfigurationBuilder()
    if not (List.isEmpty apiKeyConfig) then
        let mem = Dictionary<string, string>()
        for k, v in apiKeyConfig do
            mem.[k] <- v
        configBuilder.AddInMemoryCollection(mem) |> ignore
    let config = configBuilder.Build() :> IConfiguration
    let controller = CatalogController(getBookService(), getDetailsService(), getEditorService(), getTenantService(), config)
    let httpContext = DefaultHttpContext()
    controller.ControllerContext <- ControllerContext(HttpContext = httpContext)
    controller

[<Tests>]
let catalogTests =
    testList "Catalog Controller tests" [
        testCaseTask "GetBookByIsbnDefault formats as MARCXML when requested" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let editorService = getEditorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Catalog Author MARC")
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let editor = Editor.NewWithoutDate TenantId.Default (Name.New "Catalog Publisher MARC")
            let! addEditor = editorService.AddEditorAsync(adminContext, editor)
            Expect.isOk addEditor "Add editor should succeed"

            let isbn = "9780132350884"
            let book =
                { Book.New TenantId.Default (Title.New "Catalog Clean Code") [author.AuthorId] [] [] (Some editor.EditorId) Category.Technology [] (Year.New 2008) (Isbn isbn) None
                    with SbnCode = Some (ValidSbn "CFI0009999") }
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let controller = createController []
            let! actionResult = controller.GetBookByIsbnDefault(isbn, format = "marcxml")
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.equal contentResult.ContentType ExportFormat.MarcXml.ContentType "Content type should match MARCXML"
            Expect.isTrue (contentResult.Content.Contains("<record xmlns=\"http://www.loc.gov/MARC21/slim\">")) "Should contain MARC21 root"
            Expect.isTrue (contentResult.Content.Contains(isbn)) "Should contain ISBN"
            Expect.isTrue (contentResult.Content.Contains("Catalog Clean Code")) "Should contain title"
            Expect.isTrue (contentResult.Content.Contains("Catalog Author MARC")) "Should contain author"
            Expect.isTrue (contentResult.Content.Contains("Catalog Publisher MARC")) "Should contain publisher"
        }

        testCaseTask "GetBookByIsbnDefault formats as MODS when requested" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Catalog Author MODS")
            let! _ = authorService.AddAuthorAsync(adminContext, author)

            let isbn = "9780201616224"
            let book = Book.New TenantId.Default (Title.New "Catalog Pragmatic Programmer") [author.AuthorId] [] [] None Category.Technology [] (Year.New 1999) (Isbn isbn) None
            let! _ = bookService.AddBookAsync(adminContext, book, CancellationToken.None)

            let controller = createController []
            let! actionResult = controller.GetBookByIsbnDefault(isbn, format = "mods")
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.equal contentResult.ContentType ExportFormat.Mods.ContentType "Content type should match MODS"
            Expect.isTrue (contentResult.Content.Contains("<mods xmlns=\"http://www.loc.gov/mods/v3\"")) "Should contain MODS root"
            Expect.isTrue (contentResult.Content.Contains(isbn)) "Should contain ISBN"
            Expect.isTrue (contentResult.Content.Contains("Catalog Pragmatic Programmer")) "Should contain title"
        }

        testCaseTask "GetBookByIsbnDefault formats as ONIX when requested" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Catalog Author ONIX")
            let! _ = authorService.AddAuthorAsync(adminContext, author)

            let isbn = "9780134494166"
            let book = Book.New TenantId.Default (Title.New "Catalog Clean Architecture") [author.AuthorId] [] [] None Category.Technology [] (Year.New 2017) (Isbn isbn) None
            let! _ = bookService.AddBookAsync(adminContext, book, CancellationToken.None)

            let controller = createController []
            let! actionResult = controller.GetBookByIsbnDefault(isbn, format = "onix")
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.equal contentResult.ContentType ExportFormat.Onix.ContentType "Content type should match ONIX"
            Expect.isTrue (contentResult.Content.Contains("<ONIXMessage")) "Should contain ONIXMessage"
            Expect.isTrue (contentResult.Content.Contains(isbn)) "Should contain ISBN"
            Expect.isTrue (contentResult.Content.Contains("Catalog Clean Architecture")) "Should contain title"
        }

        testCaseTask "GetBookBySbnDefault formats as JSON and resolves SBN correctly" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Umberto Eco")
            let! _ = authorService.AddAuthorAsync(adminContext, author)

            let sbn = "CFI0054321"
            let book =
                { Book.New TenantId.Default (Title.New "Il nome della rosa") [author.AuthorId] [] [] None Category.Fiction [] (Year.New 1980) (Isbn "9788845278051") None
                    with SbnCode = Some (ValidSbn sbn) }
            let! _ = bookService.AddBookAsync(adminContext, book, CancellationToken.None)

            let controller = createController []
            let! actionResult = controller.GetBookBySbnDefault(sbn, format = "json")
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.equal contentResult.ContentType ExportFormat.Json.ContentType "Content type should match JSON"
            Expect.isTrue (contentResult.Content.Contains("Il nome della rosa")) "Should contain title"
            Expect.isTrue (contentResult.Content.Contains(sbn)) "Should contain SBN code"
            Expect.isTrue (contentResult.Content.Contains("Umberto Eco")) "Should contain author"
        }

        testCaseTask "GetBookByIdDefault returns single book representation" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Italo Calvino")
            let! _ = authorService.AddAuthorAsync(adminContext, author)

            let book = Book.New TenantId.Default (Title.New "Le citta invisibili") [author.AuthorId] [] [] None Category.Fiction [] (Year.New 1972) (Isbn "9788804667957") None
            let! _ = bookService.AddBookAsync(adminContext, book, CancellationToken.None)

            let controller = createController []
            let! actionResult = controller.GetBookByIdDefault(book.BookId.Value, format = "bibtex")
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.isTrue (contentResult.Content.Contains("@book{")) "Should contain BibTeX @book"
            Expect.isTrue (contentResult.Content.Contains("Le citta invisibili")) "Should contain title"
            Expect.isTrue (contentResult.Content.Contains("Italo Calvino")) "Should contain author"
        }

        testCaseTask "Accept header content negotiation fallback works when format query is not provided" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Dante Alighieri")
            let! _ = authorService.AddAuthorAsync(adminContext, author)

            let isbn = "9788804680000"
            let book = Book.New TenantId.Default (Title.New "La Divina Commedia") [author.AuthorId] [] [] None Category.Poetry [] (Year.New 1320) (Isbn isbn) None
            let! _ = bookService.AddBookAsync(adminContext, book, CancellationToken.None)

            let controller = createController []
            controller.ControllerContext.HttpContext.Request.Headers.["Accept"] <- StringValues("application/marcxml+xml")
            let! actionResult = controller.GetBookByIsbnDefault(isbn, format = null)
            Expect.isTrue (actionResult :? ContentResult) "Should return ContentResult"
            let contentResult = actionResult :?> ContentResult
            Expect.equal contentResult.ContentType ExportFormat.MarcXml.ContentType "Content type should match MARCXML"
            Expect.isTrue (contentResult.Content.Contains("<record xmlns=\"http://www.loc.gov/MARC21/slim\">")) "Should fall back to Accept header MARCXML"
        }

        testCaseTask "CatalogController enforces X-API-Key when configured" <| fun _ -> task {
            setUp ()
            let controller = createController [ "Catalog:ApiKey", "secret-test-key-123" ]

            // Missing header -> 401
            let! unauthorizedResult = controller.GetBookByIsbnDefault("9780132350884", format = "json")
            Expect.isTrue (unauthorizedResult :? UnauthorizedObjectResult) "Should return 401 when key is missing"

            // Invalid key -> 401
            controller.ControllerContext.HttpContext.Request.Headers.["X-API-Key"] <- StringValues("wrong-key")
            let! invalidResult = controller.GetBookByIsbnDefault("9780132350884", format = "json")
            Expect.isTrue (invalidResult :? UnauthorizedObjectResult) "Should return 401 when key is incorrect"

            // Valid key -> proceeds (returns 404 for non-existent ISBN, rather than 401)
            controller.ControllerContext.HttpContext.Request.Headers.["X-API-Key"] <- StringValues("secret-test-key-123")
            let! okResult = controller.GetBookByIsbnDefault("9999999999999", format = "json")
            Expect.isTrue (okResult :? NotFoundObjectResult) "Should authenticate and return 404 for missing book"
        }

        testCaseTask "Tenant isolation: querying default tenant does not leak other tenant books" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let tenantService = getTenantService()

            // Register owner and create a public tenant
            let! ownerId = registerUserTask "catalogowner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let publicTenant = Tenant.New(ownerId, TenantName.New "Isolated Public Library" |> Result.get, "Via Roma 1", true)
            let otherTenantId = publicTenant.TenantId
            let! createRes = tenantService.CreateTenantAsync(ownerContext, publicTenant)
            Expect.isOk createRes "Tenant creation should succeed"
            let! approveRes = tenantService.SetPublicAsync(adminContext, otherTenantId)
            Expect.isOk approveRes "Tenant public approval should succeed"

            let otherIsbn = "9781234567897"
            let bookInOtherTenant = Book.New otherTenantId (Title.New "Tenant Isolated Book") [] [] [] None Category.Fiction [] (Year.New 2024) (Isbn otherIsbn) None
            let otherContext = adminContext.WithNewTenant(otherTenantId)
            let! addBook = bookService.AddBookAsync(otherContext, bookInOtherTenant, CancellationToken.None)
            Expect.isOk addBook "Add book in other tenant should succeed"

            let controller = createController []

            // Querying via default tenant should NOT find the book in other tenant
            let! defaultResult = controller.GetBookByIsbnDefault(otherIsbn, format = "json")
            Expect.isTrue (defaultResult :? NotFoundObjectResult) "Default tenant query should not find other tenant's book"

            // Querying with other tenant GUID in URL succeeds
            let! tenantResult = controller.GetBookByIsbnWithTenant(otherTenantId.Value.ToString(), otherIsbn, format = "json")
            match tenantResult with
            | :? ContentResult as cr ->
                if not (cr.Content.Contains("Tenant Isolated Book")) then
                    failwithf "Content was: '%s'" cr.Content
            | :? ObjectResult as objRes ->
                failwithf "Expected ContentResult but got ObjectResult with status %A, value: %A" objRes.StatusCode objRes.Value
            | other ->
                failwithf "Expected ContentResult but got %s" (other.GetType().FullName)
        }

        testCase "CatalogApiKey.New generates unique keys with bbl_cat_ prefix" <| fun _ ->
            let key1 = CatalogApiKey.New()
            let key2 = CatalogApiKey.New()

            Expect.isTrue (key1.Value.StartsWith("bbl_cat_")) "Key1 should start with bbl_cat_"
            Expect.isTrue (key2.Value.StartsWith("bbl_cat_")) "Key2 should start with bbl_cat_"
            Expect.notEqual key1.Value key2.Value "Successive New() calls must generate distinct keys"
            Expect.equal key1.UniqueId.Length 32 "UniqueId should be 32 characters (hex Guid)"
            Expect.isTrue key1.TryGuid.IsSome "UniqueId should parse to a valid Guid"

        testCase "CatalogApiKey validation and parsing" <| fun _ ->
            let testGuid = Guid.NewGuid()
            let keyFromGuid = CatalogApiKey.New(testGuid)
            Expect.equal keyFromGuid.Value ($"bbl_cat_{testGuid.ToString(\"N\")}") "Should match expected format from Guid"
            Expect.equal keyFromGuid.TryGuid (Some testGuid) "TryGuid should roundtrip testGuid"

            Expect.isTrue (CatalogApiKey.IsValid keyFromGuid.Value) "Valid key should pass IsValid"
            Expect.isFalse (CatalogApiKey.IsValid "invalid-key") "Invalid key without prefix should fail IsValid"
            Expect.isFalse (CatalogApiKey.IsValid "bbl_cat_") "Empty identifier should fail IsValid"
            Expect.isFalse (CatalogApiKey.IsValid "") "Empty string should fail IsValid"

            let parsed = CatalogApiKey.FromString keyFromGuid.Value
            Expect.isOk parsed "FromString on valid key should return Ok"
            Expect.equal (Result.get parsed) keyFromGuid "Parsed key should equal original key"

            let invalidParsed = CatalogApiKey.FromString "bad_prefix_123"
            Expect.isError invalidParsed "FromString on invalid prefix should return Error"
    ]
