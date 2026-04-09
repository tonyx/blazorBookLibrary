using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using blazorBookLibrary.Client.Pages;
using blazorBookLibrary.Components;
using blazorBookLibrary.Components.Account;
using blazorBookLibrary.Data;
using BookLibrary.Shared.Services;
using BookLibrary.Services;
using blazorBookLibrary.Shared.Infrastructure.Services;
using BookLibrary.Domain;

using static BookLibrary.Shared.Commons; 
using static BookLibrary.CleanServices.CleanUpServices;
using BookLibrary.CleanServices;
using BookLibrary.Server.SeedServices;

using blazorBookLibrary.Security;
using blazorBookLibrary.Infrastructure.Services;
using BookLibrary.CleanServices;
using BookLibrary.Server.CleanServices;
using Microsoft.FSharp.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<BotScoreService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddLocalization();
builder.Services.AddControllers()
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(blazorBookLibrary.Shared.Resources.SharedResources));
    });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "PLACEHOLDER_GOOGLE_ID";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "PLACEHOLDER_GOOGLE_SECRET";
    }).AddIdentityCookies();

var usersDbConnection = builder.Configuration.GetConnectionString("UsersDbConnection") ?? throw new InvalidOperationException("Connection string 'UsersDbConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(usersDbConnection));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IMailResenderService, MailResenderService>();

builder.Services.AddSingleton<IMailNotificator, MailNotificator>();

builder.Services.AddSingleton<IAuthorService, AuthorService>();
builder.Services.AddSingleton<IReservationService, ReservationService>();
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<ILoanService, LoanService>();
builder.Services.AddSingleton<IGoogleBooksService, GoogleBooksService>();
builder.Services.AddTransient<CleanUpService>();

builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddHttpClient<IAuthorsSearchService, AuthorsSearchService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BlazorBookLibrary/1.0");
});
builder.Services.AddHttpClient();

builder.Services.AddScoped<RandomAuthorGeneratorService>();
builder.Services.AddScoped<RandomBooksGeneratorService>();

// Optional: Configure Passkey options for ASP.NET Core 10 Identity WebAuthn support
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    // Configure WebAuthn options here if needed, for instance customizing the relying party or challenge
    // options.ServerDomain = "localhost"; // Example configuration
});

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddHostedService<ScheduledWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

var supportedCultures = new[] { "it-IT", "en-US", "en-GB", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("it-IT")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(blazorBookLibrary.Client._Imports).Assembly);

app.MapControllers();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Seed Admin role to a specified user from appsettings if they exist
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminUsername = app.Configuration["AdminUsername"];
    if (!string.IsNullOrWhiteSpace(adminUsername))
    {
        var adminUser = await userManager.FindByEmailAsync(adminUsername) ?? await userManager.FindByNameAsync(adminUsername);
        if (adminUser != null)
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }

    // Synchronize ASP.NET Identity users with Sharpino Domain users
    if (app.Configuration.GetValue<bool>("SyncUsersOnStartup", false))
    {
        var userService = scope.ServiceProvider.GetRequiredService<BookLibrary.Shared.Services.IUserService>();
        var allIdentityUsers = await userManager.Users.ToListAsync();

        foreach (var identityUser in allIdentityUsers)
        {
            if (Guid.TryParse(identityUser.Id, out var userGuid))
            {
                var sharpinoUserId = BookLibrary.Shared.Commons.UserId.NewUserId(userGuid);
                var sharpinoUserResult = await userService.GetUserAsync(sharpinoUserId, Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);

                if (sharpinoUserResult.IsError) // User not found in event store
                {
                    var newSharpinoUser = BookLibrary.Domain.User.New(sharpinoUserId);
                    var createResult = await userService.CreateUserAsync(newSharpinoUser, Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);
                    
                    if (createResult.IsError)
                    {
                        Console.WriteLine($"[Sync] Failed to create Sharpino user for {identityUser.UserName}: {createResult.ErrorValue}");
                    }
                    else
                    {
                        Console.WriteLine($"[Sync] Created Sharpino user record for {identityUser.UserName}");
                    }
                }
            }
        }
    }

    // Invoke CleanUpService to force snapshots on startup if configured
    // CleanUpService cleanUpService = scope.ServiceProvider.GetRequiredService<BookLibrary.CleanServices.CleanUpServices.CleanUpService>();
    var cleanUpService = scope.ServiceProvider.GetRequiredService<CleanUpService>();
    FSharpResult<Unit, string> cleanupResult = await cleanUpService.ReSnapshotOnStartup();

    if (cleanupResult.IsError)
    {
        var cleanUpLogger = scope.ServiceProvider.GetRequiredService<ILogger<CleanUpService>>();
        cleanUpLogger.LogError("Snapshot cleanup service failed: {Error}", cleanupResult.ErrorValue);
    }
    if (app.Configuration.GetValue<bool>("TestDataSeedSetup:SeedRandomAuthors", false))
    {
        var randomAuthorGeneratorService = scope.ServiceProvider.GetRequiredService<RandomAuthorGeneratorService>();
        var result = await randomAuthorGeneratorService.SeedRandomAuthorsAccordingToThreshold();
        if (result.IsError)
        {
            var randomAuthorGeneratorLogger = scope.ServiceProvider.GetRequiredService<ILogger<RandomAuthorGeneratorService>>();
            randomAuthorGeneratorLogger.LogError("Random author generator service failed: {Error}", result.ErrorValue);
        }
    }
    if (app.Configuration.GetValue<bool>("TestDataSeedSetup:SeedRandomBooks", false))
    {
        var randomBookGeneratorService = scope.ServiceProvider.GetRequiredService<RandomBooksGeneratorService>();
        var result = await randomBookGeneratorService.SeedRandomBooksAccordingToThreshold();
        if (result.IsError)
        {
            var randomBookGeneratorLogger = scope.ServiceProvider.GetRequiredService<ILogger<RandomBooksGeneratorService>>();
            randomBookGeneratorLogger.LogError("Random book generator service failed: {Error}", result.ErrorValue);
        }
    }

    var mailResenderService = scope.ServiceProvider.GetRequiredService<IMailResenderService>();
    await mailResenderService.CreateInitialMailQueueInstanceAsync(Microsoft.FSharp.Core.FSharpOption<System.Threading.CancellationToken>.None);

}

app.Run();

