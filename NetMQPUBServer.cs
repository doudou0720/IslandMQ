using System;
using System.Threading;
using ClassIsland.Shared;
using IslandMQ.Utils;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace IslandMQ;

/// <summary>
/// 提供基于 NetMQ 的发布-订阅模式服务器实现
/// </summary>
/// <remarks>
/// 该类实现了 IDisposable 接口，用于管理 NetMQ 发布者套接字和消息发布任务队列
/// 支持异步消息发布，通过内部任务队列处理消息发布操作
/// </remarks>
public class NetMQPUBServer : IDisposable
{
    private PublisherSocket? _serverSocket;
    private Thread? _serverThread;
    private volatile bool _isRunning;
    private readonly string _endpoint;
    private readonly ILogger<NetMQPUBServer>? _logger;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    private NetMQPUBTaskQueue? _taskQueue;
    

    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// 初始化 <see cref="NetMQPUBServer"/> 类的新实例
    /// </summary>
    /// <param name="endpoint">服务器绑定的端点地址，默认为 "tcp://127.0.0.1:5556"</param>
    public NetMQPUBServer(string endpoint = "tcp://127.0.0.1:5556")
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQPUBServer>>();
        _endpoint = endpoint;
    }

    /// <summary>
    /// 检查对象是否已被释放
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQPUBServer));
        }
    }

    /// <summary>
    /// 启动 PUB 服务器
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
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
            if (_serverThread != null && _serverThread.IsAlive)
            {
                _logger?.LogWarning("Previous PUB server thread still alive, waiting for exit...");
                bool waited = false;
                bool isDisposed;
                lock (_disposeLock)
                {
                    isDisposed = _disposed;
                }
                if (!isDisposed)
                {
                    try
                    {
                        waited = _threadExitEvent.Wait(3000);
                    }
                    catch (ObjectDisposedException)
                    {
                        // 事件已被释放，视为已等待完成
                        waited = true;
                    }
                }
                if (!waited && !isDisposed)
                {
                    _logger?.LogError("Previous thread still running, cannot start new PUB server.");
                    return;
                }
            }

            _isRunning = true;
            _serverThread = new Thread(RunServer)
            {
                IsBackground = true,
                Name = "NetMqPubServerThread"
            };
            _serverThread.Start();
        }
    }

    /// <summary>
    /// 内部停止服务器的方法
    /// </summary>
    private void StopInternal()
    {
        lock (_threadLock)
        {
            _isRunning = false;
            if (_serverThread != null)
            {
                bool needsForcedDispose = false;
                
                if (_serverThread.IsAlive)
                {
                    bool eventSignaled = false;
                    bool isDisposed;
                    lock (_disposeLock)
                    {
                        isDisposed = _disposed;
                    }
                    
                    if (!isDisposed)
                    {
                        try
                        {
                            eventSignaled = _threadExitEvent.Wait(2000);
                        }
                        catch (ObjectDisposedException)
                        {
                            // 事件已被释放，视为已等待完成
                            eventSignaled = true;
                        }
                    }
                    
                    if (!eventSignaled && !isDisposed)
                    {
                        _logger?.LogWarning("PUB server thread did not signal exit within 2000ms, forcing join.");
                        if (!_serverThread.Join(5000))
                        {
                            _logger?.LogError("PUB server thread still running after 5000ms, proceeding with socket disposal.");
                            needsForcedDispose = true;
                        }
                    }
                }
                
                if (needsForcedDispose)
                {
                    DisposeSocket();
                }
                
                _serverThread = null;
            }
        }
    }
    
    /// <summary>
    /// 停止 PUB 服务器
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }



    /// <summary>
    /// 服务器运行方法，在单独的线程中执行
    /// </summary>
    private void RunServer()
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
            _serverSocket = new PublisherSocket();
            _serverSocket.Bind(_endpoint);

            _logger?.LogInformation("NetMQ PUB server started at {Endpoint}", _endpoint);

            _taskQueue = new NetMQPUBTaskQueue(InternalPublish);
            _taskQueue.Start();

            while (_isRunning)
            {
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "PUB server error: {Message}", ex.Message);
        }
        finally
        {
            if (_taskQueue != null)
            {
                try
                {
                    _taskQueue.Stop();
                    _taskQueue.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing task queue: {Message}", ex.Message);
                }
                _taskQueue = null;
            }
            DisposeSocket();
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
    /// 发布消息
    /// </summary>
    /// <param name="message">要发布的消息内容</param>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public void Publish(string message)
    {
        CheckDisposed();
        try
        {
            if (_taskQueue != null && _isRunning)
            {
                _taskQueue.EnqueueMessage(message);
                _logger?.LogDebug("Message queued for publishing: {Message}", message);
            }
            else
            {
                _logger?.LogWarning("Cannot publish message, server not running or task queue not initialized.");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "Error queueing message: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 内部发布消息的方法
    /// </summary>
    /// <param name="message">要发布的消息内容</param>
    private void InternalPublish(string message)
    {
        try
        {
            var socket = _serverSocket;
            if (socket != null && _isRunning)
            {
                socket.SendFrame(message);
                _logger?.LogInformation("Published: {Message}", message);
            }
            else
            {
                _logger?.LogWarning("Cannot publish message, server not running.");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "Error publishing message: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 释放服务器套接字资源
    /// </summary>
    private void DisposeSocket()
    {
        var socket = Interlocked.Exchange(ref _serverSocket, null);
        if (socket != null)
        {
            try
            {
                socket.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing PUB socket: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 释放 <see cref="NetMQPUBServer"/> 类的所有资源
    /// </summary>
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