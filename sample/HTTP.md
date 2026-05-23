# IslandMQ HTTP 服务

IslandMQ 支持通过 HTTP 协议进行通信，提供 REST API 和 WebSocket 两种连接方式。

## 启用 HTTP 服务

在 ClassIsland 设置中启用 HTTP 服务器并配置端口号。

## 连接信息

> **请替换为实际配置值/地址/端口**

- **HTTP API 地址**: `http://{{HOST}}:{{PORT}}/api`
- **WebSocket 地址**: `ws://{{HOST}}:{{PORT}}/ws`
- **协议**: HTTP REST API + WebSocket

## HTTP API

### 请求格式

发送 POST 请求到 `/api` 端点，请求体为 JSON 格式：

```json
{
  "version": 0,
  "command": "notice",
  "args": ["测试提醒", "--context=这是一条测试提醒"]
}
```

### 响应格式

```json
{
  "success": true,
  "message": "Notice sent successfully",
  "data": null,
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```

### 示例代码

#### cURL

```bash
# 请替换为实际配置值
curl -X POST http://{{HOST}}:{{PORT}}/api \
  -H "Content-Type: application/json" \
  -d '{"version":0,"command":"notice","args":["测试提醒"]}'
```

#### Python

```python
import requests

# 请替换为实际配置值
response = requests.post(
    "http://{{HOST}}:{{PORT}}/api",
    json={
        "version": 0,
        "command": "notice",
        "args": ["测试提醒", "--context=这是一条测试提醒"]
    }
)
print(response.json())
```

#### JavaScript

```javascript
// 请替换为实际配置值
fetch('http://{{HOST}}:{{PORT}}/api', {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json'
    },
    body: JSON.stringify({
        version: 0,
        command: 'notice',
        args: ['测试提醒', '--context=这是一条测试提醒']
    })
})
.then(res => res.json())
.then(data => console.log(data));
```

## WebSocket

### 连接方式

> **请替换为实际配置值/地址/端口**

使用 WebSocket 连接到 `ws://{{HOST}}:{{PORT}}/ws` 端点。

### 接收消息

连接成功后，将自动接收 IslandMQ 发布的事件消息：

```json
"OnClass"
"OnBreakingTime"
"OnAfterSchool"
"CurrentTimeStateChanged"
```

### 示例代码

#### JavaScript

```javascript
// 请替换为实际配置值
const ws = new WebSocket('ws://{{HOST}}:{{PORT}}/ws');

ws.onopen = () => {
    console.log('WebSocket connected');
};

ws.onmessage = (event) => {
    console.log('Received event:', event.data);
};

ws.onclose = () => {
    console.log('WebSocket disconnected');
};
```

#### Python

```python
import websocket

# 请替换为实际配置值
ws = websocket.WebSocket()
ws.connect("ws://{{HOST}}:{{PORT}}/ws")

while True:
    message = ws.recv()
    print(f"Received event: {message}")

ws.close()
```

## CORS 支持

> 按配置生效，仅在启用 CORS 时返回

HTTP API 支持跨域请求，仅在启用 CORS 时返回以下 CORS 头（具体值来自配置或允许列表）：

- `Access-Control-Allow-Origin` - 值来自配置/允许列表或环境变量，空值表示仅允许同源
- `Access-Control-Allow-Methods` - GET, POST, PUT, DELETE, OPTIONS
- `Access-Control-Allow-Headers` - Content-Type, Authorization

## 可用命令

所有命令与 ZeroMQ REQ 模式相同，详见 [README.md](./README.md)。

## 对比

| 特性 | ZeroMQ | HTTP API | WebSocket |
|------|--------|----------|-----------|
| 协议 | TCP | HTTP | WebSocket |
| 端口 | 5555/5556 | 8080 | 8080 |
| 请求-响应 | ✓ | ✓ | ✗ |
| 实时推送 | ✗ | ✗ | ✓ |
| 跨语言支持 | 需要 ZMQ 库 | 通用 HTTP 库 | 需要 WS 库 |
| 跨域支持 | ✗ | ✓ | ✓ |
