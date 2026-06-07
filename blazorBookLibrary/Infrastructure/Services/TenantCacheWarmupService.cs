using System;
using System.Threading;
using System.Threading.Tasks;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using Microsoft.FSharp.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using blazorBookLibrary.Data;
using System.Linq;

namespace blazorBookLibrary.Infrastructure.Services
{
    public class TenantCacheWarmupService : ITenantCacheWarmupService
    {
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly ILogger<TenantCacheWarmupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public TenantCacheWarmupService(
            IBookService bookService,
            IAuthorService authorService,
            ILogger<TenantCacheWarmupService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _bookService = bookService;
            _authorService = authorService;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task WarmupTenantAsync(Commons.TenantId tenantId, CancellationToken ct)
        {
            _logger.LogInformation("Warming up cache for tenant {TenantId}...", tenantId.Value);
            try
            {
                Guid adminGuid = Guid.Empty;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var admins = await userManager.GetUsersInRoleAsync("Admin");
                    var firstAdmin = admins.FirstOrDefault();
                    if (firstAdmin != null && Guid.TryParse(firstAdmin.Id, out var parsedGuid))
                    {
                        adminGuid = parsedGuid;
                    }
                    else
                    {
                        _logger.LogWarning("No admin user found in ASP.NET Identity database. Falling back to Guid.Empty.");
                    }
                }

                var systemUserId = Commons.UserId.NewUserId(adminGuid);
                var roles = Microsoft.FSharp.Collections.FSharpList<Commons.Role>.Cons(
                    Commons.Role.Admin,
                    Microsoft.FSharp.Collections.FSharpList<Commons.Role>.Empty);
                var systemContext = Commons.UserContext.NewAuthenticated(systemUserId, roles);

                systemContext = systemContext.WithNewTenant(tenantId);

                var fsharpCt = FSharpOption<CancellationToken>.Some(ct);

                // Run prefetching of books and authors in parallel
                var booksTask = _bookService.GetAllBooksOfTenantAsync(systemContext, tenantId, fsharpCt);
                var authorsTask = _authorService.GetAllAuthorsOfTenantAsync(systemContext, tenantId, fsharpCt);

                await Task.WhenAll(booksTask, authorsTask);

                var booksResult = await booksTask;
                var authorsResult = await authorsTask;

                if (booksResult.IsError)
                {
                    _logger.LogWarning("Failed to warm up books cache for tenant {TenantId}: {Error}", tenantId.Value, booksResult.ErrorValue);
                }
                else
                {
                    _logger.LogInformation("Successfully warmed up books cache for tenant {TenantId}.", tenantId.Value);
                }

                if (authorsResult.IsError)
                {
                    _logger.LogWarning("Failed to warm up authors cache for tenant {TenantId}: {Error}", tenantId.Value, authorsResult.ErrorValue);
                }
                else
                {
                    _logger.LogInformation("Successfully warmed up authors cache for tenant {TenantId}.", tenantId.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cache warmup for tenant {TenantId}.", tenantId.Value);
            }
        }
    }
}



