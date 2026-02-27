# IslandMQ 客户端示例

本示例展示了如何使用 pyzmq 库与 IslandMQ 插件进行通信。

但是理论上，任何支持 ZeroMQ (ZMQ) 语言都可以与 IslandMQ 插件进行通信。

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

### 所有命令详细说明

#### 1. ping 命令

**功能**: 健康检查命令，用于验证服务器是否正常运行。

**命令格式**:
```bash
# 通过测试命令运行
python client.py test
```

**示例请求**:
```python
ping_request = {
    "version": 0,
    "command": "ping"
}
```

**响应**:
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

#### 2. notice 命令

**功能**: 发送通知消息到 ClassIsland 应用。

**命令格式**:
```bash
python client.py notice <title> [--context <内容>] [--allow-break <true|false>] [--mask-duration <秒数>] [--overlay-duration <秒数>]
```

**参数说明**:
- `title` (必填): 通知标题
- `--context <内容>` (可选): 通知内容
- `--allow-break <true|false>` (可选): 是否允许打断当前操作，默认为 `true`
- `--mask-duration <秒数>` (可选): 遮罩显示持续时间，默认为 `3.0` 秒
- `--overlay-duration <秒数>` (可选): 覆盖层显示持续时间，默认为 `5.0` 秒

**使用示例**:
```bash
# 发送带有标题和内容的通知
python client.py notice "测试提醒" --context="这是一条测试提醒" --mask-duration=2.0 --overlay-duration=6.0

# 发送仅带标题的简单通知
python client.py notice "简单提醒"
```

**响应**:
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

#### 3. time 命令

**功能**: 获取系统时间与 ClassIsland 精确时间服务之间的差值（以毫秒为单位）。

**命令格式**:
```bash
python client.py time
```

**示例请求**:
```python
time_request = {
    "version": 0,
    "command": "time"
}
```

**响应**:
```json
{
  "success": true,
  "message": "123.456",  // 时间差值（毫秒）
  "data": null,
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```

#### 4. lesson 命令

**功能**: 获取当前课程信息，包括当前科目、下节课科目、课表信息等。

**命令格式**:
```bash
python client.py lesson
```

**示例请求**:
```python
get_lesson_request = {
    "version": 0,
    "command": "get_lesson"
}
```

**响应**:
```json
{
  "success": true,
  "message": "Lesson data retrieved successfully",
  "data": {
    "CurrentSubject": { ... },  // 当前科目信息
    "NextClassSubject": { ... },  // 下节课科目信息
    "CurrentState": "OnClass",  // 当前状态（上课、课间、放学等）
    "CurrentClassPlan": {  // 当前课表信息
      "TimeLayoutId": "guid",
      "Classes": [
        {
          "SubjectId": "guid",
          "IsChangedClass": false,  // 是否为换课
          "Subject": { ... }  // 科目详细信息
        },
        // 更多课程...
      ]
    },
    // 更多信息...
  },
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```

#### 5. change_lesson 命令

**功能**: 换课操作，支持替换、交换、批量替换和清除换课。

**命令格式**:
```bash
python client.py change_lesson <operation> [参数] [--date <日期>]
```

**操作类型**:
- `replace`: 替换单节课程
- `swap`: 交换两节课程
- `batch`: 批量替换课程
- `clear`: 清除换课（恢复原始课表）

**参数说明**:

##### replace 操作参数
- `class_index` (必填): 课程索引（从 0 开始）
- `subject_id` (必填): 新科目的 GUID
- `--date <日期>` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### swap 操作参数
- `class_index1` (必填): 第一节课程的索引（从 0 开始）
- `class_index2` (必填): 第二节课程的索引（从 0 开始）
- `--date <日期>` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### batch 操作参数
- `changes` (必填): JSON 格式的替换计划，键为课程索引，值为新科目的 GUID
- `--date <日期>` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### clear 操作参数
- `--date <日期>` (可选): 日期（格式：YYYY-MM-DD），默认为今天

**使用示例**:

##### 1. 替换课程
```bash
# 将第 3 节课（索引为 2）替换为指定科目
python client.py change_lesson replace 2 "550e8400-e29b-41d4-a716-446655440000" --date "2026-02-28"
```

##### 2. 交换课程
```bash
# 交换第 2 节（索引为 1）和第 4 节（索引为 3）课程
python client.py change_lesson swap 1 3 --date "2026-02-28"
```

##### 3. 批量替换课程
```bash
# 批量替换第 1 节和第 3 节课
python client.py change_lesson batch "{\"0\": \"550e8400-e29b-41d4-a716-446655440000\", \"2\": \"6ba7b810-9dad-11d1-80b4-00c04fd430c8\"}" --date "2026-02-28"
```

##### 4. 清除换课
```bash
# 清除指定日期的所有换课
python client.py change_lesson clear --date "2026-02-28"
```

**注意事项**:
1. 确保提供的科目 GUID 是有效的，否则会返回错误
2. 课程索引必须在有效范围内（0 到课程总数-1）
3. 日期格式必须正确（YYYY-MM-DD）
4. 批量替换的 JSON 格式必须正确
5. 换课操作会创建临时课表，不会修改原始课表
6. 换课后，课程的 `IsChangedClass` 字段会被设置为 `true`，用于在界面上高亮显示

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
- `lesson`: 获取课程信息
- `change_lesson`: 换课操作（支持替换、交换、批量替换和清除换课）

## 发布的事件

IslandMQ 插件会发布以下事件：

- `OnClass`: 上课事件
- `OnBreakingTime`: 课间事件
- `OnAfterSchool`: 放学事件
- `CurrentTimeStateChanged`: 当前时间状态改变事件
