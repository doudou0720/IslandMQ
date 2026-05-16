---
title: "Getting Started"
description: "Connect external programs to ClassIsland through IslandMQ's ZeroMQ request and event endpoints."
---

IslandMQ is a .NET 8 ClassIsland 2.x plugin that exposes timetable queries, notifications, schedule mutation, and lifecycle events over ZeroMQ.

## The Problem

- ClassIsland plugins normally run in-process, which makes cross-language automation awkward.
- Simple desktop automations still need a reliable way to ask for the current lesson, time drift, or a dated class plan.
- External tools need both commands and push events, but HTTP alone does not provide a clean broadcast channel for schedule state changes.
- Temporary schedule edits are easy to get wrong if a client writes directly to ClassIsland profile data instead of using the host's overlay model.

## The Solution

IslandMQ starts two local ZeroMQ endpoints inside ClassIsland: a REQ/REP server on `tcp://127.0.0.1:5555` for commands and a PUB/SUB server on `tcp://127.0.0.1:5556` for event broadcasts. Requests are versioned, validated against JSON Schema, dispatched through `ClassIslandAPIHelper`, and normalized into a stable JSON response envelope.

```ts
import { Request } from "zeromq";

const sock = new Request();
sock.connect("tcp://127.0.0.1:5555");

await sock.send(JSON.stringify({ version: 0, command: "ping" }));
const [reply] = await sock.receive();

console.log(reply.toString());
```

## Installation

<Callout type="info">
IslandMQ itself is shipped as a ClassIsland plugin. The commands below install a JavaScript ZeroMQ client so your app can talk to the plugin after you install the plugin release into ClassIsland.
</Callout>

" "bun"]}>
<Tab value="npm">

```bash
npm install zeromq
```

</Tab>
<Tab value="pnpm">

```bash
pnpm add zeromq
```

</Tab>
<Tab value="yarn">

```bash
yarn add zeromq
```

</Tab>
<Tab value="bun">

```bash
bun add zeromq
```

</Tab>
</Tabs>

## Quick Start

The smallest useful workflow is a `ping` request against the REQ endpoint.

```ts
import { Request } from "zeromq";

const client = new Request();
client.connect("tcp://127.0.0.1:5555");

await client.send(JSON.stringify({ version: 0, command: "ping" }));
const [message] = await client.receive();
const response = JSON.parse(message.toString());

console.log(response);
```

Expected output:

```json
{
  "success": true,
  "message": "OK",
  "data": null,
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```

## Key Features

- ZeroMQ REQ/REP API for `ping`, `notice`, `time`, `get_lesson`, `change_lesson`, and `get_classplan`
- ZeroMQ PUB/SUB events for lesson lifecycle changes such as `OnClass` and `OnAfterSchool`
- Schema-validated protocol versioning through `JsonParser`, `JsonParser0`, and `JsonSchemaDefinitions`
- Safe schedule edits through ClassIsland overlay class plans instead of destructive profile rewrites
- Notification bridging from remote requests into native ClassIsland notification UI

<Cards>
  <Card title="Architecture" href="/docs/architecture">See the plugin runtime, transport endpoints, and request flow.</Card>
  <Card title="Core Concepts" href="/docs/request-protocol">Learn the protocol, command dispatch, overlays, and event model.</Card>
  <Card title="API Reference" href="/docs/api-reference/plugin">Inspect the public C# types and response shapes.</Card>
</Cards>
