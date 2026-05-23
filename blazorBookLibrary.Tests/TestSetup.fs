
module TestSetup

open System
open System.Net.Http
open DotNetEnv
open Sharpino.PgStorage
open BookLibrary.Domain
open BookLibrary.Services
open BookLibrary.CleanServices
open BookLibrary.Shared.Details
open Sharpino.Cache
open Sharpino.Core
open BookLibrary.Shared.Commons
open Sharpino.CommandHandler
open Sharpino.EventBroker
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Threading
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.DataProtection
open blazorBookLibrary.Tests.MockServices
open blazorBookLibrary.Data
open blazorBookLibrary.Shared.Infrastructure.Services
open Microsoft.Extensions.Logging
open BookLibrary.Shared.Services
open Microsoft.Extensions.Localization
open blazorBookLibrary.Shared.Resources
open BookLibrary.Utils
open Npgsql
Environment.SetEnvironmentVariable("IsTestEnv", "True")
Env.Load() |> ignore

let config = 
    ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appSettings.json", false)
        .Build()

let timeSlotDurationInDays =
    config.GetValue<int>("BookLibrary::TimeSlotLoanDurationInDays", 30)

let connection =
    config.GetConnectionString("BookLibraryDbConnection")

let pgEventStore:Sharpino.Storage.IEventStore<string> = PgEventStore connection

let usersDbConnection = config.GetConnectionString("UsersDbConnection")

let getDbContext () =
    let options = 
        DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(usersDbConnection)
            .Options
    new ApplicationDbContext(options)

let getServiceScopeFactory () =
    let services = ServiceCollection()
    services.AddLogging() |> ignore
    services.AddDataProtection() |> ignore
    services.AddDbContext<ApplicationDbContext>(fun options -> 
        options.UseNpgsql(usersDbConnection) |> ignore) |> ignore
    services.AddIdentityCore<ApplicationUser>()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddClaimsPrincipalFactory<UserClaimsPrincipalFactory<ApplicationUser>>()
        .AddDefaultTokenProviders() |> ignore
    services.AddSingleton<BookLibrary.Utils.SecretsReader>(fun _ -> new BookLibrary.Utils.SecretsReader(config)) |> ignore

    services.AddSingleton<IMailBodyRetriever, MailBodyRetriever>(fun _ -> new MailBodyRetriever()) |> ignore
    
    let serviceProvider = services.BuildServiceProvider()
    serviceProvider.GetRequiredService<IServiceScopeFactory>()


let getUserManagerOld () =
    let services = ServiceCollection()
    services.AddLogging() |> ignore
    services.AddDataProtection() |> ignore
    services.AddDbContext<ApplicationDbContext>(fun options -> 
        options.UseNpgsql(usersDbConnection) |> ignore) |> ignore
    services.AddIdentityCore<ApplicationUser>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders() |> ignore
    
    let serviceProvider = services.BuildServiceProvider()
    serviceProvider.GetRequiredService<UserManager<ApplicationUser>>()

let getUserManager () =
    let serviceScopeFacotry = getServiceScopeFactory()
    let scope = serviceScopeFacotry.CreateScope()
    scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()

let getClaimsFactory () =
    let serviceScopeFacotry = getServiceScopeFactory()
    let scope = serviceScopeFacotry.CreateScope()
    scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>()

let getSecretReader () =
    let serviceScopeFacotry = getServiceScopeFactory()
    let scope = serviceScopeFacotry.CreateScope()
    scope.ServiceProvider.GetRequiredService<BookLibrary.Utils.SecretsReader>()

let getMailBodyRetriever () =
    let serviceScopeFacotry = getServiceScopeFactory()
    let scope = serviceScopeFacotry.CreateScope()
    scope.ServiceProvider.GetRequiredService<IMailBodyRetriever>()

