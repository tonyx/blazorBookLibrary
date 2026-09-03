using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using blazorBookLibrary.Shared.Infrastructure.Services;
using blazorBookLibrary.Data;
using blazorBookLibrary.Infrastructure.Services;
using BookLibrary.Shared;
using BookLibrary.Shared.Services;

namespace blazorBookLibrary.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IMailNotificator _mailNotificator;
    private readonly string _emailFrom;
    private readonly string _nameFrom;
    private readonly ILogger<IdentityNoOpEmailSender> _logger;
    private readonly IMailBodyRetriever _mailBodyRetriever;
    private string _agreementText;

    public IdentityNoOpEmailSender(IConfiguration configuration, IMailNotificator mailNotificator, ILogger<IdentityNoOpEmailSender> logger, IMailBodyRetriever mailBodyRetriever)
    {
        _mailNotificator = mailNotificator;
        _mailBodyRetriever = mailBodyRetriever;
        _emailFrom = configuration["BooksLibrary:FromEmail"] ?? "noreply@biblionet.eu";
        _nameFrom = configuration["BooksLibrary:FromName"] ?? "Biblio Net";
        _logger = logger;

        // var agreementFilePath = Path.Combine(Directory.GetCurrentDirectory(), "agreement.txt");
        // var agreementContent =  File.ReadAllText(agreementFilePath);
        _agreementText = "agreement";
    }

    private async Task<string> LoadAgreementText(CultureInfo culture)
    {
        var locale = culture.TwoLetterISOLanguageName;
        var shortLang = Commons.ShortLang.NewShortLocale(locale);
        var mailBody = await _mailBodyRetriever.GetAgreementTextMailAsync(shortLang);
        if (mailBody.IsError)
        {
            _logger.LogWarning("Unable to retrieve agreement text from retriever, using fallback.");
            return _agreementText;
        }
        else
        {
            return mailBody.ResultValue;
        }
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) {
        _logger.LogInformation("Sending confirmation link to {Email}", email);
        _logger.LogInformation("Confirmation link: {ConfirmationLink}", confirmationLink);
        var agreementText = await LoadAgreementText(CultureInfo.CurrentCulture);
        await _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Confirm your email", agreementText.Replace("{confirmationLink}", confirmationLink).Replace("{email}", email));
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Reset your password", $"Biblio Net. Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        _mailNotificator.SendEmailAsync(_emailFrom, _nameFrom, email, "Reset your password", $"Biblio Net. Please reset your password using the following code: {resetCode}");
}
