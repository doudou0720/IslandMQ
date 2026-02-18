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
    /// <param name="endpoint">服务器绑定的端点地址，默认为 "tcp://127.0.0.1:5555"</param>
    public NetMQREQServer(string endpoint = "tcp://127.0.0.1:5555")
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQREQServer>>();
        _endpoint = endpoint;
    }
    
    /// <summary>
    /// 获取下一个请求ID，处理溢出情况
    /// </summary>
    /// <returns>唯一的请求ID</returns>
    private long GetNextRequestId()
    {
        // 使用Interlocked.Increment实现线程安全的递增
        // 使用long类型避免溢出问题
        return Interlocked.Increment(ref _requestIdCounter);
    }

    /// <summary>
    /// 检查对象是否已被释放
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
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
    /// <returns>处理后的响应消息</returns>
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
    /// <returns>序列化后的成功响应消息</returns>
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
    /// <returns>序列化后的错误响应消息</returns>
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
                _logger?.LogError(ex, "Error disposing socket: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 释放 <see cref="NetMQREQServer"/> 类的所有资源
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
