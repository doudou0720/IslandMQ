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

使用任何支持 ZeroMQ 的语言向 IslandMQ 插件发送 JSON 格式的请求并接收响应。

**连接信息**:
- 地址: `tcp://localhost:5555` (注意该端口号可能未来会根据配置而变化)
- 协议: ZeroMQ REQ/REP 模式
- 请求格式: JSON

**示例请求**:
```json
{
  "version": 0,
  "command": "notice",
  "args": ["测试提醒", "--context=这是一条测试提醒", "--mask-duration=2.0", "--overlay-duration=6.0"]
}
```

### PUB/SUB 模式 (发布-订阅)

使用任何支持 ZeroMQ 的语言订阅 IslandMQ 插件发布的事件消息。

**连接信息**:
- 地址: `tcp://localhost:5556`
- 协议: ZeroMQ PUB/SUB 模式
- 订阅: 空字符串（订阅所有事件）

**示例代码**:
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

## 命令参数说明

### 所有命令详细说明

#### 1. ping 命令

**功能**: 健康检查命令，用于验证服务器是否正常运行。

**JSON 请求格式**:
```json
{
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

**JSON 请求格式**:
```json
{
  "version": 0,
  "command": "notice",
  "args": ["测试提醒", "--context=这是一条测试提醒", "--mask-duration=2.0", "--overlay-duration=6.0"]
}
```

**参数说明**:
- `args[0]`: 通知标题（必填）
- `--context=<内容>`: 通知内容（可选）
- `--allow-break=<true|false>`: 是否允许打断当前操作，默认为 `true`（可选）
- `--mask-duration=<秒数>`: 遮罩显示持续时间，默认为 `3.0` 秒（可选）
- `--overlay-duration=<秒数>`: 覆盖层显示持续时间，默认为 `5.0` 秒（可选）

**使用示例**:

1. 发送带有标题和内容的通知:
```json
{
  "version": 0,
  "command": "notice",
  "args": ["测试提醒", "--context=这是一条测试提醒", "--mask-duration=2.0", "--overlay-duration=6.0"]
}
```

2. 发送仅带标题的简单通知:
```json
{
  "version": 0,
  "command": "notice",
  "args": ["简单提醒"]
}
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

**JSON 请求格式**:
```json
{
  "version": 0,
  "command": "time"
}
```

**响应**:
```json
{
  "success": true,
  "message": "1919.8106",  // 时间差值（毫秒）
  "data": null,
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```

#### 4. lesson 命令

**功能**: 获取当前课程信息，包括当前科目、下节课科目、（当前）课表信息等。

**JSON 请求格式**:
```json
{
  "version": 0,
  "command": "get_lesson"
}
```

**响应**:

