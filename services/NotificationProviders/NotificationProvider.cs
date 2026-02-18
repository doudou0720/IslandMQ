using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using IslandMQ.Utils;
using Microsoft.Extensions.Logging;

namespace IslandMQ.Services.NotificationProviders;

[NotificationProviderInfo("339502CA-8BE7-FFD9-474E-DBDBFA910E1D", "IslandMQ 提醒", "\uE708", "通过 IslandMQ 接收并显示提醒")]
public class IslandMQNotificationProvider : NotificationProviderBase
{
    private readonly ILogger<IslandMQNotificationProvider>? _logger;

    public IslandMQNotificationProvider(ILogger<IslandMQNotificationProvider>? logger = null)
    {
        _logger = logger;
        ClassIslandAPIHelper.NotificationRequested += OnNotificationRequested;
    }

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        _logger?.LogDebug("OnNotificationRequested - e.Title: {Title}, e.Message: {Message}, e.MaskDuration: {MaskDuration}, e.OverlayDuration: {OverlayDuration}", e.Title, e.Message, e.MaskDuration, e.OverlayDuration);
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _logger?.LogDebug("Showing notification - mask title: {Title}, overlay message: {Message}", e.Title, e.Message);
            
            double ClampDuration(double duration, double min = 0.0, double max = 3600.0)
            {
                if (!double.IsFinite(duration))
                {
                    _logger?.LogWarning("Invalid duration value: {Duration}, using default", duration);
                    return Math.Max(min, 0.1);
                }
                if (duration < min)
                {
                    _logger?.LogWarning("Duration {Duration} is less than minimum {MinDuration}, clamping to {MinDuration}", duration, min, min);
                    return min;
                }
                if (duration > max)
                {
                    _logger?.LogWarning("Duration {Duration} exceeds maximum {MaxDuration}, clamping to {MaxDuration}", duration, max, max);
                    return max;
                }
                return duration;
            }
            
            double safeMaskDuration = ClampDuration(e.MaskDuration, min: 0.1);
            double safeOverlayDuration = ClampDuration(e.OverlayDuration, min: 0.0);
            
            var mask = NotificationContent.CreateTwoIconsMask(e.Title);
            mask.Duration = TimeSpan.FromSeconds(safeMaskDuration);
            
            var notice = new NotificationRequest
            {
                MaskContent = mask
            };
            
            if (safeOverlayDuration > 0.0)
            {
                notice.OverlayContent = NotificationContent.CreateSimpleTextContent(e.Message);
                notice.OverlayContent.Duration = TimeSpan.FromSeconds(safeOverlayDuration);
            }
            
            ShowNotification(notice);
        });
    }
}
