import zmq
import signal
import sys
import time
import logging

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

create_socket()
logger.info("Server started, waiting for requests...")

while True:
    try:
        message = socket.recv_string()
        logger.info(f"Received request: {message}")
        
        # 模拟处理时间
        time.sleep(1)
        
        # 发送响应
        response = f"Hello, {message}!"
        socket.send_string(response)
        logger.info(f"Sent response: {response}")
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
            socket.send_string("Internal server error")
        except zmq.ZMQError as send_err:
            logger.error(f"Failed to send error response: {send_err} - socket may be in invalid state")
            create_socket()
