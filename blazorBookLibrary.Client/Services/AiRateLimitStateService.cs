using System;
using System.Threading;
using System.Threading.Tasks;

namespace blazorBookLibrary.Client.Services;

public class AiRateLimitStateService : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    public bool IsRateLimited { get; private set; }
    public bool IsGuest { get; private set; }
    public int RemainingSeconds { get; private set; }
    public string? Message { get; private set; }

    public event Action? OnChange;

    public AiRateLimitStateService()
    {
        ServiceClientHelper.OnRateLimited += HandleRateLimitNotification;
    }

    public void Trigger(bool isGuest, int retryAfterSeconds, string? message = null)
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            IsRateLimited = true;
            IsGuest = isGuest;
            RemainingSeconds = retryAfterSeconds > 0 ? retryAfterSeconds : 60;
            Message = message;
        }

        NotifyStateChanged();

        var token = _cts.Token;
        _ = RunCountdownAsync(token);
    }

    private void HandleRateLimitNotification(AiRateLimitNotification notification)
    {
        Trigger(notification.IsGuest, notification.RetryAfterSeconds, notification.Message);
    }

    private async Task RunCountdownAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
                
                lock (_lock)
                {
                    if (token.IsCancellationRequested) return;

                    if (RemainingSeconds > 1)
                    {
                        RemainingSeconds--;
                    }
                    else
                    {
                        RemainingSeconds = 0;
                        IsRateLimited = false;
                        NotifyStateChanged();
                        return;
                    }
                }

                NotifyStateChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when reset or re-triggered
        }
    }

    public void Dismiss()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            IsRateLimited = false;
            RemainingSeconds = 0;
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke();
        }
        catch
        {
            // Ignore subscriber exceptions
        }
    }

    public void Dispose()
    {
        ServiceClientHelper.OnRateLimited -= HandleRateLimitNotification;
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
