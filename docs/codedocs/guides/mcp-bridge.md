---
title: "Expose IslandMQ Through MCP"
description: "Wrap IslandMQ commands in a FastMCP server so AI tools can call timetable and notice operations."
---

The repository includes a practical bridge in `sample/fastmcp_server.py`. It turns IslandMQ commands into MCP tools such as `ping_islandmq`, `send_notice`, `get_time`, `get_lesson`, `get_classplan`, and `change_lesson`.

<Steps>
<Step>
### Install the bridge dependencies

```bash
pip install pyzmq fastmcp
```

</Step>
<Step>
### Reuse the IslandMQ request helper

The sample keeps a module-level ZeroMQ context and creates a short-lived REQ socket per tool call. That is a good fit for MCP tools, which are naturally request-oriented.

```python
from dataclasses import dataclass
import zmq

SERVER_ADDRESS = "tcp://localhost:5555"
_zmq_context = zmq.Context()

@dataclass
class SocketHolder:
    socket: zmq.Socket
```

</Step>
<Step>
### Define MCP tools that translate directly to IslandMQ commands

```python
from fastmcp import FastMCP

mcp = FastMCP("IslandMQ MCP Service")

@mcp.tool(name="ping_islandmq")
def ping_islandmq():
    ...
```

</Step>
<Step>
### Prefer read-before-write for schedule tools

The sample's `change_lesson` tool description explicitly tells callers to fetch `get_classplan` first. Keep that rule in your own bridge so LLMs do not guess class indexes from natural language alone.

</Step>
</Steps>

## Complete Example

```python
import json
import zmq
from dataclasses import dataclass
from fastmcp import FastMCP

SERVER_ADDRESS = "tcp://localhost:5555"
context = zmq.Context()
mcp = FastMCP("IslandMQ MCP Service")

@dataclass
class SocketHolder:
    socket: zmq.Socket

def create_socket():
    sock = context.socket(zmq.REQ)
    sock.setsockopt(zmq.RCVTIMEO, 5000)
    sock.setsockopt(zmq.LINGER, 0)
    sock.connect(SERVER_ADDRESS)
    return sock

def request(payload: dict):
    sock = create_socket()
    try:
        sock.send_string(json.dumps(payload, ensure_ascii=False))
        return json.loads(sock.recv_string())
    finally:
        sock.close()

@mcp.tool(name="get_lesson")
def get_lesson():
    return request({"version": 0, "command": "get_lesson"})
```

This is not a separate IslandMQ feature; it is an integration pattern built on the stable REQ API. The important architectural point is that the MCP server remains a thin translator, not a second source of scheduling truth.

That thin-wrapper approach matters when you expose write operations such as `change_lesson`. If your MCP layer starts caching lesson state or inferring class indexes without asking IslandMQ first, it will drift from the actual ClassIsland overlay state. Treat the bridge as a command adapter and keep all authoritative reads and writes inside IslandMQ itself.
