import json
import zmq
from dataclasses import dataclass
import argparse
import sys

# 设置标准输出为 UTF-8 编码
if sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8')
if sys.stderr.encoding != 'utf-8':
    sys.stderr.reconfigure(encoding='utf-8')


SERVER_ADDRESS = "tcp://localhost:5555"


@dataclass
class SocketHolder:
    socket: zmq.Socket


def create_socket(context, server_address=SERVER_ADDRESS):
    """
    创建并返回一个已连接的 REQ 类型 ZeroMQ 套接字，且设置了接收超时与立即关闭行为。
    
    Parameters:
    	context (zmq.Context): ZeroMQ 上下文，用于创建套接字。
    	server_address (str): 要连接的服务器地址，例如 "tcp://localhost:5555"（默认为模块常量 SERVER_ADDRESS）。
    
    Returns:
    	zmq.Socket: 一个已连接的 REQ 套接字，带有接收超时（RCVTIMEO=5000 ms）和挂起关闭时间为 0（LINGER=0）。
    """
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    socket.setsockopt(zmq.LINGER, 0)
    socket.connect(server_address)
    return socket


def send_request(context, holder, payload):
    """
    发送 JSON 序列化的请求并等待解析后的 JSON 响应。
    
    Parameters:
        context (zmq.Context): ZeroMQ 上下文，用于在发生超时后重建 socket。
        holder (SocketHolder): 包含当前 zmq.Socket 的容器；在超时情况下会替换为新 socket。
        payload (Any): 将被序列化为 JSON 并发送的有效负载。
    
    Returns:
        tuple:
            ok (bool): `True` 表示成功接收到并解析了响应，`False` 表示发生超时或解析错误。
            resp (Any): 当 `ok` 为 `True` 时为解析后的 JSON 响应对象；当 `ok` 为 `False` 时为错误标识字符串，可能为 `"timeout"` 或 `"json_error: <error message>"`。
    """
    socket = holder.socket
    json_request = json.dumps(payload, ensure_ascii=False)
    print(f"Sending request: {json_request}")
    
    try:
        socket.send_string(json_request)
        response = socket.recv_string()
        json_response = json.loads(response)
        print(f"Received response: {json.dumps(json_response, indent=2, ensure_ascii=False)}")
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


def run_tests(context, holder):
    """
    运行一系列预定义的请求测试并在终端打印每项结果与最终汇总。
    
    依次发送 13 个测试请求（ping、无效命令、缺少字段、若干 notice 场景、time、get_lesson 等），根据每次响应的 success 字段判断测试通过与否，收集失败项并在结束时打印通过或失败的汇总。
    
    Parameters:
        context (zmq.Context): 用于发送请求的 ZeroMQ 上下文。
        holder (SocketHolder): 包含用于通信的 socket 的容器。
    
    Returns:
        int: The number of failed tests.
    """
    failures = []
    
    print("\n=== Test 1: ping command ===")
    ping_request = {
        "version": 0,
        "command": "ping"
    }
    ok, resp = send_request(context, holder, ping_request)
    if not (ok and resp.get("success")):
        failures.append("Test 1: ping command")

    print("\n=== Test 2: non-existent command ===")
    invalid_request = {
        "version": 0,
        "command": "nonexistentcommand"
    }
    ok, resp = send_request(context, holder, invalid_request)
    if not (ok and not resp.get("success")):
        failures.append("Test 2: non-existent command")

    print("\n=== Test 3: request without command ===")
    no_command_request = {
        "version": 0,
        "message": "Test without command"
    }
    ok, resp = send_request(context, holder, no_command_request)
    if not (ok and not resp.get("success")):
        failures.append("Test 3: request without command")

    print("\n=== Test 4: notice command with all parameters ===")
    notice_request_full = {
        "version": 0,
        "command": "notice",
        "args": ["测试提醒", "--context=这是一条测试提醒消息", "--allow-break=true"]
    }
    ok, resp = send_request(context, holder, notice_request_full)
    if not (ok and resp.get("success")):
        failures.append("Test 4: notice command with all parameters")

    print("\n=== Test 5: notice command with minimal parameters ===")
    notice_request_minimal = {
        "version": 0,
        "command": "notice",
        "args": ["简单提醒"]
    }
    ok, resp = send_request(context, holder, notice_request_minimal)
    if not (ok and resp.get("success")):
        failures.append("Test 5: notice command with minimal parameters")

    print("\n=== Test 6: notice command with allow-break=false ===")
    notice_request_no_break = {
        "version": 0,
        "command": "notice",
        "args": ["测试提醒", "--allow-break=false"]
    }
    ok, resp = send_request(context, holder, notice_request_no_break)
    if not (ok and resp.get("success")):
        failures.append("Test 6: notice command with allow-break=false")

    print("\n=== Test 7: notice command without title ===")
    notice_request_no_title = {
        "version": 0,
        "command": "notice",
        "args": ["--context=缺少标题的提醒"]
    }
    ok, resp = send_request(context, holder, notice_request_no_title)
    if not (ok and not resp.get("success")):
        failures.append("Test 7: notice command without title")

    print("\n=== Test 8: notice command with custom mask duration ===")
    notice_request_custom_mask_duration = {
        "version": 0,
        "command": "notice",
        "args": ["自定义提醒", "--context=这是一条自定义mask持续时间的提醒", "--mask-duration=2.0"]
    }
    ok, resp = send_request(context, holder, notice_request_custom_mask_duration)
    if not (ok and resp.get("success")):
        failures.append("Test 8: notice command with custom mask duration")

    print("\n=== Test 9: notice command with custom both durations ===")
    notice_request_custom_both_durations = {
        "version": 0,
        "command": "notice",
        "args": ["自定义提醒", "--context=这是一条自定义both持续时间的提醒", "--mask-duration=1.5", "--overlay-duration=20.0"]
    }
    ok, resp = send_request(context, holder, notice_request_custom_both_durations)
    if not (ok and resp.get("success")):
        failures.append("Test 9: notice command with custom both durations")

    print("\n=== Test 10: notice command without args ===")
    notice_request_no_args = {
        "version": 0,
        "command": "notice"
    }
    ok, resp = send_request(context, holder, notice_request_no_args)
    if not (ok and not resp.get("success")):
        failures.append("Test 10: notice command without args")

    print("\n=== Test 11: invalid version ===")
    invalid_version_request = {
        "version": 1,
        "command": "ping"
    }
    ok, resp = send_request(context, holder, invalid_version_request)
    if not (ok and not resp.get("success")):
        failures.append("Test 11: invalid version")

    print("\n=== Test 12: time command ====")
    time_request = {
        "version": 0,
        "command": "time"
    }
    ok, resp = send_request(context, holder, time_request)
    if not (ok and resp.get("success")):
        failures.append("Test 12: time command")

    print("\n=== Test 13: get_lesson command ====")
    get_lesson_request = {
        "version": 0,
        "command": "get_lesson"
    }
    ok, resp = send_request(context, holder, get_lesson_request)
    if not (ok and resp.get("success")):
        failures.append("Test 13: get_lesson command")

    # Print final summary
    print("\n=== Test Summary ===")
    if not failures:
        print("All tests passed!")
    else:
        print(f"Failed tests ({len(failures)}):")
        for failure in failures:
            print(f"  - {failure}")
    
    return len(failures)


