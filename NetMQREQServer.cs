using System;
using System.Text.Json;
using System.Threading;
using ClassIsland.Shared;
using IslandMQ.Utils;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace IslandMQ;

/// <summary>
/// 提供基于 NetMQ 的请求-响应模式服务器实现
/// </summary>
/// <remarks>
/// 该类实现了 IDisposable 接口，用于管理 NetMQ 响应者套接字
/// 支持处理客户端请求并返回响应
/// </remarks>
public class NetMQREQServer : IDisposable
{
    private ResponseSocket? _serverSocket;
    private Thread? _serverThread;
    private volatile bool _isRunning;
    private readonly string _endpoint;
    private readonly ILogger<NetMQREQServer>? _logger;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    private long _requestIdCounter = 0;
    private const int ResponseVersion = 0;
    

    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// 初始化 <see cref="NetMQREQServer"/> 类的新实例
    /// </summary>
    /// <summary>
    /// 初始化一个 NetMQ 请求-响应服务器实例，并设置用于绑定的端点地址。
    /// </summary>
    /// <param name="endpoint">要绑定的端点地址，例如 "tcp://127.0.0.1:5555"；默认值为 "tcp://127.0.0.1:5555"。</param>
    public NetMQREQServer(string endpoint = "tcp://127.0.0.1:5555")
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQREQServer>>();
        _endpoint = endpoint;
    }
    
    /// <summary>
    /// 获取下一个请求ID，处理溢出情况
    /// </summary>
    /// <summary>
    /// 生成并返回下一个唯一的请求标识，确保在并发环境中递增不会冲突。
    /// </summary>
    /// <returns>下一个递增的、唯一的请求 ID。</returns>
    private long GetNextRequestId()
    {
        // 使用Interlocked.Increment实现线程安全的递增
        // 使用long类型避免溢出问题
        return Interlocked.Increment(ref _requestIdCounter);
    }

    /// <summary>
    /// 检查对象是否已被释放
    /// </summary>
    /// <summary>
    /// 验证当前实例未被释放；如果已释放则抛出异常。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出。</exception>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQREQServer));
        }
    }

    /// <summary>
    /// 启动 REQ 服务器
    /// </summary>
    /// <summary>
    /// 启动服务器后台线程并开始处理请求；如果服务器已在运行则不做任何操作，并在必要时等待先前线程退出以确保同一时间仅有一个服务器实例运行。
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
                _logger?.LogWarning("Previous server thread still alive, waiting for exit...");
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
                    _logger?.LogError("Previous thread still running, cannot start new server.");
                    return;
                }
            }

            _isRunning = true;
            _serverThread = new Thread(RunServer)
            {
                IsBackground = true,
                Name = "NetMqServerThread"
            };
            _serverThread.Start();
        }
    }

    /// <summary>
    /// 内部停止服务器的方法
    /// <summary>
    /// 停止并清理服务器后台线程的内部实现：停止循环标志、等待线程退出并根据超时强制释放套接字，然后清除线程引用。
    /// </summary>
    /// <remarks>
    /// - 将 _isRunning 设为 false，通知服务器循环停止。  
    /// - 在 _threadLock 保护下操作线程引用和等待逻辑，保证并发安全。  
    /// - 若存在活动线程，优先等待最多 2000ms 的 _threadExitEvent 信号（若对象已被释放则视为已退出）；若未收到信号，再尝试阻塞 Join 最多 5000ms。  
    /// - 若线程在上述等待后仍未退出，则调用 DisposeSocket 强制释放底层套接字以促使线程终止。  
    /// - 最终将 _serverThread 设为 null。
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
                        _logger?.LogWarning("Server thread did not signal exit within 2000ms, forcing join.");
                        if (!_serverThread.Join(5000))
                        {
                            _logger?.LogError("Server thread still running after 5000ms, proceeding with socket disposal.");
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
    /// 停止 REQ 服务器
    /// </summary>
    /// <summary>
    /// 停止正在运行的 NetMQ 响应服务器并等待后台线程安全退出。
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }


    
    /// <summary>
    /// 服务器运行方法，在单独的线程中执行
    /// <summary>
    /// 在后台线程中运行 NetMQ 响应服务器：绑定到配置的端点，循环接收请求、处理并发送响应，并在结束时释放资源与通知线程退出信号。
    /// </summary>
    /// <remarks>
    /// 循环期间对外部请求进行接收、生成请求 ID、调用消息处理并将响应发送回客户端；发生非致命异常时触发 <see cref="ErrorOccurred"/> 事件并记录错误，发生致命异常则向上抛出。在退出或发生错误后会处置底层 socket 并设置线程退出事件以通知等待方。
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
            _serverSocket = new ResponseSocket();
            _serverSocket.Bind(_endpoint);

            _logger?.LogInformation("NetMQ server started at {Endpoint}", _endpoint);

            while (_isRunning)
            {
                
                try
                {
                    var socket = Volatile.Read(ref _serverSocket);
                    if (socket != null && socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out var message))
                    {
                        // 生成请求ID
                        long requestId = GetNextRequestId();
                        
                        _logger?.LogDebug("Received (Request ID: {RequestId}): {Message}", requestId, message);

                        var response = ProcessMessage(message, requestId);
                        socket.SendFrame(response);
                        _logger?.LogDebug("Sent (Request ID: {RequestId}): {Response}", requestId, response);
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
            _logger?.LogError(ex, "Server error: {Message}", ex.Message);
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
    /// 处理接收到的消息
    /// </summary>
    /// <param name="message">接收到的消息内容</param>
    /// <param name="requestId">请求ID</param>
    /// <summary>
    /// 处理接收到的请求消息并生成规范化的 JSON 响应。
    /// </summary>
    /// <param name="message">包含请求的 JSON 字符串。</param>
    /// <param name="requestId">为该请求分配的唯一请求标识，会包含在响应中以便跟踪。</param>
    /// <returns>JSON 格式的响应字符串，包含字段 `success`、`message`/`error`、可选的 `data`、`request_id`、`status_code` 和 `version`。</returns>
    private string ProcessMessage(string message, long requestId)
    {
        try
        {
            // 解析JSON消息
            var parseResult = JsonParser.Parse(message);
            
            if (!parseResult.Success)
            {
                // 返回错误响应
                return CreateErrorResponse(parseResult.ErrorMessage ?? "Unknown error", requestId);
            }
            
            // 调用API助手处理请求
            var apiResult = ClassIslandAPIHelper.ProcessRequest(parseResult.ParsedData!.Value);
            
            // 根据StatusCode决定返回成功还是错误响应
            if (apiResult.StatusCode >= 200 && apiResult.StatusCode < 300)
            {
                // 2xx状态码，返回成功响应
                return CreateSuccessResponse(apiResult.Message, apiResult.Data, requestId, apiResult.StatusCode);
            }
            else
            {
                // 其他状态码，返回错误响应
                return CreateErrorResponse(apiResult.Message, requestId, apiResult.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing message (Request ID: {RequestId}): {Message}", requestId, ex.Message);
            return CreateErrorResponse("Internal server error", requestId);
        }
    }
    
    /// <summary>
    /// 创建成功响应消息
    /// </summary>
    /// <param name="message">响应消息</param>
    /// <param name="data">响应数据</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="statusCode">状态码</param>
    /// <summary>
    /// 构建并序列化一个表示成功响应的 JSON 字符串。
    /// </summary>
    /// <param name="message">响应的消息文本，映射到返回对象的 `message` 字段。</param>
    /// <param name="data">可选的响应数据，映射到返回对象的 `data` 字段。</param>
    /// <param name="requestId">请求标识，映射到返回对象的 `request_id` 字段。</param>
    /// <param name="statusCode">响应状态码，映射到返回对象的 `status_code` 字段。</param>
    /// <returns>包含字段 `success`、`message`、`data`、`request_id`、`status_code` 和 `version` 的 JSON 字符串。</returns>
    private string CreateSuccessResponse(string message, object? data = null, long requestId = 0, int statusCode = 200)
    {
        var response = new
        {
            success = true,
            message = message,
            data = data,
            request_id = requestId,
            status_code = statusCode,
            version = ResponseVersion
        };
        
        return JsonSerializer.Serialize(response);
    }
    
    /// <summary>
    /// 创建错误响应消息
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="requestId">请求ID</param>
    /// <param name="statusCode">状态码</param>
    /// <summary>
    /// 构建一个包含错误信息的标准响应对象并将其序列化为 JSON 字符串。
    /// </summary>
    /// <param name="errorMessage">用于 response 中的错误描述。</param>
    /// <param name="requestId">关联的请求 ID（如无则为 0）。</param>
    /// <param name="statusCode">HTTP 风格的状态码，表示错误类型，默认 500。</param>
    /// <returns>序列化后的 JSON 字符串，包含字段：success=false、message、error、request_id、status_code 和 version。</returns>
    private string CreateErrorResponse(string errorMessage, long requestId = 0, int statusCode = 500)
    {
        var response = new
        {
            success = false,
            message = errorMessage,
            error = errorMessage,
            request_id = requestId,
            status_code = statusCode,
            version = ResponseVersion
        };
        
        return JsonSerializer.Serialize(response);
    }

    /// <summary>
    /// 释放服务器套接字资源
    /// <summary>
    /// 以线程安全的方式释放并清除当前活动的响应套接字（如果存在）。
    /// </summary>
    /// <remarks>
    /// 使用原子交换将内部套接字引用设置为 null，并在存在时调用其 Dispose。释放过程中捕获并记录异常但不会重新抛出它们。
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
                _logger?.LogError(ex, "Error disposing socket: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 释放 <see cref="NetMQREQServer"/> 类的所有资源
    /// <summary>
    /// 释放服务器并清理运行时资源：停止后台服务器线程、标记对象为已释放并释放线程退出事件句柄。
    /// 此方法可安全多次调用，后续调用无任何效果。
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