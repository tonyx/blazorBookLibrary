using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using static BookLibrary.Shared.Commons;
using blazorBookLibrary.Shared.Infrastructure.Services;

namespace blazorBookLibrary.Infrastructure.Services;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMailNotificator _mailNotificator;
    private readonly INotificationService _notificationService;
    private readonly IMailBodyRetriever _mailBodyRetriever;
    private readonly IInAppMessagesRetriever _inAppMessagesRetriever;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public NotificationDispatcher(
        IServiceProvider serviceProvider,
        IMailNotificator mailNotificator,
        INotificationService notificationService,
        IMailBodyRetriever mailBodyRetriever,
        IInAppMessagesRetriever inAppMessagesRetriever,
        IConfiguration configuration,
        ILogger<NotificationDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _mailNotificator = mailNotificator;
        _notificationService = notificationService;
        _mailBodyRetriever = mailBodyRetriever;
        _inAppMessagesRetriever = inAppMessagesRetriever;
        _logger = logger;
        _fromEmail = configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com");
        _fromName = configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library");
    }

    public async Task<FSharpResult<Unit, string>> DispatchNotificationAsync(
        UserContext context,
        UserId recipientId,
        TenantId tenantId,
        FSharpOption<string> actionUrl,
        FSharpOption<CancellationToken> ct)
    {
        var cancellationToken = ct != null && FSharpOption<CancellationToken>.get_IsSome(ct) 
            ? ct.Value 
            : CancellationToken.None;

        try
        {
            var tenantService = _serviceProvider.GetRequiredService<ITenantService>();
            var tenantResult = await tenantService.GetTenantAsync(context, tenantId, cancellationToken);
            if (tenantResult.IsError)
            {
                return FSharpResult<Unit, string>.NewError($"Failed to fetch tenant: {tenantResult.ErrorValue}");
            }
            var tenant = tenantResult.ResultValue;

            var actionUrlValue = actionUrl != null && FSharpOption<string>.get_IsSome(actionUrl) ? actionUrl.Value : null;

            var preference = tenant.ReservationNotificationPreference;
            if (actionUrlValue != null && actionUrlValue.StartsWith("/loans/", StringComparison.OrdinalIgnoreCase))
            {
                preference = tenant.LoanNotificationPreference;
            }

            string recipientEmail = null;
            if (preference.IsEmail || preference.IsInAppAndEmail)
            {
                var userService = _serviceProvider.GetRequiredService<IUserService>();
                var userDetailsResult = await userService.GetUserDetailsAsync(context, recipientId, cancellationToken);
                if (userDetailsResult.IsError)
                {
                    return FSharpResult<Unit, string>.NewError($"Failed to fetch user details: {userDetailsResult.ErrorValue}");
                }
                recipientEmail = userDetailsResult.ResultValue.AppUser.Email;
            }

            string resolvedTitle = "Notification";
            string resolvedEmailContent = "You have a new notification";
            string resolvedInAppContent = "You have a new notification";

            if (actionUrlValue != null && actionUrlValue.StartsWith("/reservations/", StringComparison.OrdinalIgnoreCase))
            {
                var reservationIdString = actionUrlValue.Substring("/reservations/".Length);
                if (Guid.TryParse(reservationIdString, out Guid resGuid))
                {
                    var reservationId = ReservationId.NewReservationId(resGuid);
                    var reservationService = _serviceProvider.GetRequiredService<IReservationService>();
                    var bookService = _serviceProvider.GetRequiredService<IBookService>();
                    var userService = _serviceProvider.GetRequiredService<IUserService>();
                    var dpService = _serviceProvider.GetRequiredService<IDistributionPointService>();

                    var resResult = await reservationService.GetReservationAsync(context, reservationId, cancellationToken);
                    if (resResult.IsOk)
                    {
                        var reservation = resResult.ResultValue;
                        var bookResult = await bookService.GetBookAsync(context, reservation.BookId, cancellationToken);
                        var userResult = await userService.GetUserUnsafeAsync(recipientId, cancellationToken);

                        if (bookResult.IsOk && userResult.IsOk)
                        {
                            var book = bookResult.ResultValue;
                            var user = userResult.ResultValue;
                            var langPref = user.LangPref;

                            string dpName = "Unspecified";
                            if (book.DistributionPoint != null && FSharpOption<DistributionPointId>.get_IsSome(book.DistributionPoint))
                            {
                                var dpResult = await dpService.GetDistributionPointAsync(context, book.DistributionPoint.Value, cancellationToken);
                                if (dpResult.IsOk)
                                {
                                    dpName = dpResult.ResultValue.Name.Value;
                                }
                            }

                            var emailTextRes = await _mailBodyRetriever.GetReservationNotificationTextMailAsync(
                                book.Title,
                                reservation.ReservationCode,
                                tenant.Name,
                                dpName,
                                langPref,
                                cancellationToken
                            );

                            var emailSubjRes = await _mailBodyRetriever.GetReservationNotificationSubject(
                                langPref,
                                cancellationToken
                            );

                            var inAppTextRes = await _inAppMessagesRetriever.GetReservationNotificationInAppAsync(
                                book.Title,
                                reservation.ReservationCode,
                                tenant.Name,
                                dpName,
                                langPref,
                                cancellationToken
                            );

                            if (emailTextRes.IsOk)
                            {
                                resolvedEmailContent = emailTextRes.ResultValue;
                            }
                            if (emailSubjRes.IsOk)
                            {
                                resolvedTitle = emailSubjRes.ResultValue.Replace("{bookTitle}", book.Title.Value);
                            }
                            if (inAppTextRes.IsOk)
                            {
                                resolvedInAppContent = inAppTextRes.ResultValue;
                            }
                        }
                    }
                }
            }
            else if (actionUrlValue != null && actionUrlValue.StartsWith("/loans/", StringComparison.OrdinalIgnoreCase))
            {
                var isRelease = actionUrlValue.EndsWith("/release", StringComparison.OrdinalIgnoreCase);
                var loanIdString = isRelease 
                    ? actionUrlValue.Substring("/loans/".Length, actionUrlValue.Length - "/loans/".Length - "/release".Length)
                    : actionUrlValue.Substring("/loans/".Length);

                if (Guid.TryParse(loanIdString, out Guid loanGuid))
                {
                    var loanId = LoanId.NewLoanId(loanGuid);
                    var loanService = _serviceProvider.GetRequiredService<ILoanService>();
                    var bookService = _serviceProvider.GetRequiredService<IBookService>();
                    var userService = _serviceProvider.GetRequiredService<IUserService>();
                    var dpService = _serviceProvider.GetRequiredService<IDistributionPointService>();

                    var loanResult = await loanService.GetLoanAsync(context, loanId, cancellationToken);
                    if (loanResult.IsOk)
                    {
                        var loan = loanResult.ResultValue;
                        var bookResult = await bookService.GetBookAsync(context, loan.BookId, cancellationToken);
                        var userResult = await userService.GetUserUnsafeAsync(recipientId, cancellationToken);

                        if (bookResult.IsOk && userResult.IsOk)
                        {
                            var book = bookResult.ResultValue;
                            var user = userResult.ResultValue;
                            var langPref = user.LangPref;

                            string dpName = "Unspecified";
                            if (book.DistributionPoint != null && FSharpOption<DistributionPointId>.get_IsSome(book.DistributionPoint))
                            {
                                var dpResult = await dpService.GetDistributionPointAsync(context, book.DistributionPoint.Value, cancellationToken);
                                if (dpResult.IsOk)
                                {
                                    dpName = dpResult.ResultValue.Name.Value;
                                }
                            }

                            if (isRelease)
                            {
                                var dpNameNonEmpty = NonEmptyName.NewNonEmptyName(dpName);
                                var returnedAt = DateTime.UtcNow;
                                if (loan.LoanStatus is LoanStatus.Returned returnedStatus)
                                {
                                    returnedAt = returnedStatus.Item;
                                }

                                var emailTextRes = await _mailBodyRetriever.GetReleaseLoanNotificationTextMailAsync(
                                    user.AppUserInfo.UserName,
                                    book.Title,
                                    loan.LoanedAt,
                                    returnedAt,
                                    tenant.Name,
                                    dpNameNonEmpty,
                                    langPref,
                                    cancellationToken
                                );

                                var emailSubjRes = await _mailBodyRetriever.GetReleaseLoanNotificationSubject(
                                    book.Title,
                                    langPref,
                                    cancellationToken
                                );

                                var inAppTextRes = await _inAppMessagesRetriever.GetReleaseLoanNotificationInAppAsync(
                                    user.AppUserInfo.UserName,
                                    book.Title,
                                    loan.LoanedAt,
                                    returnedAt,
                                    tenant.Name,
                                    dpNameNonEmpty,
                                    langPref,
                                    cancellationToken
                                );

                                if (emailTextRes.IsOk)
                                {
                                    resolvedEmailContent = emailTextRes.ResultValue;
                                }
                                if (emailSubjRes.IsOk)
                                {
                                    resolvedTitle = emailSubjRes.ResultValue;
                                }
                                if (inAppTextRes.IsOk)
                                {
                                    resolvedInAppContent = inAppTextRes.ResultValue;
                                }
                            }
                            else
                            {
                                var emailTextRes = await _mailBodyRetriever.GetLoanNotificationTextMailAsync(
                                    book.Title,
                                    tenant.Name,
                                    dpName,
                                    loan.LoanedAt,
                                    loan.DueDate,
                                    langPref,
                                    cancellationToken
                                );

                                var emailSubjRes = await _mailBodyRetriever.GetLoanNotificationSubject(
                                    book.Title,
                                    loan.DueDate,
                                    langPref
                                );

                                var inAppTextRes = await _inAppMessagesRetriever.GetLoanNotificationInAppAsync(
                                    book.Title,
                                    tenant.Name,
                                    dpName,
                                    loan.LoanedAt,
                                    loan.DueDate,
                                    langPref,
                                    cancellationToken
                                );

                                if (emailTextRes.IsOk)
                                {
                                    resolvedEmailContent = emailTextRes.ResultValue;
                                }
                                if (emailSubjRes.IsOk)
                                {
                                    resolvedTitle = emailSubjRes.ResultValue;
                                }
                                if (inAppTextRes.IsOk)
                                {
                                    resolvedInAppContent = inAppTextRes.ResultValue;
                                }
                            }
                        }
                    }
                }
            }

            if (preference.IsEmail || preference.IsInAppAndEmail)
            {
                if (!string.IsNullOrEmpty(recipientEmail))
                {
                    await _mailNotificator.SendEmailAsync(_fromEmail, _fromName, recipientEmail, resolvedTitle, resolvedEmailContent);
                }
            }

            if (preference.IsInApp || preference.IsInAppAndEmail)
            {
                var notifId = NotificationId.NewNotificationId(Guid.NewGuid());
                var notification = new Notification(
                    notifId,
                    recipientId,
                    resolvedTitle,
                    resolvedInAppContent,
                    false, // isRead
                    DateTime.UtcNow,
                    actionUrl
                );

                var createNotifResult = await _notificationService.CreateNotificationAsync(context, notification, cancellationToken);
                if (createNotifResult.IsError)
                {
                    return FSharpResult<Unit, string>.NewError($"Failed to create in-app notification: {createNotifResult.ErrorValue}");
                }
            }

            return FSharpResult<Unit, string>.NewOk(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching notification for recipient {RecipientId}", recipientId.Value);
            return FSharpResult<Unit, string>.NewError(ex.Message);
        }
    }
}
