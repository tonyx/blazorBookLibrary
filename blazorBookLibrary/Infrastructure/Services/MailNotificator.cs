// See https://aka.ms/new-console-template for more information

using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Mailjet.Client.TransactionalEmails.Response;
using Microsoft.AspNetCore.Identity.UI.Services;
using blazorBookLibrary.Shared.Infrastructure.Services;
using static BookLibrary.Shared.Commons;
using BookLibrary.MessagesScheduler;
using BookLibrary.CleanServices;
using Farmer.Builders;

namespace blazorBookLibrary.Infrastructure.Services;

public sealed partial class MailNotificator : IMailNotificator
{
    private readonly MailjetClient _mailjetClient;
    private readonly ILogger<MailNotificator> _logger;
    private readonly IConfiguration _config;
    private readonly bool _isEmailSendEnabled;
    private readonly IMailResenderService _mailResenderService;

    public MailNotificator(IConfiguration config, ILogger<MailNotificator> logger, IMailResenderService mailResenderService)
    {
        this._mailResenderService = mailResenderService;
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

                _isEmailSendEnabled = _config.GetValue<bool>("BooksLibrary:EmailNotificationEnabled", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MailNotificator");
                throw;
            }
    }
    
    public async Task SendEmailAsync(string emailFrom, string nameFrom, string emailRecipient, string subject, string body)
    {
        if (!_isEmailSendEnabled) // can be disabled only in case of development
        {
            _logger.LogWarning("Email sending is disabled. Some features may not work. Particularly the user will be unable to log at all in case Program.cs uses options.SignIn.RequireConfirmedAccount = true");
            return;
        }
        
        var email = new TransactionalEmailBuilder()
            .WithFrom(from: new SendContact(emailFrom, nameFrom))
            .WithSubject(subject)
            .WithHtmlPart(body)
            .WithTo(new SendContact(emailRecipient))
            .Build();

        LogSendingEmail(emailRecipient);
        try
        {
            TransactionalEmailResponse response = await _mailjetClient.SendTransactionalEmailAsync(email);
            if (response is null)
            {
                LogEmailSendFailed(emailRecipient);

                await _mailResenderService.AddMailQueueItemAsync(MailQueueItem.New(email));
                throw new InvalidOperationException($"Failed to send email to {emailRecipient}");
            }
            LogEmailSent(emailRecipient);
        }
        catch (Exception ex)
        {
            LogEmailSendError(ex, emailRecipient);
            await _mailResenderService.AddMailQueueItemAsync(MailQueueItem.New(email));
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending email to {EmailRecipient}")]
    private partial void LogSendingEmail(string emailRecipient);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent to {EmailRecipient}")]
    private partial void LogEmailSent(string emailRecipient);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email to {EmailRecipient}")]
    private partial void LogEmailSendFailed(string emailRecipient);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email to {EmailRecipient}")]
    private partial void LogEmailSendError(Exception ex, string emailRecipient);
}
    