import zmq
import signal
import sys
import time
import logging
import json

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

context = zmq.Context()
socket = None

def create_socket():
    global socket
    if socket is not None:
        try:
            socket.close()
        except Exception as e:
            logger.error(f"Error closing existing socket: {e}")
    socket = context.socket(zmq.REP)
    socket.bind("tcp://*:5555")
    logger.info("Socket created and bound to tcp://*:5555")

def cleanup(signum=None, frame=None):
    logger.info("Shutting down server...")
    if socket is not None:
        try:
            socket.close()
        except Exception as e:
            logger.error(f"Error closing socket during cleanup: {e}")
    try:
        context.term()
    except Exception as e:
        logger.error(f"Error terminating context during cleanup: {e}")
    sys.exit(0)

signal.signal(signal.SIGINT, cleanup)
signal.signal(signal.SIGTERM, cleanup)

def parse_args(args):
    title = ""
    context = ""
    allow_break = True
    mask_duration = 3.0
    overlay_duration = 5.0
    
    for arg in args:
        if arg.startswith("--context="):
            parts = arg.split('=', 1)
            if len(parts) == 2:
                context = parts[1]
        elif arg.startswith("--allow-break="):
            parts = arg.split('=', 1)
            if len(parts) == 2:
                allow_break = parts[1].lower() == "true"
        elif arg.startswith("--mask-duration="):
            parts = arg.split('=', 1)
            if len(parts) == 2:
                try:
                    mask_duration = float(parts[1])
                except ValueError:
                    pass
        elif arg.startswith("--overlay-duration="):
            parts = arg.split('=', 1)
            if len(parts) == 2:
                try:
                    overlay_duration = float(parts[1])
                except ValueError:
                    pass
        elif not arg.startswith("--") and not title:
            title = arg
    
    return title, context, allow_break, mask_duration, overlay_duration

def handle_request(request):
    if "command" not in request:
        return {
            "status_code": 400,
            "message": "Missing or invalid 'command' parameter"
        }
    
    command = request["command"]
    
    if command == "ping":
        return {
            "status_code": 200,
            "message": "OK"
        }
    elif command == "notice":
        args = request.get("args", [])
        title, context, allow_break, mask_duration, overlay_duration = parse_args(args)
        
        if not title:
            return {
                "status_code": 400,
                "message": "Missing required parameter 'title'"
            }
        
        logger.info(f"Processing notice: title={title}, context={context}, allow_break={allow_break}, mask_duration={mask_duration}, overlay_duration={overlay_duration}")
        
        status_code = 200 if allow_break else 202
        return {
            "status_code": status_code,
            "message": "Notice sent successfully"
        }
    else:
        return {
            "status_code": 404,
            "message": "Command not found"
        }

create_socket()
logger.info("Server started, waiting for requests...")

while True:
    try:
        message = socket.recv_string()
        logger.info(f"Received request: {message}")
        
        try:
            request = json.loads(message)
            response = handle_request(request)
        except json.JSONDecodeError:
            response = {
                "status_code": 400,
                "message": "Invalid JSON"
            }
        
        # 模拟处理时间
        time.sleep(0.1)
        
        # 发送响应
        response_json = json.dumps(response, ensure_ascii=False)
        socket.send_string(response_json)
        logger.info(f"Sent response: {response_json}")
    except zmq.ZMQError as e:
        logger.error(f"ZMQ error occurred: {e} (errno: {e.errno})")
        if e.errno == zmq.EFSM:
            logger.warning("EFSM error detected - recreating socket to recover")
            create_socket()
        else:
            logger.warning("Recreating socket due to ZMQ error")
            create_socket()
    except Exception as e:
        logger.exception("Unexpected error occurred during request processing")
        try:
            error_response = {
                "status_code": 500,
                "message": "Internal server error"
            }
            socket.send_string(json.dumps(error_response))
        except zmq.ZMQError as send_err:
            logger.error(f"Failed to send error response: {send_err} - socket may be in invalid state")
            create_socket()