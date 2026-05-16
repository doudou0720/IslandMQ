---
title: "ClassIslandAPIHelper"
description: "Reference for the command dispatcher, result records, and JSON-facing operations."
---

`ClassIslandAPIHelper` is the public static dispatcher in `utils/ClassIslandAPIHelper.cs`. External clients do not import it directly, but every REQ command flows through it.

## Type

- Fully qualified type: `IslandMQ.Utils.ClassIslandAPIHelper`
- Namespace import: `using IslandMQ.Utils;`
- Source file: `utils/ClassIslandAPIHelper.cs`

## Signature

```csharp
public static class ClassIslandAPIHelper
```

### Event

```csharp
public static event EventHandler<NotificationEventArgs>? NotificationRequested;
```

Raised by `Notice(JsonElement parsedData)` and consumed by `IslandMQNotificationProvider`.

### Public Methods

```csharp
public static ApiHelperResult ProcessRequest(JsonElement parsedData);
public static ApiHelperResult Ping();
public static ApiHelperResult Time();
public static ApiHelperResult Notice(JsonElement parsedData);
public static ApiHelperResult GetLesson();
public static ApiHelperResult ChangeLesson(JsonElement parsedData);
public static ApiHelperResult GetClassPlanByDate(JsonElement parsedData);
```

| Method | Return type | Description |
|--------|-------------|-------------|
| `ProcessRequest(JsonElement parsedData)` | `ApiHelperResult` | Dispatches a validated command to the right handler. |
| `Ping()` | `ApiHelperResult` | Returns status code `200` with message `OK`. |
| `Time()` | `ApiHelperResult` | Returns local time drift against `IExactTimeService`. |
| `Notice(JsonElement parsedData)` | `ApiHelperResult` | Parses notice arguments and emits `NotificationRequested`. |
| `GetLesson()` | `ApiHelperResult` | Returns a runtime lesson snapshot with enriched class and subject data. |
| `ChangeLesson(JsonElement parsedData)` | `ApiHelperResult` | Applies overlay-aware schedule mutations. |
| `GetClassPlanByDate(JsonElement parsedData)` | `ApiHelperResult` | Returns a dated class plan projection that pairs class slots with subject details. |

## Result Records

```csharp
public record ApiHelperResult<T>
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
}

public record ApiHelperResult : ApiHelperResult<object>;
```

## Example

```csharp
using IslandMQ.Utils;

ApiHelperResult result = ClassIslandAPIHelper.Ping();
Console.WriteLine($"{result.StatusCode}: {result.Message}");
```

## Combined Example

This sketch shows how the helper is meant to be used internally after JSON parsing.

```csharp
using System.Text.Json;
using IslandMQ.Utils;

JsonElement parsed = JsonDocument.Parse("""
{
  "version": 0,
  "command": "time"
}
""").RootElement.Clone();

ApiHelperResult result = ClassIslandAPIHelper.ProcessRequest(parsed);
Console.WriteLine(result.Message);
```

## Notes

`ProcessRequest` is intentionally the only place that maps command names to handlers. When you add a new command, you need to update this switch and the schema map in `JsonSchemaDefinitions`; otherwise a valid new handler would remain unreachable from the wire protocol.
