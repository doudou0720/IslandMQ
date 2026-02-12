using System;
using ClassIsland.Core.Models.Notification;

namespace IslandMQ.Services.NotificationProviders;

public class NotificationEventArgs : EventArgs
{
    public string Title { get; }
    public string Message { get; }
    public double MaskDuration { get; }
    public double OverlayDuration { get; }

    public NotificationEventArgs(string title, string message, double maskDuration = 3.0, double overlayDuration = 5.0)
    {
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