let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> pgEventStore
let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> pgEventStore
let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> pgEventStore
let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> pgEventStore
let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> pgEventStore
let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> pgEventStore
let reviewViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> pgEventStore
let distributionPointViewerAsync = getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> pgEventStore
let tagViewerAsync = getAggregateStorageFreshStateViewerAsync<Tags, TagEvent, string> pgEventStore
let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> pgEventStore

let adminId = UserId (System.Guid.Parse("787b784e-42d8-416b-9d57-f1e62f857f47"))
let adminContext = UserContext.Authenticated (adminId, [Role.Admin])

let fakeEmailNotificator: IMailNotificator = new FakeEmailNotificator()
let fakeReservationService: IReservationService = new FakeReservationService()
let fakeLocalizer: IStringLocalizer<SharedResources> = new FakeLocalizer<SharedResources>()

let dummyLogger = 
    LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore).CreateLogger<MailResenderService>()

let getDummyLogger<'T> () = 
    LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore).CreateLogger<'T>()
let dummyMailJetClient = new Mailjet.Client.MailjetClient("", "")

let getUserTenantResolverService () : IUserTenantResolverService =
    UserTenantResolverService(pgEventStore, MessageSenders.NoSender, userViewerAsync) :> IUserTenantResolverService

let getAuthorService () : IAuthorService = 
    AuthorService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        tenantViewerAsync,
        getUserTenantResolverService(),
        getSecretReader()) :> IAuthorService

let getReviewService () : IReviewService =
    ReviewService(
        pgEventStore, 
        MessageSenders.NoSender, 
        reviewViewerAsync,
        authorViewerAsync, 
        editorViewerAsync, 
        bookViewerAsync,
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        tenantViewerAsync,
        getUserTenantResolverService(),
        getServiceScopeFactory()) :> IReviewService

let getUserService () : IUserService =
    UserService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        reviewViewerAsync,
        tenantViewerAsync,
        getUserTenantResolverService(),
        distributionPointViewerAsync,
        getReviewService(),
        getServiceScopeFactory(),
        getDummyLogger<UserService>()) :> IUserService

let getReservationService () : IReservationService =
    ReservationService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        tenantViewerAsync,
        distributionPointViewerAsync,
        getUserTenantResolverService(),
        getUserService(),
        fakeEmailNotificator,
        3,
        "noreply@blazorbooklibrary.com",
        "Blazor Book Library",
        getMailBodyRetriever()) :> IReservationService

let getLoanService () : ILoanService =
    LoanService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        tenantViewerAsync,
        distributionPointViewerAsync,
        getUserTenantResolverService(),
        getReservationService(),
        getUserService(),
        fakeEmailNotificator,
        3,
        "noreply@blazorbooklibrary.com",
        "Blazor Book Library",
        fakeLocalizer,
        getMailBodyRetriever()) :> ILoanService
let getDetailsService () : IDetailsService =
    DetailsService(
        pgEventStore,
        MessageSenders.NoSender,
        bookViewerAsync,
        authorViewerAsync,
        editorViewerAsync,
        reservationViewerAsync,
        loanViewerAsync,
        userViewerAsync,
        reviewViewerAsync,
        tenantViewerAsync,
        distributionPointViewerAsync,
        getUserTenantResolverService(),
        getLoanService(),
        getReservationService(),
        getReviewService(),
        getServiceScopeFactory()) :> IDetailsService


let getTextEmbeddingService () =
    let httpClient = new HttpClient()
    TextEmbeddingService(config, httpClient, getDetailsService(), getSecretReader(), getUserTenantResolverService()) :> ITextEmbeddingService

let getVectorDbService () =
    VectorDbService(config, getSecretReader()) :> IVectorDbService


let getBookService () : IBookService = 
    BookService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        tenantViewerAsync,
        distributionPointViewerAsync,
        getUserTenantResolverService(),
        getVectorDbService()) :> IBookService



