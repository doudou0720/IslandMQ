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

## 命令参数说明

### notice 命令参数

- `title` (必填): 通知标题
- `--context=<消息>`: 通知内容
- `--allow-break=<true|false>`: 是否允许打断当前操作，默认为 `true`
- `--mask-duration=<秒数>`: 遮罩显示持续时间，默认为 `3.0` 秒
- `--overlay-duration=<秒数>`: 覆盖层显示持续时间，默认为 `5.0` 秒

### 示例

#### CLI 用法

```bash
# 发送带有自定义显示时间的通知
python client.py notice "测试提醒" --context="这是一条测试提醒" --mask-duration=2.0 --overlay-duration=6.0
```

#### 在代码中使用

以下代码属于 `client.py`（或客户端调用方），请求通过现有的 pyzmq 请求-应答套接字发送：

```python
# 构造 notice 请求
notice_request = {
    "version": 0,
    "command": "notice",
    "args": ["测试提醒", "--context=这是一条测试提醒", "--mask-duration=2.0", "--overlay-duration=6.0"]
}

# 发送请求
socket.send_string(json.dumps(notice_request))

# 接收响应
response = socket.recv_string()
response_data = json.loads(response)
```

这符合标准的 pyzmq REQ/REP 流程：构造请求、发送、接收响应。

## 示例输出

### 服务器输出：
```
2025-02-12 10:00:00,000 - INFO - Socket created and bound to tcp://*:5555
2025-02-12 10:00:00,001 - INFO - Server started, waiting for requests...
2025-02-12 10:00:01,000 - INFO - Received request: {"version": 0, "command": "ping"}
2025-02-12 10:00:01,101 - INFO - Sent response: {"status_code": 200, "message": "OK"}
2025-02-12 10:00:01,200 - INFO - Received request: {"version": 0, "command": "notice", "args": ["测试提醒", "--context=这是一条测试提醒消息", "--allow-break=true"]}
2025-02-12 10:00:01,201 - INFO - Processing notice: title=测试提醒, context=这是一条测试提醒消息, allow_break=True, mask_duration=3.0, overlay_duration=5.0
2025-02-12 10:00:01,302 - INFO - Sent response: {"status_code": 200, "message": "Notice sent successfully"}
```

### 客户端输出：
```
Client started, sending requests...

=== Test 1: ping command ===
Sending request: {"version": 0, "command": "ping"}
Received response: {
  "status_code": 200,
  "message": "OK"
}

=== Test 4: notice command with all parameters ===
Sending request: {"version": 0, "command": "notice", "args": ["测试提醒", "--context=这是一条测试提醒消息", "--allow-break=true"]}
Received response: {
  "status_code": 200,
  "message": "Notice sent successfully"
}

Client finished
```
