using System.Globalization;
using Microsoft.JSInterop;
using blazorBookLibrary.Shared.Security;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using blazorBookLibrary.Client.Services;
using BookLibrary.Shared.Services;

namespace blazorBookLibrary.Client;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddLocalization();
        builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        // API Services
        builder.Services.AddScoped<IAuthorService, AuthorClientService>();
        builder.Services.AddScoped<IBookService, BookClientService>();
        builder.Services.AddScoped<IAuthorsSearchService, AuthorsSearchClientService>();
        builder.Services.AddScoped<IDistributionPointService, DistributionPointsClientService>();
        builder.Services.AddScoped<IReviewService, ReviewsClientService>();
        builder.Services.AddScoped<ITagService, TagsClientService>();
        builder.Services.AddScoped<IUserService, UsersClientService>();
        builder.Services.AddScoped<IReservationService, ReservationsClientService>();
        builder.Services.AddScoped<ILoanService, LoansClientService>();
        builder.Services.AddScoped<IAdminServices, AdminClientService>();
        builder.Services.AddScoped<IGoogleBooksService, GoogleBooksClientService>();
        builder.Services.AddScoped<IDataExportService, DataExportClientService>();
        builder.Services.AddScoped<ITextEmbeddingService, TextEmbeddingClientService>();
        builder.Services.AddScoped<IDetailsService, DetailsClientService>();
        builder.Services.AddScoped<IBotScoreService, BotScoreClientService>();
        builder.Services.AddScoped<IVectorDbService, VectorDbClientService>();
        builder.Services.AddScoped<ITenantService, TenantClientService>();
        builder.Services.AddScoped<IUserTenantResolverService, UserTenantResolverClientService>();
        builder.Services.AddScoped<TenantStateService>();

        var host = builder.Build();

        var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
        var cultureResult = await jsRuntime.InvokeAsync<string>("blazorCulture.get");

        if (cultureResult != null)
        {
            var culture = new CultureInfo(cultureResult);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        await host.RunAsync();
    }
}