<details>
<summary>点击展开响应示例</summary>
```json
{
  "success": true,
  "message": "Lesson data retrieved successfully",
  "data": {
    "CurrentSubject": {  // 当前科目信息
      "Name": "???",  // 科目名称
      "Initial": "?",  // 科目简称
      "TeacherName": "",  // 教师姓名
      "IsOutDoor": false,  // 是否为户外科目
      "AttachedObjects": {},  // 附加对象
      "IsActive": false  // 是否被禁用
    },
    "NextClassSubject": {  // 下节课科目信息，内容同上
      "Name": "???",  // 科目名称
      "Initial": "?",  // 科目简称
      "TeacherName": "",  // 教师姓名
      "IsOutDoor": false,  // 是否为户外科目
      "AttachedObjects": {},  // 附加对象
      "IsActive": false  // 是否被禁用
    },
    "CurrentState": 0,  // 当前状态（0=无状态，1=准备上课，2=上课中，3=课间休息，4=放学）
    "CurrentTimeLayoutItem": {  // 当前时间点信息
      "StartSecond": "",  // 开始秒数（已废弃）
      "EndSecond": "",  // 结束秒数（已废弃）
      "StartTime": "00:00:00",  // 开始时间
      "EndTime": "00:00:00",  // 结束时间
      "TimeType": 0,  // 时间类型（0=上课，1=课间，2=其他）
      "IsHideDefault": false,  // 是否隐藏默认课程
      "DefaultClassId": "00000000-0000-0000-0000-000000000000",  // 默认课程ID
      "BreakName": "",  // 课间名称
      "ActionSet": null,  // 动作集
      "AttachedObjects": {},  // 附加对象
      "IsActive": false  // 是否被禁用
    },
    "CurrentClassPlan": {  // 当前课表信息（无课表时为null）
      "TimeLayoutId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",  // 时间表GUID
      "Name": "周一课表",  // 课表名称
      "TimeRule": {  // 时间规则
        "WeekDay": 1,  // 星期几（0=周日，1=周一，...，6=周六）
        "WeekRotation": 0,  // 周轮换（0=不轮换，1=单周，2=双周）
        "StartDate": "2024-01-01T00:00:00",  // 开始日期
        "EndDate": "2024-12-31T23:59:59"  // 结束日期
      },
      "IsActivated": true,  // 是否激活
      "IsOverlay": false,  // 是否为临时层
      "OverlaySourceId": null,  // 原始课表ID（仅临时层用）
      "OverlaySetupTime": "2024-01-01T00:00:00",  // 临时层创建时间
      "IsEnabled": true,  // 是否启用
      "AssociatedGroup": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",  // 关联组ID
      "Classes": [  // 课程列表
        {
          "SubjectId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",  // 科目ID
          "IsChangedClass": false,  // 是否为换课
          "IsEnabled": true,  // 是否启用
          "AttachedObjects": {},  // 附加对象
          "IsActive": false  // 是否被禁用
        }
        // 更多课程...
      ],
      "TimeLayouts": {  // 时间表集合
        "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx": {  // 时间表GUID
          "Name": "默认时间表",  // 时间表名称
          "Layouts": [  // 时间点列表
            {
              "StartTime": "08:00:00",  // 开始时间
              "EndTime": "08:45:00",  // 结束时间
              "TimeType": 0,  // 时间类型（0=上课，1=课间，2=其他）
              "IsHideDefault": false,  // 是否隐藏默认
              "DefaultClassId": "00000000-0000-0000-0000-000000000000",  // 默认课程ID
              "BreakName": "",  // 课间名称
              "ActionSet": null,  // 动作集
              "AttachedObjects": {},  // 附加对象
              "IsActive": false  // 是否被禁用
            }
            // 更多时间点...
          ],
          "IsActivated": false,  // 是否激活
          "IsActivatedManually": false  // 是否手动激活
        }
      }
    },  // 当前课表信息
    "NextBreakingTimeLayoutItem": {  // 下一个课间时间点，内容同 CurrentTimeLayoutItem
      "StartSecond": "",  // 开始秒数（已废弃）
      "EndSecond": "",  // 结束秒数（已废弃）
      "StartTime": "00:00:00",  // 开始时间
      "EndTime": "00:00:00",  // 结束时间
      "TimeType": 0,  // 时间类型（0=上课，1=课间，2=其他）
      "IsHideDefault": false,  // 是否隐藏默认课程
      "DefaultClassId": "00000000-0000-0000-0000-000000000000",  // 默认课程ID
      "BreakName": "",  // 课间名称
      "ActionSet": null,  // 动作集
      "AttachedObjects": {},  // 附加对象
      "IsActive": false  // 是否被禁用
    },
    "NextClassTimeLayoutItem": {  // 下一个上课时间点，内容同 CurrentTimeLayoutItem
      "StartSecond": "",  // 开始秒数（已废弃）
      "EndSecond": "",  // 结束秒数（已废弃）
      "StartTime": "00:00:00",  // 开始时间
      "EndTime": "00:00:00",  // 结束时间
      "TimeType": 0,  // 时间类型（0=上课，1=课间，2=其他）
      "IsHideDefault": false,  // 是否隐藏默认课程
      "DefaultClassId": "00000000-0000-0000-0000-000000000000",  // 默认课程ID
      "BreakName": "",  // 课间名称
      "ActionSet": null,  // 动作集
      "AttachedObjects": {},  // 附加对象
      "IsActive": false  // 是否被禁用
    },
    "CurrentSelectedIndex": -1,  // 当前时间点索引（无则为-1）
    "OnClassLeftTime": "00:00:00",  // 距上课剩余时间（上课中或无下一节则为0）
    "OnBreakingTimeLeftTime": "00:00:00",  // 距下课剩余时间（不在上课则为0）
    "IsClassPlanEnabled": true,  // 是否启用课表
    "IsClassPlanLoaded": false,  // 是否已加载课表
    "IsLessonConfirmed": false  // 是否已确定当前时间点
  },
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```
</details>

#### 5. get_classplan 命令

**功能**: 获取指定日期的课表信息。

**JSON 请求格式**:
```json
{
  "version": 0,
  "command": "get_classplan",
  "date": "2026-03-01"
}
```

**参数说明**:
- `date` (可选): 日期（格式：YYYY-MM-DD），默认为今天

**响应**:

