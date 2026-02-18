import zmq
import signal
import sys
import logging

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

context = zmq.Context()
socket = None

subscribed = False
_cleaned_up = False

# 清理函数
def cleanup(signum=None, frame=None, *, exit_on_completion=False):
    """
    执行模块级资源的清理：关闭订阅 socket，终止 ZeroMQ 上下文，并可选择在完成后退出进程。
    
    Parameters:
    	signum (int | None): 触发清理的信号编号（可为 None），用于 signal 处理器回调上下文。
    	frame (types.FrameType | None): 信号处理时传入的当前堆栈帧（可为 None），仅作为回调签名的一部分。
    	exit_on_completion (bool): 若为 True，清理完成后以状态码 0 终止进程。
    
    Behaviour:
    	- 本函数可安全重复调用；若已完成清理则立即返回。
    	- 关闭全局订阅 socket（若存在）并终止全局 ZeroMQ 上下文。
    	- 在 exit_on_completion 为 True 时调用 sys.exit(0) 结束进程。
    """
    global _cleaned_up, socket, context
    if _cleaned_up:
        return
    logger.info("Shutting down subscriber...")
    if socket is not None:
        try:
            socket.close()
            socket = None
        except Exception:
            logger.exception("Error closing socket during cleanup")
    try:
        context.term()
    except Exception:
        logger.exception("Error terminating context during cleanup")
    _cleaned_up = True
    if exit_on_completion:
        sys.exit(0)

# 注册信号处理
signal.signal(signal.SIGINT, lambda s, f: cleanup(s, f, exit_on_completion=True))
signal.signal(signal.SIGTERM, lambda s, f: cleanup(s, f, exit_on_completion=True))

try:
    # 创建订阅套接字
    socket = context.socket(zmq.SUB)
    socket.setsockopt(zmq.RCVTIMEO, 1000)  # 1秒超时
    socket.connect("tcp://localhost:5556")
    
    # 订阅所有消息（空字符串表示订阅所有主题）
    socket.setsockopt_string(zmq.SUBSCRIBE, "")
    subscribed = True
    
    logger.info("Subscriber started, waiting for messages...")
    
    # 接收消息
    message_count = 0
    while True:
        try:
            # 接收消息
            message = socket.recv_string()
            message_count += 1
            logger.info(f"Received message #{message_count}: {message}")
            
            # 可以在这里添加处理消息的逻辑
            
        except zmq.Again:
            # 超时，继续等待
            continue
        except Exception:
            logger.exception("Error receiving message")
            # 可以在这里添加错误处理逻辑
            continue
            
except KeyboardInterrupt:
    # 捕获键盘中断
    logger.info("Keyboard interrupt received")
finally:
    # 清理资源
    cleanup()