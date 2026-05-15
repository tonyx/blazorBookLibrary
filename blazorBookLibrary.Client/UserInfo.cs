namespace blazorBookLibrary.Client;

// Add the UserInfo class to hold the minimal data needed for the client-side ClaimsPrincipal
public class UserInfo
{
    public required string UserId { get; set; }
    public required string Email { get; set; }
    public string? UserName { get; set; }
    public string[]? Roles { get; set; }
}
