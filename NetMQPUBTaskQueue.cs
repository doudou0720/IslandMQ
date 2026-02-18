using System;
using System.Collections.Concurrent;
using System.Threading;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace IslandMQ;

public class NetMQPUBTaskQueue : IDisposable
{
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private Thread? _processingThread;
    private volatile bool _isRunning;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    private readonly ILogger<NetMQPUBTaskQueue> _logger;
    private readonly Action<string> _publishAction;

    /// <summary>
    /// 初始化 NetMQPUBTaskQueue 的新实例，使用提供的发布动作处理队列中的消息。
    /// </summary>
    /// <param name="publishAction">用于处理并发布从队列中取出的每条消息的回调动作。</param>
    public NetMQPUBTaskQueue(Action<string> publishAction)
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQPUBTaskQueue>>();
        _publishAction = publishAction;
    }

    /// <summary>
    /// 检查实例是否已释放；如果已释放则抛出 ObjectDisposedException。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQPUBTaskQueue));
        }
    }

    /// <summary>
    /// 启动用于发布消息的后台处理线程，开始从队列中取出消息并调用注入的发布操作进行发布。
    /// </summary>
    /// <remarks>
    /// 如果已经处于运行状态则不做任何操作；如果发现先前的处理线程仍在运行，会等待至多 3000 毫秒以等待其退出，等待失败时不会启动新的处理线程。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">如果实例已被释放。</exception>
    public void Start()
    {
        CheckDisposed();
        lock (_threadLock)
        {
            CheckDisposed();
            if (_isRunning)
            {
                return;
            }
            if (_processingThread != null && _processingThread.IsAlive)
            {
                _logger.LogWarning("Previous processing thread still alive, waiting for exit...");
                bool waited = false;
                lock (_disposeLock)
                {
                    if (!_disposed)
                    {
                        waited = _threadExitEvent.Wait(3000);
                    }
                }
                if (!waited && !_disposed)
                {
                    _logger.LogError("Previous thread still running, cannot start new task queue.");
                    return;
                }
            }

            _isRunning = true;
            _processingThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "NetMqPubTaskQueueThread"
            };
            _processingThread.Start();
        }
    }

    /// <summary>
    /// 请求后台处理线程停止并在内部等待其退出；若线程未在短时限内响应，则尝试强制联接并记录超时与错误信息。
    /// </summary>
    /// <remarks>
    /// 在内部获取线程控制锁、设置停止标志，然后：
    /// - 等待线程通过退出事件最多 2000 毫秒（若未处于已释放状态），
    /// - 如未收到信号则记录警告并尝试使用最多 5000 毫秒的 Join 来等待线程终止，
    /// - 最终将内部线程引用置空以完成停止流程。
    /// </remarks>
    private void StopInternal()
    {
        lock (_threadLock)
        {
            _isRunning = false;
            if (_processingThread != null)
            {
                if (_processingThread.IsAlive)
                {
                    bool eventSignaled = false;
                    lock (_disposeLock)
                    {
                        if (!_disposed)
                        {
                            eventSignaled = _threadExitEvent.Wait(2000);
                        }
                    }
                    
                    if (!eventSignaled && !_disposed)
                    {
                        _logger.LogWarning("Task queue thread did not signal exit within 2000ms, forcing join.");
                        if (!_processingThread.Join(5000))
                        {
                            _logger.LogError("Task queue thread still running after 5000ms, proceeding with disposal.");
                        }
                    }
                }
                
                _processingThread = null;
            }
        }
    }
    
    /// <summary>
    /// 停止后台消息发布处理并等待处理线程安全退出。
    /// </summary>
    /// <exception cref="ObjectDisposedException">如果实例已被释放。</exception>
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }

    /// <summary>
    /// 处理内部消息队列：持续从队列取出消息并调用注入的发布操作来发布消息，直到处理被停止或实例被释放。
    /// </summary>
    /// <remarks>
    /// 该方法在专用后台线程中运行；在遇到发布或处理异常时会记录错误并继续处理后续消息。方法结束前会在未释放实例时发出线程退出信号以供外部等待。
    /// </remarks>
    private void ProcessQueue()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
            _threadExitEvent.Reset();
        }
        
        try
        {
            _logger.LogInformation("NetMQ PUB task queue started processing messages");

            while (_isRunning)
            {
                try
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            _publishAction(message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing message from queue: {Message}", ex.Message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message queue: {Message}", ex.Message);
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task queue processing thread error: {Message}", ex.Message);
        }
        finally
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _threadExitEvent.Set();
                }
            }
        }
    }

    /// <summary>
    /// 将指定消息加入内部发布队列，供后台线程异步发布。
    /// </summary>
    /// <param name="message">要发布的消息文本，将被入队以供后台处理和发送。</param>
    public void EnqueueMessage(string message)
    {
        CheckDisposed();
        try
        {
            _messageQueue.Enqueue(message);
            _logger.LogDebug("Message enqueued for publishing: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing message: {Message}", ex.Message);
        }
    }

    public int QueueCount => _messageQueue.Count;

    /// <summary>
    /// 释放队列及其后台处理线程占用的资源并禁止后续使用该实例。
    /// </summary>
    /// <remarks>
    /// 调用时会先停止后台处理线程（如果正在运行），然后释放线程退出信号等受控资源，并抑制终结器。方法是幂等的：重复调用不会抛出异常或重复释放已释放的资源。
    /// </remarks>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
        }
        
        StopInternal();
        
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _threadExitEvent.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}