---
title: "NetMQ Servers"
description: "Reference for the request/response and publish/subscribe servers that expose IslandMQ over ZeroMQ."
---

IslandMQ exposes two transport classes in the `IslandMQ` namespace. `NetMQREQServer` handles synchronous command traffic, and `NetMQPUBServer` handles asynchronous event broadcasts.

## Types

- `IslandMQ.NetMQREQServer`
- `IslandMQ.NetMQPUBServer`
- Namespace import: `using IslandMQ;`
- Source files: `NetMQREQServer.cs`, `NetMQPUBServer.cs`

## `NetMQREQServer`

```csharp
public class NetMQREQServer(string endpoint = "tcp://127.0.0.1:5555") : IDisposable
```

### Constructor

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `endpoint` | `string` | `"tcp://127.0.0.1:5555"` | ZeroMQ bind endpoint for REQ/REP command traffic. |

### Public Members

```csharp
public event EventHandler<Exception>? ErrorOccurred;
public void Start();
public void Stop();
public void Dispose();
```

| Member | Return type | Description |
|--------|-------------|-------------|
| `ErrorOccurred` | `event EventHandler<Exception>?` | Raised when non-fatal request-loop errors occur. |
| `Start()` | `void` | Binds the response socket and starts the background thread. |
| `Stop()` | `void` | Stops the request loop and waits for the thread to exit. |
| `Dispose()` | `void` | Stops the loop, disposes resources, and suppresses finalization. |

### Example

```csharp
using IslandMQ;

var server = new NetMQREQServer("tcp://127.0.0.1:5555");
server.ErrorOccurred += (_, ex) => Console.WriteLine(ex.Message);
server.Start();
```

## `NetMQPUBServer`

```csharp
public class NetMQPUBServer : IDisposable
```

Actual constructor:

```csharp
public NetMQPUBServer(string endpoint = "tcp://127.0.0.1:5556")
```

### Constructor

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `endpoint` | `string` | `"tcp://127.0.0.1:5556"` | ZeroMQ bind endpoint for PUB/SUB event traffic. |

### Public Members

```csharp
public event EventHandler<Exception>? ErrorOccurred;
public void Start();
public void Stop();
public void Publish(string message);
public void Dispose();
```

| Member | Return type | Description |
|--------|-------------|-------------|
| `ErrorOccurred` | `event EventHandler<Exception>?` | Raised when the publisher loop or socket operations fail. |
| `Start()` | `void` | Starts the background publisher task and retry loop. |
| `Stop()` | `void` | Cancels and drains the publisher task. |
| `Publish(string message)` | `void` | Queues a plain string message for later publishing. |
| `Dispose()` | `void` | Stops the server and disposes the queue-related resources. |

### Example

```csharp
using IslandMQ;

var publisher = new NetMQPUBServer();
publisher.Start();
publisher.Publish("OnClass");
```

## Combined Pattern

In production, `Plugin.Initialize` owns both servers and starts them from DI. The servers are public, but the plugin is the intended orchestrator because it coordinates them with ClassIsland startup, shutdown, and lesson events.
