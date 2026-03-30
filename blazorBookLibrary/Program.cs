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
using BookLibrary.Domain;
using static BookLibrary.Shared.Commons;
// using static BookLibrary.Application.ServiceLayer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddLocalization();
builder.Services.AddControllers();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var usersDbConnection = builder.Configuration.GetConnectionString("UsersDbConnection") ?? throw new InvalidOperationException("Connection string 'UsersDbConnection' not found.");

var bookLibraryDbConnection = builder.Configuration.GetConnectionString("BookLibraryDbConnection") ?? throw new InvalidOperationException("Connection string 'BookLibraryDbConnection' not found.");

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseSqlite(connectionString));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(usersDbConnection));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();


builder.Services.AddScoped<IAuthorService, AuthorService>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IGoogleBooksService, GoogleBooksService>();
builder.Services.AddScoped<IUserService>(sp => new UserService(bookLibraryDbConnection));
builder.Services.AddHttpClient<IAuthorsSearchService, AuthorsSearchService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "BlazorBookLibrary/1.0");
});
builder.Services.AddHttpClient();

// Optional: Configure Passkey options for ASP.NET Core 10 Identity WebAuthn support
builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    // Configure WebAuthn options here if needed, for instance customizing the relying party or challenge
    // options.ServerDomain = "localhost"; // Example configuration
});

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

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
}

app.Run();

