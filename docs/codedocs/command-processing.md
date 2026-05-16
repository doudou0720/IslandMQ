---
title: "Command Processing"
description: "See how IslandMQ maps commands to ClassIsland services and normalizes results."
---

Command processing is the business-logic core of IslandMQ. `ClassIslandAPIHelper.ProcessRequest` in `utils/ClassIslandAPIHelper.cs` reads the validated `command` field and routes it to one of six handlers: `Ping`, `Notice`, `Time`, `GetLesson`, `ChangeLesson`, or `GetClassPlanByDate`.

## Why This Exists

The REQ server should not know anything about lessons, notifications, or profile overlays. Its job is transport. `ClassIslandAPIHelper` exists to decouple socket handling from host integration, so the same dispatcher can talk to `ILessonsService`, `IProfileService`, and `IExactTimeService` without transport code leaking into every feature.

## Command Map

| Command | Handler | Main dependency | Result shape |
|---------|---------|-----------------|--------------|
| `ping` | `Ping()` | none | `"OK"` message |
| `notice` | `Notice(JsonElement parsedData)` | `NotificationRequested` event | success or failure message |
| `time` | `Time()` | `IExactTimeService` | time drift in milliseconds |
| `get_lesson` | `GetLesson()` | `ILessonsService`, `IProfileService` | current runtime lesson projection |
| `change_lesson` | `ChangeLesson(JsonElement parsedData)` | `ClassChangeService` | mutation status |
| `get_classplan` | `GetClassPlanByDate(JsonElement parsedData)` | `ILessonsService`, `IProfileService` | dated class plan projection |

## Basic Example

A notice command is just a structured request with positional and flag-like arguments.

```json
{
  "version": 0,
  "command": "notice",
  "args": [
    "Morning briefing",
    "--context=Bring the lab worksheet",
    "--mask-duration=2.5",
    "--overlay-duration=8.0"
  ]
}
```

## Advanced Example

The `get_classplan` response only includes class time slots with their attached class data, not break rows. That projection is built in `GetClassPlanByDate` by filtering `TimeLayout.Layouts` to `TimeType == 0` and then matching those rows back to enriched classes.

```python
import json
import zmq

ctx = zmq.Context()
sock = ctx.socket(zmq.REQ)
sock.connect("tcp://127.0.0.1:5555")

sock.send_string(json.dumps({
    "version": 0,
    "command": "get_classplan",
    "date": "2026-05-16"
}))

response = json.loads(sock.recv_string())
for slot in response["data"]["ClassPlan"]["TimeLayout"]["Layouts"]:
    print(slot["StartTime"], slot["EndTime"], slot["Class"]["Subject"]["Name"])
```

## Internal Walkthrough

`ProcessRequest` performs a `switch` on the validated command string. `Ping` is intentionally trivial and serves as a liveness check. `Time` reads `IExactTimeService.GetCurrentLocalDateTime()`, compares it with `DateTime.Now`, and returns the millisecond drift as a string. `Notice` parses the `args` array, extracting `--context`, `--allow-break`, `--mask-duration`, and `--overlay-duration`, then emits `NotificationRequested` with a `NotificationEventArgs` instance.

The heavier commands are `GetLesson`, `ChangeLesson`, and `GetClassPlanByDate`. `GetLesson` composes a runtime snapshot from `ILessonsService`, then enriches `CurrentClassPlan.Classes` by resolving subject details from `IProfileService.Profile.Subjects`. `ChangeLesson` constructs a `ClassChangeService` and delegates to overlay-aware mutation methods. `GetClassPlanByDate` projects a dated class plan into a client-friendly object that pairs class time slots with subject metadata.

## Relationship To Other Concepts

Command processing sits directly on top of the [Request Protocol](/docs/request-protocol) and drives the [Schedule Overlays](/docs/schedule-overlays) model. The `notice` command also feeds the [Event Broadcasting](/docs/event-broadcasting) adjacent notification path, though notification delivery is local to the ClassIsland UI rather than a PUB event.

<Callout type="warn">
`Notice` accepts an `--allow-break` argument and returns `202` when it is `false`, but the current implementation still dispatches the notification immediately. Treat the status code as metadata, not as proof that delivery was deferred by the plugin.
</Callout>

<Accordions>
<Accordion title="Why the helper returns ApiHelperResult instead of raw JSON">
Returning `ApiHelperResult` keeps command code focused on meaning instead of transport serialization. The REQ server can add standard metadata such as `request_id` and `version` without each command handler repeating that work. It also makes tests and future alternate transports easier because handlers return plain structured values. The trade-off is that handlers cannot express transport-specific behavior directly, which is a good constraint for this plugin.
</Accordion>
<Accordion title="Why some handlers still do semantic validation">
Schema validation only proves that the payload shape is acceptable; it does not prove that a GUID maps to a real subject or that a class index exists in the overlay. `ChangeLesson` therefore performs another validation pass and catches `ArgumentException`, `FormatException`, `JsonException`, and similar failures to return `400`. This layered approach looks repetitive at first, but it lets the schema stay generic while runtime code enforces host-specific invariants. That is especially important for schedule mutation, where a structurally valid request can still be logically invalid.
</Accordion>
</Accordions>
