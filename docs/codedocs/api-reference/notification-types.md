---
title: "Notification Types"
description: "Reference for the notification event payload and the provider that turns notice requests into native ClassIsland UI notifications."
---

IslandMQ's notification path has two public types under `services/NotificationProviders/`: `NotificationEventArgs` and `IslandMQNotificationProvider`.

## Types

- `IslandMQ.Services.NotificationProviders.NotificationEventArgs`
- `IslandMQ.Services.NotificationProviders.IslandMQNotificationProvider`
- Namespace import: `using IslandMQ.Services.NotificationProviders;`
- Source files: `services/NotificationProviders/NotificationEventArgs.cs`, `services/NotificationProviders/NotificationProvider.cs`

## `NotificationEventArgs`

```csharp
public class NotificationEventArgs : EventArgs
{
    public string Title { get; }
    public string Message { get; }
    public double MaskDuration { get; }
    public double OverlayDuration { get; }

    public NotificationEventArgs(
        string title,
        string message,
        double maskDuration = 3.0,
        double overlayDuration = 5.0
    );
}
```

### Constructor Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `title` | `string` | — | Notification title. Cannot be null, empty, or whitespace. |
| `message` | `string` | — | Notification body text. Cannot be null. |
| `maskDuration` | `double` | `3.0` | Mask display duration in seconds. Must be non-negative. |
| `overlayDuration` | `double` | `5.0` | Overlay text duration in seconds. Must be non-negative. |

## `IslandMQNotificationProvider`

```csharp
[NotificationProviderInfo(...)]
public class IslandMQNotificationProvider : NotificationProviderBase
{
    public IslandMQNotificationProvider(ILogger<IslandMQNotificationProvider>? logger = null);
}
```

### Constructor Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `logger` | `ILogger<IslandMQNotificationProvider>?` | `null` | Optional logger used during notification handling. |

## Example

```csharp
using IslandMQ.Services.NotificationProviders;

var args = new NotificationEventArgs(
    title: "Morning briefing",
    message: "Bring the lab worksheet",
    maskDuration: 2.0,
    overlayDuration: 6.0
);
```

## Notes

`IslandMQNotificationProvider` subscribes to `ClassIslandAPIHelper.NotificationRequested` in its constructor and renders notifications on the Avalonia UI thread. Inside `OnNotificationRequested`, both durations are clamped before `NotificationRequest` is shown, which protects the host UI from invalid or extreme duration values even if a remote client sends surprising input.

One subtle point in this path is that `NotificationEventArgs` performs argument validation before the provider ever sees the request. That means obviously bad inputs such as a blank title or negative duration fail at the event payload boundary, while extreme but numeric durations are clamped later by the provider. The split is useful: constructors guard correctness, and UI code guards presentation.