let getGoogleBooksService () =
    let httpClient = new HttpClient()
    httpClient.DefaultRequestHeaders.Add("User-Agent", "BlazorBookLibraryTest/1.0")
    GoogleBooksService(httpClient, config, tenantViewerAsync, getUserTenantResolverService()) :> IGoogleBooksService

let getAuthorsSearchService () =
    let httpClient = new HttpClient()
    httpClient.DefaultRequestHeaders.Add("User-Agent", "BlazorBookLibraryTest/1.0")
    AuthorsSearchService(httpClient, tenantViewerAsync, getUserTenantResolverService()) :> IAuthorsSearchService

let getDataExportService () : IDataExportService =
    DataExportService(
        pgEventStore,
        MessageSenders.NoSender,
        bookViewerAsync,
        authorViewerAsync,
        editorViewerAsync,
        reservationViewerAsync,
        loanViewerAsync,
        userViewerAsync,
        tenantViewerAsync,
        getUserTenantResolverService(),
        getBookService(),
        getAuthorService(),
        getDetailsService(),
        getGoogleBooksService(),
        getAuthorsSearchService(),
        getTextEmbeddingService(),
        getVectorDbService()
    ) :> IDataExportService

let getTenantService () : ITenantService =
    TenantService(
        getSecretReader(),
        config,
        fakeEmailNotificator,
        getMailBodyRetriever(),
        getBookService(),
        getAuthorService(),
        getDummyLogger<ITenantService>()
    ) :> ITenantService

let getTagService () : ITagService =
    TagService(getSecretReader(), getUserTenantResolverService()) :> ITagService

let truncateVectorDb () =
    let connStr = config.GetConnectionString "VectorDbConnection"
    use conn = new NpgsqlConnection(connStr)
    conn.Open()
    use cmd = new NpgsqlCommand("TRUNCATE TABLE item_embeddings_projections", conn)
    cmd.ExecuteNonQuery() |> ignore

let setUp () =
    pgEventStore.Reset Book.Version Book.StorageName
    pgEventStore.ResetAggregateStream Book.Version Book.StorageName
    pgEventStore.Reset Author.Version Author.StorageName
    pgEventStore.ResetAggregateStream Author.Version Author.StorageName
    pgEventStore.Reset Editor.Version Editor.StorageName
    pgEventStore.ResetAggregateStream Editor.Version Editor.StorageName
    pgEventStore.Reset Reservation.Version Reservation.StorageName
    pgEventStore.ResetAggregateStream Reservation.Version Reservation.StorageName
    pgEventStore.Reset Loan.Version Loan.StorageName
    pgEventStore.ResetAggregateStream Loan.Version Loan.StorageName
    pgEventStore.Reset User.Version User.StorageName
    pgEventStore.ResetAggregateStream User.Version User.StorageName
    pgEventStore.Reset Tenant.Version Tenant.StorageName
    pgEventStore.ResetAggregateStream Tenant.Version Tenant.StorageName
    pgEventStore.Reset Tags.Version Tags.StorageName
    pgEventStore.ResetAggregateStream Tags.Version Tags.StorageName

    AggregateCache3.Instance.Clear()            
    try
        let context = getDbContext()
        context.Database.EnsureDeleted() |> ignore
        context.Database.EnsureCreated() |> ignore
        let scopeFactory = getServiceScopeFactory()
        use scope = scopeFactory.CreateScope()
        let roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
        if not (roleManager.RoleExistsAsync("Admin").Result) then
            roleManager.CreateAsync(IdentityRole("Admin")).Result |> ignore
        
        let userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
        let adminEmail = "admin@blazorbooklibrary.com"
        let aspAdmin = ApplicationUser(UserName = adminEmail, Email = adminEmail, Id = adminId.Value.ToString())
        userManager.CreateAsync(aspAdmin, "Password123!").Result |> ignore
        userManager.AddToRoleAsync(aspAdmin, "Admin").Result |> ignore

        let userService = getUserService()
        let adminUser = User.New adminId
        userService.CreateUserAsync(UserContext.Anonymous, adminUser).Result |> ignore

        let tenantService = getTenantService()
        tenantService.EnsureDefaultTenantExistsAsync(adminId).Result |> ignore

        let tagService = getTagService()
        tagService.EnsureTagsRepoCreatedAsync().Result |> ignore
    with
    | ex -> printfn "Warning: %s" ex.Message
    truncateVectorDb ()


