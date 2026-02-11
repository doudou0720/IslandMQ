import json
import zmq
from dataclasses import dataclass


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
    json_request = json.dumps(payload)
    print(f"Sending request: {json_request}")
    socket.send_string(json_request)
    
    try:
        response = socket.recv_string()
        json_response = json.loads(response)
        print(f"Received response: {json.dumps(json_response, indent=2)}")
        return (True, json_response)
    except zmq.Again:
        print("Request timed out")
        # 关闭并重新创建socket以恢复状态
        socket.close()
        holder.socket = create_socket(context)
        return (False, "timeout")
    except json.JSONDecodeError as e:
        print(f"Failed to parse response: {e}")
        return (False, f"json_error: {e}")


if __name__ == "__main__":
    context = zmq.Context()
    holder = SocketHolder(create_socket(context))
    try:
        print("Client started, sending requests...")

        # 测试1: 发送ping命令
        print("\n=== Test 1: ping command ===")
        ping_request = {
            "version": 0,
            "command": "ping"
        }
        send_request(context, holder, ping_request)

        # 测试2: 发送不存在的命令
        print("\n=== Test 2: non-existent command ===")
        invalid_request = {
            "version": 0,
            "command": "nonexistentcommand"
        }
        send_request(context, holder, invalid_request)

        # 测试3: 发送没有command字段的请求
        print("\n=== Test 3: request without command ===")
        no_command_request = {
            "version": 0,
            "message": "Test without command"
        }
        send_request(context, holder, no_command_request)

        print("\nClient finished")
    finally:
        holder.socket.close()
        context.term()