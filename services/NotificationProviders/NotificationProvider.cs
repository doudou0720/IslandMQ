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

    /// <summary>
    /// 初始化 IslandMQNotificationProvider 实例并开始监听来自 ClassIsland 的通知事件。
    /// </summary>
    /// <remarks>
    /// 可选地接受一个日志记录器并将其保存在实例中，然后订阅 ClassIslandAPIHelper.NotificationRequested 以响应传入的通知请求。
    /// </remarks>
    public IslandMQNotificationProvider(ILogger<IslandMQNotificationProvider>? logger = null)
    {
        _logger = logger;
        ClassIslandAPIHelper.NotificationRequested += OnNotificationRequested;
    }

    /// <summary>
    /// 处理 NotificationRequested 事件，在 Avalonia UI 线程上显示通知：创建一个基于事件标题的遮罩（mask）并根据事件提供的时长校验后设置其持续时间，必要时还会添加一个显示事件消息的覆盖层（overlay）。
    /// </summary>
    /// <param name="sender">事件的触发者，可能为 null。</param>
    /// <param name="e">包含通知数据的 <see cref="NotificationEventArgs"/>，其字段包括 Title（通知标题）、Message（覆盖层文本）、MaskDuration（遮罩持续时间，单位秒）和 OverlayDuration（覆盖层持续时间，单位秒）。时长会被限制到合理范围；当 OverlayDuration 小于或等于 0 时不会创建覆盖层。</param>
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