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
public class NetMQPUBServer : IDisposable, IAsyncDisposable
{
    private PublisherSocket? _serverSocket;
    private Task? _serverTask;
    private volatile bool _isRunning;
    private int _startAttempts;
    private const int MaxStartAttempts = 3;
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
    /// 启动并运行后台 PUB 服务器任务以处理发布操作。
    /// </summary>
    /// <remarks>
    /// 如果服务器已在运行则立即返回。若检测到先前的服务器任务仍在运行，会等待最多 3 秒以让其退出；若未退出则不会启动新的服务器任务。成功启动后会设置运行标志并创建执行 &lt;c&gt;RunServer&lt;/c&gt; 的后台任务。
    /// 启动失败时会在10秒后重试，最多重试3次，三次失败后记录critical日志并停止重启操作。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public void Start()
    {
        Task.Run(() => StartAsync()).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步启动并运行后台 PUB 服务器任务以处理发布操作。
    /// </summary>
    /// <remarks>
    /// 如果服务器已在运行则立即返回。若检测到先前的服务器任务仍在运行，会等待最多 3 秒以让其退出；若未退出则不会启动新的服务器任务。成功启动后会设置运行标志并创建执行 &lt;c&gt;RunServer&lt;/c&gt; 的后台任务。
    /// 启动失败时会在10秒后重试，最多重试3次，三次失败后记录critical日志并停止重启操作。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public async Task StartAsync()
    {
        CheckDisposed();
        lock (_threadLock)
        {
            CheckDisposed();
            if (_isRunning)
            {
                return;
            }
            if (_serverTask != null && !_serverTask.IsCompleted)
            {
                _logger?.LogWarning("Previous PUB server task still running, waiting for exit...");
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
                    _logger?.LogError("Previous task still running, cannot start new PUB server.");
                    return;
                }
            }

            _isRunning = true;
            _startAttempts = 0;
            _serverTask = RunServerWithRetry();
        }
        // 添加 await 以消除警告，等待服务器启动完成
        await Task.Yield();
    }

    /// <summary>
    /// 运行服务器并处理启动失败的重试逻辑。
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    private async Task RunServerWithRetry()
    {
        while (_isRunning)
        {
            try
            {
                int currentAttempt = Interlocked.Increment(ref _startAttempts);
                await RunServer();
                // 如果RunServer正常结束，重置尝试次数
                Interlocked.Exchange(ref _startAttempts, 0);
                break;
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }

                int currentAttempt = Interlocked.CompareExchange(ref _startAttempts, 0, 0);
                if (currentAttempt >= MaxStartAttempts)
                {
                    _logger?.LogCritical(ex, "Failed to start PUB server after {Attempts} attempts, stopping restart operation.", currentAttempt);
                    _isRunning = false;
                    break;
                }

                _logger?.LogError(ex, "PUB server failed to start (attempt {Attempts}/{MaxAttempts}), retrying in 10 seconds...", currentAttempt, MaxStartAttempts);
                await Task.Delay(10000);
            }
        }
    }

    /// <summary>
    /// 在内部停止发布服务器：将运行标志设为 false，等待后台任务退出，并在必要时强制释放套接字资源后清除任务引用。
    /// </summary>
    /// <remarks>
    /// - 在 _threadLock 下执行以保证线程安全。
    /// - 如果后台任务仍然运行，会等待最多 2000ms 的退出信号；若未收到信号，会尝试以最多 5000ms 的等待任务完成。
    /// - 若任务在等待后仍未结束，则调用 DisposeSocket 强制释放套接字以避免阻塞进程终止。
    /// - 最终会将 _serverTask 置为 null，不抛出异常（异常在调用处或通过事件上报）。
    /// </remarks>
    private async Task StopInternalAsync()
    {
        Task? serverTask = null;
        bool isDisposed = false;

        lock (_threadLock)
        {
            _isRunning = false;
            if (_serverTask != null && !_serverTask.IsCompleted)
            {
                serverTask = _serverTask;

                lock (_disposeLock)
                {
                    isDisposed = _disposed;
                }
            }
        }

        if (serverTask != null)
        {
            bool needsForcedDispose = false;
            bool eventSignaled = false;

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
                _logger?.LogWarning("PUB server task did not signal exit within 2000ms, forcing wait.");
                try
                {
                    var completedTask = await Task.WhenAny(serverTask, Task.Delay(5000)).ConfigureAwait(false);
                    if (completedTask != serverTask)
                    {
                        _logger?.LogError("PUB server task still running after 5000ms, proceeding with socket disposal.");
                        needsForcedDispose = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error waiting for PUB server task to complete: {Message}", ex.Message);
                }
            }

            if (needsForcedDispose)
            {
                DisposeSocket();
            }

            lock (_threadLock)
            {
                if (_serverTask == serverTask)
                {
                    _serverTask = null;
                }
            }
        }
    }

    /// <summary>
    /// 停止服务器并等待其优雅退出。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    public void Stop()
    {
        CheckDisposed();
        Task.Run(() => StopInternalAsync()).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步停止服务器并等待其优雅退出。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    public async Task StopAsync()
    {
        CheckDisposed();
        await StopInternalAsync();
    }



    /// <summary>
    /// 在后台任务上运行 PUB 服务器循环：创建并绑定 PublisherSocket，处理入队消息直到停止。
    /// </summary>
    /// <remarks>
    /// 在发生非致命异常时通过 &lt;c&gt;ErrorOccurred&lt;/c&gt; 事件报告错误。方法返回前会确保套接字被安全释放，并在未处于已释放状态时发出线程退出信号。
    /// </remarks>
    private async Task RunServer()
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
                        await Task.Delay(10);
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
                    await Task.Delay(100);
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
    /// 将消息加入内部任务队列以供后续异步发布。
    /// </summary>
    /// <param name="message">要发布的文本消息，将被放入服务器的发布队列。</param>
    /// <exception cref="ObjectDisposedException">当实例已被释放时抛出。</exception>
    /// <remarks>若服务器未运行或内部队列未初始化，则不会将消息入队，消息将被忽略（不会抛出异常）。发生内部错误时会通过 &lt;see cref="ErrorOccurred"/&gt; 事件上报。</remarks>
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
    /// 停止服务器（如果正在运行）、释放内部托管资源并将实例标记为已释放；可安全重复调用。
    /// </summary>
    /// <remarks>
    /// 调用会触发内部停止逻辑、释放用于线程同步的退出事件，并抑制终结器以避免重复释放。
    /// </remarks>
    public void Dispose()
    {
        Task.Run(() => DisposeAsync().AsTask()).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步停止服务器（如果正在运行）、释放内部托管资源并将实例标记为已释放；可安全重复调用。
    /// </summary>
    /// <remarks>
    /// 调用会触发内部停止逻辑、释放用于线程同步的退出事件，并抑制终结器以避免重复释放。
    /// </remarks>
    /// <returns>表示异步操作的任务</returns>
    public async ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
        }

        await StopInternalAsync().ConfigureAwait(false);

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
