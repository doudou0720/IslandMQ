---
title: "Plugin"
description: "Reference for the IslandMQ plugin entry point and how it wires services into the ClassIsland host."
---

`Plugin` is the host entry point declared in `Plugin.cs`. It is not a general-purpose application API; it is the type ClassIsland loads to register services and attach the plugin to host lifecycle events.

## Type

- Fully qualified type: `IslandMQ.Plugin`
- Namespace import: `using IslandMQ;`
- Source file: `Plugin.cs`

## Signature

```csharp
[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services);
}
```

## Method Reference

### `Initialize`

```csharp
public override void Initialize(HostBuilderContext context, IServiceCollection services)
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `context` | `HostBuilderContext` | — | Host builder context from ClassIsland startup. |
| `services` | `IServiceCollection` | — | DI container registrations for the plugin runtime. |

Returns: `void`

Behavior:

- Registers `IslandMQNotificationProvider`
- Registers singleton `NetMQREQServer`
- Registers singleton `NetMQPUBServer`
- Hooks `AppBase.Current.AppStarted` to start both sockets and subscribe to lesson events
- Hooks `AppBase.Current.AppStopping` to unsubscribe lesson events and dispose both sockets

## Usage Example

ClassIsland creates this type for you, but the following sketch shows the intent:

```csharp
using IslandMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var plugin = new Plugin();
plugin.Initialize(hostContext, services);
```

## Notes

The private methods in `Plugin.cs` are operational glue rather than public extension points. The important detail for readers is that lesson events are registered only after application startup, and every host event is mapped to a simple PUB message. If you are building an external client, you usually do not consume `Plugin` directly; you consume the sockets it starts.

From a maintenance perspective, `Plugin` is also where shutdown correctness is enforced. The stop handlers detach event subscriptions before disposing the sockets, which prevents the host from publishing into a disposed publisher during teardown. That sequencing is small but important, because it is the difference between a quiet application exit and a race condition that only appears when ClassIsland is closing.
