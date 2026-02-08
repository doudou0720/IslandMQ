import zmq

context = zmq.Context()
socket = context.socket(zmq.REQ)
socket.setsockopt(zmq.RCVTIMEO, 5000)
socket.setsockopt(zmq.LINGER, 0)
try:
    socket.connect("tcp://localhost:5555")

    print("Client started, sending requests...")

    for i in range(5):
        request = f"Client {i+1}"
        print(f"Sending request: {request}")
        socket.send_string(request)
        
        # 接收响应
        try:
            response = socket.recv_string()
            print(f"Received response: {response}")
        except zmq.Again:
            print(f"Request {i+1} timed out")
            break

    print("Client finished")
finally:
    socket.close()
    context.term()