<details>
<summary>点击展开响应示例</summary>
```json
{
  "success": true,
  "message": "Class plan retrieved successfully",
  "data": {
    "Date": "2026-03-01",
    "ClassPlan": {
      "TimeLayoutId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "TimeRule": {
        "WeekDay": 1,
        "WeekRotation": 0,
        "StartDate": "2024-01-01T00:00:00",
        "EndDate": "2024-12-31T23:59:59"
      },
      "Name": "周一课表",
      "IsOverlay": false,
      "OverlaySourceId": null,
      "OverlaySetupTime": "2024-01-01T00:00:00",
      "IsEnabled": true,
      "AssociatedGroup": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "AttachedObjects": {},
      "IsActive": false,
      "TimeLayout": {
        "Name": "默认时间表",
        "Layouts": [
          {
            "StartTime": "08:00:00",
            "EndTime": "08:45:00",
            "TimeType": 0,
            "IsHideDefault": false,
            "DefaultClassId": "00000000-0000-0000-0000-000000000000",
            "BreakName": "",
            "ActionSet": null,
            "AttachedObjects": {},
            "IsActive": false,
            "Class": {
              "SubjectId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
              "IsChangedClass": false,
              "IsEnabled": true,
              "AttachedObjects": {},
              "IsActive": false,
              "Subject": {
                "Name": "数学",
                "Initial": "数",
                "TeacherName": "张老师",
                "IsOutDoor": false
              }
            }
          }
          // 更多时间点...
        ]
      }
    }
  },
  "request_id": 1,
  "status_code": 200,
  "version": 0
}
```
</details>

#### 6. change_lesson 命令

**功能**: 换课操作，支持替换、交换、批量替换和清除换课。

**JSON 请求格式**:

##### 1. 替换课程
```json
{
  "version": 0,
  "command": "change_lesson",
  "operation": "replace",
  "class_index": 2,
  "subject_id": "550e8400-e29b-41d4-a716-446655440000",
  "date": "2026-02-28"
}
```

##### 2. 交换课程
```json
{
  "version": 0,
  "command": "change_lesson",
  "operation": "swap",
  "class_index1": 1,
  "class_index2": 3,
  "date": "2026-02-28"
}
```

##### 3. 批量替换课程
```json
{
  "version": 0,
  "command": "change_lesson",
  "operation": "batch",
  "changes": {
    "0": "550e8400-e29b-41d4-a716-446655440000",
    "2": "6ba7b810-9dad-11d1-80b4-00c04fd430c8"
  },
  "date": "2026-02-28"
}
```

##### 4. 清除换课
```json
{
  "version": 0,
  "command": "change_lesson",
  "operation": "clear",
  "date": "2026-02-28"
}
```

**操作类型**:
- `replace`: 替换单节课程
- `swap`: 交换两节课程
- `batch`: 批量替换课程
- `clear`: 清除换课（恢复原始课表）

**参数说明**:

##### replace 操作参数
- `class_index` (必填): 课程列表索引（从 0 开始），对应 TimeType==0 的时间点
- `subject_id` (必填): 新科目的 GUID
- `date` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### swap 操作参数
- `class_index1` (必填): 第一节课程的列表索引（从 0 开始），对应 TimeType==0 的时间点
- `class_index2` (必填): 第二节课程的列表索引（从 0 开始），对应 TimeType==0 的时间点
- `date` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### batch 操作参数
- `changes` (必填): JSON 对象，键为课程列表索引（对应 TimeType==0 的时间点），值为新科目的 GUID
- `date` (可选): 日期（格式：YYYY-MM-DD），默认为今天

##### clear 操作参数
- `date` (可选): 日期（格式：YYYY-MM-DD），默认为今天

**注意事项**:
1. 确保提供的科目 GUID 是有效的，否则会返回错误
2. 课程索引必须在有效范围内（0 到课程总数-1）
3. 日期格式必须正确（YYYY-MM-DD）
4. 批量替换的 JSON 格式必须正确
5. 换课操作会创建临时课表，不会修改原始课表
6. 换课后，课程的 `IsChangedClass` 字段会被设置为 `true`，用于在界面上高亮显示
7. 课程索引对应的是课程列表索引，只对应 TimeType==0（上课类型）的时间点，不包括课间（TimeType==1）和其他类型（TimeType==2）
7. 跨天换课请使用两次修改课程操作，分别指定日期


### 订阅事件消息

使用 ZeroMQ SUB 模式订阅事件消息，连接到 `tcp://localhost:5556` 端口，订阅所有消息（空字符串订阅）。

```bash
# 使用 Python 示例
python subscriber.py

# 或使用其他支持 ZeroMQ 的语言
# 例如 Node.js:
# const zmq = require('zeromq');
# const sock = new zmq.Subscriber();
# sock.connect('tcp://localhost:5556');
# sock.subscribe('');
# console.log('Subscribed to events');
# for await (const [msg] of sock) {
#   console.log('Received event:', msg.toString());
# }
```

## 发布的事件

IslandMQ 插件会发布以下事件：

- `OnClass`: 上课事件
- `OnBreakingTime`: 课间事件
- `OnAfterSchool`: 放学事件
- `CurrentTimeStateChanged`: 当前时间状态改变事件
