# pyzmq 最小化应答示例

本示例展示了如何使用 pyzmq 库实现基本的请求-应答模式。

## 依赖

需要安装 pyzmq 库：

```bash
pip install pyzmq
```

## 运行步骤

1. 首先启动服务器：

```bash
python server.py
```

2. 然后在另一个终端启动客户端：

```bash
python client.py
```

## 工作原理

- 服务器使用 `zmq.REP` 套接字类型，绑定到 `tcp://*:5555` 端口
- 客户端使用 `zmq.REQ` 套接字类型，连接到 `tcp://localhost:5555`
- 客户端发送请求消息，服务器接收并处理后发送响应
- 客户端接收响应并打印

## 示例输出

### 服务器输出：
```
Server started, waiting for requests...
Received request: Client 1
Sent response: Hello, Client 1!
Received request: Client 2
Sent response: Hello, Client 2!
Received request: Client 3
Sent response: Hello, Client 3!
Received request: Client 4
Sent response: Hello, Client 4!
Received request: Client 5
Sent response: Hello, Client 5!
```

### 客户端输出：
```
Client started, sending requests...
Sending request: Client 1
Received response: Hello, Client 1!
Sending request: Client 2
Received response: Hello, Client 2!
Sending request: Client 3
Received response: Hello, Client 3!
Sending request: Client 4
Received response: Hello, Client 4!
Sending request: Client 5
Received response: Hello, Client 5!
Client finished
```