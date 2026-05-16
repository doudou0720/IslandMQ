---
title: "Build a Python Client"
description: "Send commands to IslandMQ from Python with pyzmq and handle the normalized response envelope."
---

This guide turns the sample client in `sample/client.py` into a small reusable pattern. The goal is to build a Python process that can send IslandMQ requests reliably and recover from REQ socket failures.

<Steps>
<Step>
### Install pyzmq

```bash
pip install pyzmq
```

</Step>
<Step>
### Create a request socket with sensible options

IslandMQ's own sample uses `RCVTIMEO=5000` and `LINGER=0`. That combination avoids hanging forever on a dead plugin and makes socket recreation cheap after a timeout.

```python
import zmq

def create_socket(ctx: zmq.Context, endpoint: str = "tcp://localhost:5555") -> zmq.Socket:
    sock = ctx.socket(zmq.REQ)
    sock.setsockopt(zmq.RCVTIMEO, 5000)
    sock.setsockopt(zmq.LINGER, 0)
    sock.connect(endpoint)
    return sock
```

</Step>
<Step>
### Wrap send/receive in a helper

```python
import json

def send_request(ctx: zmq.Context, sock: zmq.Socket, payload: dict):
    sock.send_string(json.dumps(payload, ensure_ascii=False))
    raw = sock.recv_string()
    return json.loads(raw)
```

</Step>
<Step>
### Send a real command

```python
ctx = zmq.Context()
sock = create_socket(ctx)

response = send_request(ctx, sock, {
    "version": 0,
    "command": "get_lesson"
})

if not response["success"]:
    raise RuntimeError(response["message"])

print(response["data"]["CurrentState"])
```

</Step>
</Steps>

## Complete Example

```python
import json
import zmq

class IslandMQClient:
    def __init__(self, endpoint: str = "tcp://localhost:5555") -> None:
        self.ctx = zmq.Context()
        self.endpoint = endpoint
        self.sock = self._create_socket()

    def _create_socket(self) -> zmq.Socket:
        sock = self.ctx.socket(zmq.REQ)
        sock.setsockopt(zmq.RCVTIMEO, 5000)
        sock.setsockopt(zmq.LINGER, 0)
        sock.connect(self.endpoint)
        return sock

    def request(self, payload: dict) -> dict:
        try:
            self.sock.send_string(json.dumps(payload, ensure_ascii=False))
            return json.loads(self.sock.recv_string())
        except zmq.ZMQError:
            self.sock.close()
            self.sock = self._create_socket()
            raise

client = IslandMQClient()
print(client.request({"version": 0, "command": "ping"}))
```

This pattern mirrors the repository's `sample/client.py` and is a good starting point for command-line tools, desktop companions, or small local services.

The operational detail worth keeping is the REQ socket recovery path. ZeroMQ REQ sockets become awkward after a failed send/receive cycle, so the sample closes the socket and recreates it instead of trying to keep a potentially poisoned socket alive. That is the right trade for IslandMQ because commands are short-lived, local, and idempotent enough to retry at the application layer.
