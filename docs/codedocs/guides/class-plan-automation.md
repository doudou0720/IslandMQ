---
title: "Automate Class Plan Changes"
description: "Fetch the correct class indexes, mutate an overlay, and verify the result without corrupting the base plan."
---

The hard part of `change_lesson` is not sending the request. It is making sure you use the same class indexes IslandMQ expects. This guide shows the safe pattern: fetch the current dated class plan first, derive indexes from `TimeType == 0` class rows, then send the mutation.

<Steps>
<Step>
### Query the class plan for the target date

```json
{
  "version": 0,
  "command": "get_classplan",
  "date": "2026-05-16"
}
```

Read the returned `Layouts` array under `data.ClassPlan.TimeLayout.Layouts`. These rows already exclude breaks, which makes them suitable for `class_index`, `class_index1`, and `class_index2`.

</Step>
<Step>
### Choose the operation you need

Use `replace` for one class, `swap` for two class indexes, `batch` for multiple replacements, and `clear` to drop the overlay for the date.

```json
{
  "version": 0,
  "command": "change_lesson",
  "operation": "swap",
  "date": "2026-05-16",
  "class_index1": 0,
  "class_index2": 3
}
```

</Step>
<Step>
### Verify the overlay after the mutation

Issue `get_classplan` again and inspect the returned class data. A changed class should now reflect the new `SubjectId`, and the underlying overlay will have `IsOverlay: true`.

</Step>
</Steps>

## Complete Example

```ts
import { Request } from "zeromq";

const req = new Request();
req.connect("tcp://127.0.0.1:5555");

async function rpc(payload: unknown) {
  await req.send(JSON.stringify(payload));
  const [raw] = await req.receive();
  return JSON.parse(raw.toString());
}

const classPlan = await rpc({
  version: 0,
  command: "get_classplan",
  date: "2026-05-16"
});

const firstSlot = classPlan.data.ClassPlan.TimeLayout.Layouts[0];
const replacementSubject = "550e8400-e29b-41d4-a716-446655440000";

await rpc({
  version: 0,
  command: "change_lesson",
  operation: "replace",
  date: "2026-05-16",
  class_index: 0,
  subject_id: replacementSubject
});

const updated = await rpc({
  version: 0,
  command: "get_classplan",
  date: "2026-05-16"
});

console.log(firstSlot.StartTime, updated.data.ClassPlan.IsOverlay);
```

This approach matches the service behavior in `ClassChangeService.GetOrCreateTempClassPlan`. It avoids stale hard-coded indexes and makes your automation resilient when other tools have already changed the overlay.
