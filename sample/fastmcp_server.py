"""
IslandMQ FastMCP 服务器

本文件实现了基于 FastMCP 的 IslandMQ 服务

MCP JSON 配置示例:
{
  "mcpServers": {
    "islandmq": {
      "url": "http://localhost:8000/mcp",
      "name": "IslandMQ MCP Service",
      "description": "Provides access to ClassIsland IslandMQ functionality"
    }
  }
}
"""

import json
import zmq
import atexit
from fastmcp import FastMCP
from dataclasses import dataclass

SERVER_ADDRESS = "tcp://localhost:5555"

# 全局 zmq context
_zmq_context = zmq.Context()

# 注册清理函数
@atexit.register
def cleanup_zmq():
    _zmq_context.term()


@dataclass
class SocketHolder:
    socket: zmq.Socket


def create_socket(context, server_address=SERVER_ADDRESS):
    """
    创建并返回一个已连接的 REQ 类型 ZeroMQ 套接字, 且设置了接收超时与立即关闭行为.
    """
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    socket.setsockopt(zmq.LINGER, 0)
    socket.connect(server_address)
    return socket


def send_request(context, holder, payload):
    """
    发送 JSON 序列化的请求并等待解析后的 JSON 响应。
    """
    socket = holder.socket
    json_request = json.dumps(payload, ensure_ascii=False)
    # print(f"Sending request: {json_request}")

    try:
        socket.send_string(json_request)
        response = socket.recv_string()
        json_response = json.loads(response)
        # print(f"Received response: {json.dumps(json_response, indent=2, ensure_ascii=False)}")
    except zmq.ZMQError as e:
        print(f"ZMQ error: {e}")
        socket.close()
        holder.socket = create_socket(context)
        return (False, f"zmq_error: {e}")
    except json.JSONDecodeError as e:
        print(f"Failed to parse response: {e}")
        return (False, f"json_error: {e}")
    else:
        return (True, json_response)


# 创建 FastMCP 实例
mcp = FastMCP("IslandMQ MCP Service")


@mcp.tool(
    name="ping_islandmq",
    description="Ping the IslandMQ server to check if it's running.\n\n" +
    "Returns:\n" +
    "- success: True if server is running, False otherwise\n" +
    "- message: Status message"
)
def ping_islandmq():
    """
    Ping the IslandMQ server to check if it's running
    """
    holder = SocketHolder(create_socket(_zmq_context))
    try:
        ping_request = {
            "version": 0,
            "command": "ping"
        }
        ok, resp = send_request(_zmq_context, holder, ping_request)
        if ok and resp.get("success"):
            return {"success": True, "message": "IslandMQ server is running"}
        else:
            return {"success": False, "message": f"Failed to ping server: {resp}"}
    finally:
        holder.socket.close()


@mcp.tool(
    name="send_notice",
    description="Send a notice to ClassIsland application.\n\n" +
    "Parameters:\n" +
    "- title: Notice title (required)\n" +
    "- context: Notice content (optional)\n" +
    "- allow_break: Allow break current operation (default: true)\n" +
    "- mask_duration: Mask display duration in seconds (optional)\n" +
    "- overlay_duration: Overlay display duration in seconds (optional)\n\n" +
    "Returns:\n" +
    "- success: True if notice sent successfully, False otherwise\n" +
    "- message: Status message"
)
def send_notice(title, context=None, allow_break=True, mask_duration=None, overlay_duration=None):
    """
    Send a notice to ClassIsland application
    """
    holder = SocketHolder(create_socket(_zmq_context))
    try:
        args = [title]
        if context:
            args.append(f"--context={context}")
        args.append(f"--allow-break={str(allow_break).lower()}")
        if mask_duration is not None:
            args.append(f"--mask-duration={mask_duration}")
        if overlay_duration is not None:
            args.append(f"--overlay-duration={overlay_duration}")

        notice_request = {
            "version": 0,
            "command": "notice",
            "args": args
        }
        ok, resp = send_request(_zmq_context, holder, notice_request)
        if ok and resp.get("success"):
            return {"success": True, "message": "Notice sent successfully"}
        else:
            return {"success": False, "message": f"Failed to send notice: {resp}"}
    finally:
        holder.socket.close()


@mcp.tool(
    name="get_time",
    description="Get time difference between system time and ClassIsland exact time.\n\n" +
    "Returns:\n" +
    "- success: True if time retrieved successfully, False otherwise\n" +
    "- time_difference: Time difference in milliseconds\n" +
    "- message: Status message if failed"
)
def get_time():
    """
    Get time difference between exact time and system time
    """
    holder = SocketHolder(create_socket(_zmq_context))
    try:
        time_request = {
            "version": 0,
            "command": "time"
        }
        ok, resp = send_request(_zmq_context, holder, time_request)
        if ok and resp.get("success"):
            data = resp.get("data")
            payload = data if isinstance(data, dict) else resp
            return {"success": True, "time_difference": payload.get("time_difference"), "message": "Time retrieved successfully"}
        else:
            return {"success": False, "message": f"Failed to get time: {resp}"}
    finally:
        holder.socket.close()


@mcp.tool(
    name="get_lesson",
    description="Get current lesson information, including current subject, next lesson subject, and class plan information.\n\n" +
    "Returns:\n" +
    "- success: True if lesson information retrieved successfully, False otherwise\n" +
    "- lesson_data: Detailed lesson information\n" +
    "- message: Status message if failed"
)
def get_lesson():
    """
    Get current lesson information
    """
    holder = SocketHolder(create_socket(_zmq_context))
    try:
        get_lesson_request = {
            "version": 0,
            "command": "get_lesson"
        }
        ok, resp = send_request(_zmq_context, holder, get_lesson_request)
        if ok and resp.get("success"):
            data = resp.get("data")
            payload = data if isinstance(data, dict) else resp
            return {"success": True, "lesson_data": payload.get("lesson_data"), "message": "Lesson retrieved successfully"}
        else:
            return {"success": False, "message": f"Failed to get lesson: {resp}"}
    finally:
        holder.socket.close()