def send_notice(title, context_text, allow_break, mask_duration, overlay_duration):
    """
    向服务器发送一条带可选参数的通知（命令为 "notice"）。
    
    参数:
        title (str): 通知的主标题。
        context_text (str): 可选的上下文文本；为空时不包含该参数。
        allow_break (bool): 指示是否允许打断的布尔值，会被序列化为 "true"/"false" 形式的参数。
        mask_duration (float | None): 可选的遮罩持续时间（数值）；为 None 时不包含该参数。
        overlay_duration (float | None): 可选的叠加持续时间（数值）；为 None 时不包含该参数。
    
    说明:
        此函数构建并发送一个包含 version=0、command="notice" 和按规则组装的 args 列表的请求到默认/新建的 ZeroMQ 连接。函数在完成后会关闭其使用的套接字和上下文。
    
    Returns:
        tuple:
            ok (bool): `True` 表示成功发送并收到响应，`False` 表示发生错误。
            resp (Any): 响应或错误信息。
    """
    ctx = zmq.Context()
    holder = SocketHolder(create_socket(ctx))
    try:
        args = [title]
        if context_text:
            args.append(f"--context={context_text}")
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
        ok, resp = send_request(ctx, holder, notice_request)
        return (ok, resp)
    finally:
        holder.socket.close()
        ctx.term()


def run_request_with_socket(payload):
    """
    使用新创建的 ZeroMQ 上下文和请求套接字发送单次请求并返回处理结果。确保在返回前关闭套接字和终止上下文。
    
    Parameters:
        payload: 要发送的有效负载，会被序列化为 JSON 并通过 REQ 套接字发送。
    
    Returns:
        tuple(success, response): 
            - `success` 为 `True` 表示接收到并成功解析了 JSON 响应；为 `False` 表示发生错误。
            - `response` 在成功时为解析后的 JSON 对象；在失败时为错误标识字符串，例如 `"timeout"` 或 `"json_error: <error>"`。
    """
    ctx = zmq.Context()
    holder = SocketHolder(create_socket(ctx))
    try:
        return send_request(ctx, holder, payload)
    finally:
        holder.socket.close()
        ctx.term()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="IslandMQ Client")
    subparsers = parser.add_subparsers(dest="command", help="Available commands")
    
    test_parser = subparsers.add_parser("test", help="Run all tests")
    
    notice_parser = subparsers.add_parser("notice", help="Send a notice")
    notice_parser.add_argument("title", help="Notice title")
    notice_parser.add_argument("--context", help="Notice content")
    notice_parser.add_argument("--allow-break", type=lambda x: x.lower() in ('true', '1', 'yes'), default=True,
                              help="Allow break (true/false)")
    notice_parser.add_argument("--mask-duration", type=float, help="Mask duration in seconds")
    notice_parser.add_argument("--overlay-duration", type=float, help="Overlay duration in seconds")
    
    time_parser = subparsers.add_parser("time", help="Get time difference between exact time and system time")
    
    lesson_parser = subparsers.add_parser("lesson", help="Get lesson information")
    
    args = parser.parse_args()
    
    if args.command == "test" or args.command is None:
        context = zmq.Context()
        holder = SocketHolder(create_socket(context))
        try:
            print("Client started, sending requests...")
            failure_count = run_tests(context, holder)
            print("\nClient finished")
            sys.exit(failure_count)
        finally:
            holder.socket.close()
            context.term()
    elif args.command == "notice":
        send_notice(args.title, args.context, args.allow_break, args.mask_duration, args.overlay_duration)
    elif args.command == "time":
        time_request = {
            "version": 0,
            "command": "time"
        }
        run_request_with_socket(time_request)
    elif args.command == "lesson":
        get_lesson_request = {
            "version": 0,
            "command": "get_lesson"
        }
        run_request_with_socket(get_lesson_request)