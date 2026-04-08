using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using blazorBookLibrary.Shared.Infrastructure.Services;
using blazorBookLibrary.Data;
using blazorBookLibrary.Infrastructure.Services;

namespace blazorBookLibrary.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    // private readonly IEmailSender emailSender = new NoOpEmailSender();
    // private readonly MailNotificator _mailNotificator;
    private readonly IMailNotificator _mailNotificator;
    private readonly string _emailFrom;
    private readonly string _nameFrom;
    private readonly ILogger<IdentityNoOpEmailSender> _logger;
    private string _agreementText;


    public IdentityNoOpEmailSender(IConfiguration configuration, IMailNotificator mailNotificator, ILogger<IdentityNoOpEmailSender> logger)
    {
        _mailNotificator = mailNotificator;
        _emailFrom = configuration["BooksLibrary:FromEmail"] ?? "noreply@restaurantsystem.cloud";
        _nameFrom = configuration["BooksLibrary:FromName"] ?? "Blazor Book Library";
        _logger = logger;


        var agreementFilePath = Path.Combine(Directory.GetCurrentDirectory(), "agreement.txt");
        var agreementContent =  File.ReadAllText(agreementFilePath);
        _agreementText = agreementContent;
        _logger = logger;
    }


    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) {
        _logger.LogInformation("Sending confirmation link to {Email}", email);
        _logger.LogInformation("Confirmation link: {ConfirmationLink}", confirmationLink);
        return _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Confirm your email", _agreementText.Replace("{confirmationLink}", confirmationLink).Replace("{email}", email));
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Reset your password", $"Restaurant System. Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Reset your password", $"Restaurant System. Please reset your password using the following code: {resetCode}");
}
