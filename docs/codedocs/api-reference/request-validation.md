---
title: "Request Validation"
description: "Reference for the parser, schema registry, parse result model, and fatal-exception helper."
---

IslandMQ's validation layer is split across four public utility types in the `IslandMQ.Utils` namespace.

## Types

- `IslandMQ.Utils.JsonParser`
- `IslandMQ.Utils.JsonParser0`
- `IslandMQ.Utils.JsonSchemaDefinitions`
- `IslandMQ.Utils.JsonParseResult`
- `IslandMQ.Utils.ExceptionHelper`
- Namespace import: `using IslandMQ.Utils;`
- Source files: `utils/json-parser.cs`, `utils/json-parser-0.cs`, `utils/JsonSchemaDefinitions.cs`, `utils/ExceptionHelper.cs`

## `JsonParser`

```csharp
public static class JsonParser
{
    public const int MIN_SUPPORTED_VERSION = 0;
    public const int MAX_SUPPORTED_VERSION = 0;
    public static JsonParseResult Parse(string jsonString);
}
```

| Member | Type | Description |
|--------|------|-------------|
| `MIN_SUPPORTED_VERSION` | `int` | Lowest accepted protocol version. |
| `MAX_SUPPORTED_VERSION` | `int` | Highest accepted protocol version. |
| `Parse(string jsonString)` | `JsonParseResult` | Parses raw JSON text, validates the version, and dispatches to a version-specific parser. |

## `JsonParser0`

```csharp
public static class JsonParser0
{
    public static JsonParseResult Parse(JsonElement rootElement);
}
```

Validates the version `0` payload against the schema chosen by `GetSchemaForCommand`.

## `JsonSchemaDefinitions`

```csharp
public static class JsonSchemaDefinitions
{
    public static readonly JsonSchema VersionZeroSchema;
    public static readonly JsonSchema BaseRequestSchema;
    public static readonly JsonSchema PingRequestSchema;
    public static readonly JsonSchema NoticeRequestSchema;
    public static readonly JsonSchema TimeRequestSchema;
    public static readonly JsonSchema GetLessonRequestSchema;
    public static readonly JsonSchema ChangeLessonRequestSchema;
    public static readonly JsonSchema GetClassPlanRequestSchema;
    public static JsonSchema? GetSchemaForCommand(string? command);
}
```

The schema registry is the authoritative request-shape map for the protocol.

## `JsonParseResult`

```csharp
public class JsonParseResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public JsonElement? ParsedData { get; init; }
    public int? Version { get; init; }
}
```

## `ExceptionHelper`

```csharp
public static class ExceptionHelper
{
    public static bool IsFatal(Exception ex);
}
```

Returns `true` for `OutOfMemoryException` and `AccessViolationException`.

## Example

```csharp
using IslandMQ.Utils;

JsonParseResult result = JsonParser.Parse("""
{
  "version": 0,
  "command": "ping"
}
""");

Console.WriteLine(result.Success);
```

## Notes

This validation layer is public, but its primary consumer is `NetMQREQServer.ProcessMessage`. If you extend the wire protocol, update the parser constants only when you also add the matching version-specific parser branch and schema set; changing the supported range alone would make the parser claim support it does not actually implement.