@mcp.tool(
    name="get_classplan",
    description="Get classplan for a specific date.\n\n" +
    "Parameters:\n" +
    "- date: Date in YYYY-MM-DD format (optional, default is today)\n\n" +
    "Returns:\n" +
    "- success: True if classplan retrieved successfully, False otherwise\n" +
    "- classplan_data: Detailed classplan information\n" +
    "- message: Status message if failed"
)
def get_classplan(date=None):
    """
    Get classplan for a specific date
    """
    holder = SocketHolder(create_socket(_zmq_context))
    try:
        get_classplan_request = {
            "version": 0,
            "command": "get_classplan"
        }
        if date:
            get_classplan_request["date"] = date

        ok, resp = send_request(_zmq_context, holder, get_classplan_request)
        if ok and resp.get("success"):
            data = resp.get("data")
            payload = data if isinstance(data, dict) else resp
            return {"success": True, "classplan_data": payload.get("classplan_data"), "message": "Classplan retrieved successfully"}
        else:
            return {"success": False, "message": f"Failed to get classplan: {resp}"}
    finally:
        holder.socket.close()


@mcp.tool(
    name="change_lesson",
    description="Change lesson information with support for replace, swap, batch replace, and clear operations.\n\n" +
    "Parameters:\n" +
    "- operation: Operation type (replace, swap, batch, clear)\n" +
    "- date: Date in YYYY-MM-DD format (optional, default is today)\n" +
    "- class_index: Class list index (0-based) for replace operation, corresponds to TimeType==0 time slots (required for replace)\n" +
    "- subject_id: New subject ID (GUID) for replace operation (required for replace)\n" +
    "- class_index1: First class list index (0-based) for swap operation, corresponds to TimeType==0 time slots (required for swap)\n" +
    "- class_index2: Second class list index (0-based) for swap operation, corresponds to TimeType==0 time slots (required for swap)\n" +
    "- changes: JSON object with class list indexes as keys and subject IDs as values (required for batch,DO NOT pass a string object,MUST be JSON Object)\n\n" +
    "Returns:\n" +
    "- success: True if lesson changed successfully, False otherwise\n" +
    "- message: Status message\n\n" +
    "Notes:\n" +
    "- For batch operation, changes must be a dictionary/object, not a JSON string\n" +
    "- Class indexes correspond to the class list, only matching TimeType==0 (class) time slots, not breaks (TimeType==1) or others (TimeType==2)\n" +
    "- Example: {\"0\": \"550e8400-e29b-41d4-a716-446655440000\", \"1\": \"6ba7b810-9dad-11d1-80b4-00c04fd430c8\"}"
)
def change_lesson(operation, date=None, class_index=None, subject_id=None, class_index1=None, class_index2=None, changes=None):
    """
    Change lesson information
    """
    # 参数类型转换和保护
    try:
        # 转换整数类型参数
        if class_index is not None:
            class_index = int(class_index)
        if class_index1 is not None:
            class_index1 = int(class_index1)
        if class_index2 is not None:
            class_index2 = int(class_index2)

        # 转换 changes 参数，支持 JSON 字符串或字典
        if changes is not None:
            if isinstance(changes, str):
                # 如果是 JSON 字符串，解析为字典
                try:
                    changes = json.loads(changes)
                except json.JSONDecodeError as e:
                    return {"success": False, "message": f"Invalid JSON in changes parameter: {e}"}

            # 验证解析结果是字典
            if not isinstance(changes, dict):
                return {"success": False, "message": "changes must be a dictionary or JSON string"}

            # 将字典中的键和值都转换为字符串
            string_changes = {}
            for key, value in changes.items():
                string_changes[str(key)] = str(value)
            changes = string_changes

    except (ValueError, TypeError) as e:
        return {"success": False, "message": f"Invalid parameter type: {e}"}

    holder = SocketHolder(create_socket(_zmq_context))
    try:
        change_lesson_request = {
            "version": 0,
            "command": "change_lesson",
            "operation": operation
        }

        if date:
            change_lesson_request["date"] = date

        if operation == "replace":
            if class_index is None or subject_id is None:
                return {"success": False, "message": "class_index and subject_id are required for replace operation"}
            change_lesson_request["class_index"] = class_index
            change_lesson_request["subject_id"] = subject_id
        elif operation == "swap":
            if class_index1 is None or class_index2 is None:
                return {"success": False, "message": "class_index1 and class_index2 are required for swap operation"}
            change_lesson_request["class_index1"] = class_index1
            change_lesson_request["class_index2"] = class_index2
        elif operation == "batch":
            if changes is None:
                return {"success": False, "message": "changes are required for batch operation"}
            change_lesson_request["changes"] = changes
        elif operation == "clear":
            # clear 操作不需要额外参数
            pass
        else:
            return {"success": False, "message": f"unknown operation: {operation}"}

        ok, resp = send_request(_zmq_context, holder, change_lesson_request)
        if ok and resp.get("success"):
            return {"success": True, "message": "Lesson changed successfully"}
        else:
            return {"success": False, "message": f"Failed to change lesson: {resp}"}
    finally:
        holder.socket.close()


if __name__ == "__main__":
    print("Starting IslandMQ MCP Server...")
    print("MCP Server will be available at: http://localhost:8000/mcp")
    print("API Documentation: http://localhost:8000/docs")
    mcp.run(transport="http", host="127.0.0.1", port=8000)
