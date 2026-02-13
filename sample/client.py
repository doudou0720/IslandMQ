import json
import zmq
from dataclasses import dataclass
import argparse


SERVER_ADDRESS = "tcp://localhost:5555"


@dataclass
class SocketHolder:
    socket: zmq.Socket


def create_socket(context, server_address=SERVER_ADDRESS):
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.RCVTIMEO, 5000)
    socket.setsockopt(zmq.LINGER, 0)
    socket.connect(server_address)
    return socket


def send_request(context, holder, payload):
    socket = holder.socket
    json_request = json.dumps(payload, ensure_ascii=False)
    print(f"Sending request: {json_request}")
    socket.send_string(json_request)
    
    try:
        response = socket.recv_string()
        json_response = json.loads(response)
        print(f"Received response: {json.dumps(json_response, indent=2, ensure_ascii=False)}")
        return (True, json_response)
    except zmq.Again:
        print("Request timed out")
        socket.close()
        holder.socket = create_socket(context)
        return (False, "timeout")
    except json.JSONDecodeError as e:
        print(f"Failed to parse response: {e}")
        return (False, f"json_error: {e}")


def run_tests(context, holder):
    print("\n=== Test 1: ping command ===")
    ping_request = {
        "version": 0,
        "command": "ping"
    }
    send_request(context, holder, ping_request)

    print("\n=== Test 2: non-existent command ===")
    invalid_request = {
        "version": 0,
        "command": "nonexistentcommand"
    }
    send_request(context, holder, invalid_request)

    print("\n=== Test 3: request without command ===")
    no_command_request = {
        "version": 0,
        "message": "Test without command"
    }
    send_request(context, holder, no_command_request)

    print("\n=== Test 4: notice command with all parameters ===")
    notice_request_full = {
        "version": 0,
        "command": "notice",
        "args": ["测试提醒", "--context=这是一条测试提醒消息", "--allow-break=true"]
    }
    send_request(context, holder, notice_request_full)

    print("\n=== Test 5: notice command with minimal parameters ===")
    notice_request_minimal = {
        "version": 0,
        "command": "notice",
        "args": ["简单提醒"]
    }
    send_request(context, holder, notice_request_minimal)

    print("\n=== Test 6: notice command with allow-break=false ===")
    notice_request_no_break = {
        "version": 0,
        "command": "notice",
        "args": ["测试提醒", "--allow-break=false"]
    }
    send_request(context, holder, notice_request_no_break)

    print("\n=== Test 7: notice command without title ===")
    notice_request_no_title = {
        "version": 0,
        "command": "notice",
        "args": ["--context=缺少标题的提醒"]
    }
    send_request(context, holder, notice_request_no_title)

    print("\n=== Test 8: notice command with custom mask duration ===")
    notice_request_custom_mask_duration = {
        "version": 0,
        "command": "notice",
        "args": ["自定义提醒", "--context=这是一条自定义mask持续时间的提醒", "--mask-duration=2.0"]
    }
    send_request(context, holder, notice_request_custom_mask_duration)

    print("\n=== Test 9: notice command with custom both durations ===")
    notice_request_custom_both_durations = {
        "version": 0,
        "command": "notice",
        "args": ["自定义提醒", "--context=这是一条自定义both持续时间的提醒", "--mask-duration=1.5", "--overlay-duration=20.0"]
    }
    send_request(context, holder, notice_request_custom_both_durations)

    print("\n=== Test 10: notice command without args ===")
    notice_request_no_args = {
        "version": 0,
        "command": "notice"
    }
    send_request(context, holder, notice_request_no_args)

    print("\n=== Test 11: invalid version ===")
    invalid_version_request = {
        "version": 1,
        "command": "ping"
    }
    send_request(context, holder, invalid_version_request)

    print("\n=== Test 12: time command ===")
    time_request = {
        "version": 0,
        "command": "time"
    }
    send_request(context, holder, time_request)


def send_notice(title, context_text, allow_break, mask_duration, overlay_duration):
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
        send_request(ctx, holder, notice_request)
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
    
    args = parser.parse_args()
    
    if args.command == "test" or args.command is None:
        context = zmq.Context()
        holder = SocketHolder(create_socket(context))
        try:
            print("Client started, sending requests...")
            run_tests(context, holder)
            print("\nClient finished")
        finally:
            holder.socket.close()
            context.term()
    elif args.command == "notice":
        send_notice(args.title, args.context, args.allow_break, args.mask_duration, args.overlay_duration)
    elif args.command == "time":
        ctx = zmq.Context()
        holder = SocketHolder(create_socket(ctx))
        try:
            time_request = {
                "version": 0,
                "command": "time"
            }
            send_request(ctx, holder, time_request)
        finally:
            holder.socket.close()
            ctx.term()