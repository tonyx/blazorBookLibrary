// See https://aka.ms/new-console-template for more information

using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Mailjet.Client.TransactionalEmails.Response;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace blazorBookLibrary.Infrastructure.Services;

public sealed class MailNotificator
{
    private readonly MailjetClient _mailjetClient;
    private readonly ILogger<MailNotificator> _logger;
    private readonly IConfiguration _config;
    private readonly bool _isEmailSendEnabled;

    public MailNotificator(IConfiguration config, ILogger<MailNotificator> logger)
    {
        try
            {
                _config = config;
                _logger = logger;
                var mailjetApiKey = config["Mailjet:ApiKey"] ?? throw new InvalidOperationException("Mailjet API key not found.");
                var mailjetSecretKey = config["Mailjet:SecretKey"] ?? throw new InvalidOperationException("Mailjet secret key not found.");

                _mailjetClient = new MailjetClient(
                    mailjetApiKey,
                    mailjetSecretKey
                );

                _isEmailSendEnabled = _config.GetValue<bool>("BooksLibrary:EmailNotificationEnabled", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MailNotificator");
                throw;
            }
    }
    
    public async Task SendEmailAsync(string emailFrom, string nameFrom, string emailRecipient, string subject, string body)
    {
        if (!_isEmailSendEnabled)
        {
            return;
        }
        
        var email = new TransactionalEmailBuilder()
            .WithFrom(from: new SendContact(emailFrom, nameFrom))
            .WithSubject(subject)
            .WithHtmlPart(body)
            .WithTo(new SendContact(emailRecipient))
            .Build();

        _logger.LogInformation("Sending email to {EmailRecipient}", emailRecipient);
        try
        {
            TransactionalEmailResponse response = await _mailjetClient.SendTransactionalEmailAsync(email);
            if (response is null)
            {
                _logger.LogError("Failed to send email to {EmailRecipient}", emailRecipient);
                throw new InvalidOperationException($"Failed to send email to {emailRecipient}");
            }
            _logger.LogInformation("Email sent to {EmailRecipient}", emailRecipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {EmailRecipient}", emailRecipient);
            throw;
        }

        _logger.LogInformation("Email sent to {EmailRecipient}", emailRecipient);
        return;
    }
}
    