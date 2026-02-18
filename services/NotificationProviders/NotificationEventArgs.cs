using System;
using ClassIsland.Core.Models.Notification;

namespace IslandMQ.Services.NotificationProviders;

public class NotificationEventArgs : EventArgs
{
    public string Title { get; }
    public string Message { get; }
    public double MaskDuration { get; }
    public double OverlayDuration { get; }

    /// <summary>
    /// 表示用于通知事件的数据载体，包括标题、消息以及可选的遮罩和覆盖显示时长。
    /// </summary>
    /// <param name="title">通知标题；不能为空或仅由空白字符组成。</param>
    /// <param name="message">通知正文；不能为空。</param>
    /// <param name="maskDuration">遮罩显示时长（秒），必须大于或等于 0。</param>
    /// <param name="overlayDuration">覆盖层显示时长（秒），必须大于或等于 0。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="title"/> 或 <paramref name="message"/> 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当 <paramref name="title"/> 为空或仅由空白字符组成时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="maskDuration"/> 或 <paramref name="overlayDuration"/> 为负值时抛出。</exception>
    public NotificationEventArgs(string title, string message, double maskDuration = 3.0, double overlayDuration = 5.0)
    {
        if (title == null)
            throw new ArgumentNullException(nameof(title));
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title cannot be empty or whitespace", nameof(title));
        if (maskDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(maskDuration), "maskDuration cannot be negative");
        if (overlayDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(overlayDuration), "overlayDuration cannot be negative");

        Title = title;
        Message = message;
        MaskDuration = maskDuration;
        OverlayDuration = overlayDuration;
    }
}