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
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messageQueue = new();
    

    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// 初始化 NetMQ PUB 服务器实例并设置要绑定的端点。
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
    /// <summary>
    /// 在执行操作前验证实例未被释放；如果已释放则抛出异常。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出。</exception>
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
    /// <summary>
    /// 启动并运行后台 PUB 服务器线程以处理发布操作。
    /// </summary>
    /// <remarks>
    /// 如果服务器已在运行则立即返回。若检测到先前的服务器线程仍在运行，会等待最多 3 秒以让其退出；若未退出则不会启动新的服务器线程。成功启动后会设置运行标志并创建执行 <c>RunServer</c> 的后台线程。
    /// </remarks>
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
    /// <summary>
    /// 在内部停止发布服务器：将运行标志设为 false，等待后台线程退出，并在必要时强制释放套接字资源后清除线程引用。
    /// </summary>
    /// <remarks>
    /// - 在 _threadLock 下执行以保证线程安全。 
    /// - 如果后台线程仍然存活，会等待最多 2000ms 的退出信号；若未收到信号，会尝试以最多 5000ms 的 Join 等待线程结束。 
    /// - 若线程在等待后仍未结束，则调用 DisposeSocket 强制释放套接字以避免阻塞进程终止。 
    /// - 最终会将 _serverThread 置为 null，不抛出异常（异常在调用处或通过事件上报）。
    /// </remarks>
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
    /// <summary>
    /// 停止服务器并等待其优雅退出。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }



    /// <summary>
    /// 服务器运行方法，在单独的线程中执行
    /// <summary>
    /// 在后台线程上运行 PUB 服务器循环：创建并绑定 PublisherSocket，处理入队消息直到停止。
    /// </summary>
    /// <remarks>
    /// 在发生非致命异常时通过 <c>ErrorOccurred</c> 事件报告错误。方法返回前会确保套接字被安全释放，并在未处于已释放状态时发出线程退出信号。
    /// </remarks>
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

            while (_isRunning)
            {
                try
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            var socket = Volatile.Read(ref _serverSocket);
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
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    if (ExceptionHelper.IsFatal(ex))
                    {
                        throw;
                    }
                    ErrorOccurred?.Invoke(this, ex);
                    _logger?.LogError(ex, "Error: {Message}", ex.Message);
                    Thread.Sleep(100);
                }
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
    /// <summary>
    /// 将消息加入内部任务队列以供后续异步发布。
    /// </summary>
    /// <param name="message">要发布的文本消息，将被放入服务器的发布队列。</param>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    /// <remarks>若服务器未运行或内部队列未初始化，则不会将消息入队，消息将被忽略（不会抛出异常）。发生内部错误时会通过 <see cref="ErrorOccurred"/> 事件上报。</remarks>
    public void Publish(string message)
    {
        CheckDisposed();
        try
        {
            if (_isRunning)
            {
                _messageQueue.Enqueue(message);
                _logger?.LogDebug("Message queued for publishing: {Message}", message);
            }
            else
            {
                _logger?.LogWarning("Cannot publish message, server not running.");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "Error queueing message: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 释放服务器套接字资源
    /// <summary>
    /// 原子地将内部发布套接字引用替换为 null，并释放先前的套接字资源（如果存在）。
    /// </summary>
    /// <remarks>
    /// 如果释放套接字时发生异常，会通过可用的记录器记录错误并吞掉该异常以保证继续执行。
    /// </remarks>
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
    /// <summary>
    /// 停止服务器（如果正在运行）、释放内部托管资源并将实例标记为已释放；可安全重复调用。
    /// </summary>
    /// <remarks>
    /// 调用会触发内部停止逻辑、释放用于线程同步的退出事件，并抑制终结器以避免重复释放。
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