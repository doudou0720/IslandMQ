import zmq
import time
import logging

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

context = zmq.Context()
socket = context.socket(zmq.PUB)
socket.bind("tcp://*:5556")

logger.info("Publisher started, ready to publish messages...")

# 等待订阅者连接
time.sleep(1)

for i in range(10):
    try:
        # 发布消息
        message = f"Message {i+1}: Hello from publisher!"
        socket.send_string(message)
        logger.info(f"Published: {message}")
        
        # 等待一段时间再发布下一条消息
        time.sleep(2)
    except Exception as e:
        logger.error(f"Error publishing message: {e}")

logger.info("Publisher finished, closing...")
socket.close()
context.term()
