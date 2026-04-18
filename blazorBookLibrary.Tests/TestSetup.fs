
module TestSetup

open System
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
    "Host=127.0.0.1;Database=sharpino_booklibrary_test;Username=postgres;Password=postgres"

let pgEventStore:Sharpino.Storage.IEventStore<string> = PgEventStore connection

let usersDbConnection = config.GetConnectionString("UsersDbConnection")

let getDbContext () =
    let options = 
        DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(usersDbConnection)
            .Options
    new ApplicationDbContext(options)

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
    AggregateCache3.Instance.Clear()            
    try
        let context = getDbContext()
        context.Database.EnsureDeleted() |> ignore
        context.Database.EnsureCreated() |> ignore
    with
    | ex -> printfn "Warning: Could not wipe identity database: %s" ex.Message

let getServiceScopeFactory () =
    let services = ServiceCollection()
    services.AddLogging() |> ignore
    services.AddDataProtection() |> ignore
    services.AddDbContext<ApplicationDbContext>(fun options -> 
        options.UseNpgsql(usersDbConnection) |> ignore) |> ignore
    services.AddIdentityCore<ApplicationUser>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
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

let fakeEmailNotificator: IMailNotificator = new FakeEmailNotificator()
let fakeReservationService: IReservationService = new FakeReservationService()
let fakeLocalizer: IStringLocalizer<SharedResources> = new FakeLocalizer<SharedResources>()

let dummyLogger = 
    LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore).CreateLogger<MailResenderService>()
let dummyMailJetClient = new Mailjet.Client.MailjetClient("", "")

let getAuthorService () = 
    AuthorService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        getSecretReader())

let getReviewService () =
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
        getServiceScopeFactory())

let getUserService () =
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
        getReviewService(),
        getServiceScopeFactory())

let getReservationService () =
    ReservationService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        getUserService(),
        fakeEmailNotificator,
        3,
        "noreply@blazorbooklibrary.com",
        "Blazor Book Library",
        getMailBodyRetriever())

let getLoanService () =
    LoanService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        getReservationService(),
        getUserService(),
        fakeEmailNotificator,
        3,
        "noreply@blazorbooklibrary.com",
        "Blazor Book Library",
        fakeLocalizer,
        getMailBodyRetriever())

let getBookService () = 
    BookService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync)

let getDetailsService () =
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
        getLoanService(),
        getReservationService(),
        getServiceScopeFactory())

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
        userService.CreateUserAsync user
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
        let! addUser = userService.CreateUserAsync user
        
        if not (addUser |> Result.isOk) then
            failwithf "Domain user creation failed: %A" addUser

        return userId
    }