let getMailResenderService () =
    MailResenderService(
        config,
        pgEventStore,
        getAggregateStorageFreshStateViewerAsync<BookLibrary.MessagesScheduler.MailQueue, BookLibrary.MessagesScheduler.MailQueueEvent, string> pgEventStore,
        dummyMailJetClient,
        dummyLogger
    )

let registerUser (email: string) (password: string) =
    // ensure unique email to avoid parallel test conflicts
    let guid = Guid.NewGuid()
    let guidStr = guid.ToString("N")
    let parts = email.Split('@')
    let uniqueEmail = 
        if parts.Length = 2 then
            sprintf "%s+%s@%s" parts.[0] guidStr parts.[1]
        else
            sprintf "%s_%s" guidStr email

    let userManager = getUserManager()
    let aspUser = ApplicationUser(UserName = uniqueEmail, Email = uniqueEmail)
    aspUser.Id <- guid.ToString() // ensure same ID as domain user
    let result = (userManager.CreateAsync(aspUser, password) |> Async.AwaitTask |> Async.RunSynchronously)
    if not result.Succeeded then
        failwithf "Identity user creation failed: %A" result.Errors

    let userId = UserId guid
    let userService = getUserService()
    let user = User.New userId
    let addUser = 
        userService.CreateUserAsync(adminContext, user)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    
    if not (addUser |> Result.isOk) then
        failwithf "Domain user creation failed: %A" addUser

    userId

let registerUserTask (email: string) (password: string) =
    task {
        let guid = Guid.NewGuid()
        let guidStr = guid.ToString("N")
        let parts = email.Split('@')
        let uniqueEmail = 
            if parts.Length = 2 then
                sprintf "%s+%s@%s" parts.[0] guidStr parts.[1]
            else
                sprintf "%s_%s" guidStr email

        let userManager = getUserManager()
        let aspUser = ApplicationUser(UserName = uniqueEmail, Email = uniqueEmail)
        aspUser.Id <- guid.ToString()
        let! result = userManager.CreateAsync(aspUser, password)
        if not result.Succeeded then
            failwithf "Identity user creation failed: %A" result.Errors

        let userId = UserId guid
        let userService = getUserService()
        let user = User.New userId
        let! addUser = userService.CreateUserAsync(adminContext, user)
        
        if not (addUser |> Result.isOk) then
            failwithf "Domain user creation failed: %A" addUser
        return userId
    }

let registerUserWithAdminRoleTask (email: string) (password: string) =
    task {
        let guid = Guid.NewGuid()
        let guidStr = guid.ToString("N")
        let parts = email.Split('@')
        let uniqueEmail = 
            if parts.Length = 2 then
                sprintf "%s+%s@%s" parts.[0] guidStr parts.[1]
            else
                sprintf "%s_%s" guidStr email

        let userManager = getUserManager()
        let aspUser = ApplicationUser(UserName = uniqueEmail, Email = uniqueEmail)
        aspUser.Id <- guid.ToString()
        let! result = userManager.CreateAsync(aspUser, password)
        if not result.Succeeded then
            failwithf "Identity user creation failed: %A" result.Errors

        let! roleResult = userManager.AddToRoleAsync(aspUser, "Admin")
        if not roleResult.Succeeded then
            failwithf "Adding Admin role failed: %A" roleResult.Errors

        let userId = UserId guid
        let userService = getUserService()
        let user = User.New userId
        let! addUser = userService.CreateUserAsync(adminContext, user)
        
        if not (addUser |> Result.isOk) then
            failwithf "Domain user creation failed: %A" addUser
        
        return userId
    }
