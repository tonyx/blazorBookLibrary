using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;


namespace blazorBookLibrary.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var adminRoleId = "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d";
        var managerRoleId = "b2c3d4e5-f6a7-4b6c-9d0e-1f2a3b4c5d6e";
        var userRoleId = "c3d4e5f6-a7b8-4c7d-0e1f-2a3b4c5d6e7f";

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = adminRoleId,
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "a1b2c3d4-e5f6-4a5b-8c9d-111111111111"
            },
            new IdentityRole
            {
                Id = managerRoleId,
                Name = "Manager",
                NormalizedName = "MANAGER",
                ConcurrencyStamp = "b2c3d4e5-f6a7-4b6c-9d0e-222222222222"
            },
            new IdentityRole
            {
                Id = userRoleId,
                Name = "User",
                NormalizedName = "USER",
                ConcurrencyStamp = "c3d4e5f6-a7b8-4c7d-0e1f-333333333333"
            }
        );

    }
}

