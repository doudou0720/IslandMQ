# IslandMQ 客户端示例

本示例展示了如何使用 pyzmq 库与 IslandMQ 插件进行通信。

## 依赖

需要安装 pyzmq 库：

```bash
pip install pyzmq
```

## 使用说明

### REQ/REP 模式 (请求-应答)

使用 `client.py` 向 IslandMQ 插件发送请求并接收响应。

```bash
python client.py notice "测试提醒" --context="这是一条测试提醒" --mask-duration=2.0 --overlay-duration=6.0
```

### PUB/SUB 模式 (发布-订阅)

使用 `subscriber.py` 订阅 IslandMQ 插件发布的事件消息。

```bash
python subscriber.py
```

## 命令参数说明

### notice 命令参数

- `title` (必填): 通知标题
- `--context=<消息>`: 通知内容
- `--allow-break=<true|false>`: 是否允许打断当前操作，默认为 `true`
- `--mask-duration=<秒数>`: 遮罩显示持续时间，默认为 `3.0` 秒
- `--overlay-duration=<秒数>`: 覆盖层显示持续时间，默认为 `5.0` 秒

## 代码示例

### 发送 notice 请求

```python
import zmq
import json

context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.connect("tcp://localhost:5555")

notice_request = {
    "version": 0,
    "command": "notice",
    "args": ["测试提醒", "--context=这是一条测试提醒", "--mask-duration=2.0", "--overlay-duration=6.0"]
}

socket.send_string(json.dumps(notice_request))
response = socket.recv_string()
response_data = json.loads(response)

print(f"Status: {response_data['status_code']}")
print(f"Message: {response_data['message']}")

socket.close()
context.term()
```

### 订阅事件消息

```python
import zmq

context = zmq.Context()
socket = context.socket(zmq.SUB)
socket.connect("tcp://localhost:5556")
socket.setsockopt_string(zmq.SUBSCRIBE, "")

while True:
    message = socket.recv_string()
    print(f"Received event: {message}")

socket.close()
context.term()
```

## 支持的命令

- `ping`: 健康检查
- `notice`: 发送通知
- `time`: 获取时间差值

## 发布的事件

IslandMQ 插件会发布以下事件：

- `OnClass`: 上课事件
- `OnBreakingTime`: 课间事件
- `OnAfterSchool`: 放学事件
- `CurrentTimeStateChanged`: 当前时间状态改变事件
