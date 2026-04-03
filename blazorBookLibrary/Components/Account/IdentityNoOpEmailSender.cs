using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using blazorBookLibrary.Data;
using blazorBookLibrary.Infrastructure.Services;

namespace blazorBookLibrary.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();
    private readonly MailNotificator _mailNotificator;
    private readonly string _emailFrom;
    private readonly string _nameFrom;
    private readonly ILogger<IdentityNoOpEmailSender> _logger;
    private string _fistSubscriptionEmailText;


    public IdentityNoOpEmailSender(IConfiguration configuration, MailNotificator mailNotificator, ILogger<IdentityNoOpEmailSender> logger)
    {
        _mailNotificator = mailNotificator;
        _emailFrom = configuration["Mail:FromEmail"] ?? "[EMAIL_ADDRESS]";
        _nameFrom = configuration["Mail:FromName"] ?? "Blazor Book Library";
        _logger = logger;
        var agreementFilePath = Path.Combine(Directory.GetCurrentDirectory(), "agreement.txt");
    }


    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
}
