using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace blazorBookLibrary.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    [Required]
    [MaxLength(16)]
    public string CodiceFiscale { get; set; } = string.Empty;

    // Altri campi utili per una biblioteca
    public bool IsIdentifiedPhysically { get; set; } = false;
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
}

