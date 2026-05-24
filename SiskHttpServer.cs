using System.Collections.Concurrent;
using System.Text.Json;
using ClassIsland.Shared;
using IslandMQ.Utils;
using Microsoft.Extensions.Logging;
using Sisk.Core.Http;
using Sisk.Core.Http.Streams;
using Sisk.Core.Routing;

namespace IslandMQ;

/// <summary>
/// 提供基于 Sisk 的 HTTP/WebSocket 服务器实现，同时支持 HTTP API 和 WebSocket 推送。
/// </summary>
/// <remarks>
/// 该类实现了 IDisposable 接口，用于管理 Sisk HTTP 服务器实例
/// - /api/* 路由处理 HTTP 请求，复用 ClassIslandAPIHelper.ProcessRequest
/// - /ws 路由处理 WebSocket 连接，复用消息发布逻辑
/// </remarks>
public class SiskHttpServer : IDisposable
{
    private HttpServer? _server;
    private readonly ushort _port;
    private readonly string _host;
    private readonly ILogger<SiskHttpServer>? _logger;
    private readonly bool _isCorsEnabled;
    private readonly string _corsAllowedOrigins;
    private readonly ConcurrentDictionary<string, HttpWebSocket> _wsConnections = new();
    private volatile bool _isRunning;
    private volatile bool _disposed;
    private readonly object _disposeLock = new();
    private long _requestIdCounter;
    private const int ResponseVersion = 0;

    /// <summary>
    /// 当服务器发生错误时触发的事件
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// 初始化 Sisk HTTP 服务器实例
    /// </summary>
    /// <param name="host">服务器绑定地址，默认 "127.0.0.1"</param>
    /// <param name="port">服务器监听端口，默认 8080</param>
    /// <param name="isCorsEnabled">是否启用 CORS</param>
    /// <param name="corsAllowedOrigins">允许的 CORS 来源（逗号分隔）</param>
    public SiskHttpServer(string host = "127.0.0.1", ushort port = 8080, bool isCorsEnabled = false, string corsAllowedOrigins = "")
    {
        _host = host;
        _port = port;
        _isCorsEnabled = isCorsEnabled;
        _corsAllowedOrigins = corsAllowedOrigins ?? "";
        _logger = IAppHost.GetService<ILogger<SiskHttpServer>>();
    }

    /// <summary>
    /// 验证当前实例未被释放
    /// </summary>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SiskHttpServer));
        }
    }

    /// <summary>
    /// 生成唯一的请求ID
    /// </summary>
    private long GetNextRequestId()
    {
        return Interlocked.Increment(ref _requestIdCounter);
    }

    /// <summary>
    /// 启动 HTTP 服务器
    /// </summary>
    /// <exception cref="ObjectDisposedException">当对象已被释放时抛出</exception>
    public void Start()
    {
        CheckDisposed();

        if (_isRunning)
        {
            _logger?.LogWarning("HTTP server is already running.");
            return;
        }

        try
        {
            var router = new Router();

            // HTTP API 路由 - 对应 REQ 逻辑
            router.SetRoute(RouteMethod.Post, "/api", HandleApiRequest);
            router.SetRoute(RouteMethod.Get, "/api", HandleApiRequestGet);

            // WebSocket 路由 - 对应 PUB 逻辑
            router.SetRoute(RouteMethod.Get, "/ws", HandleWebSocket);

            // CORS 预检请求
            router.SetRoute(RouteMethod.Options, "/api", HandleOptionsPreflight);
            router.SetRoute(RouteMethod.Options, "/ws", HandleOptionsPreflight);

            // 使用 Emit 获取服务器实例
            _server = HttpServer.Emit(_port, out var configuration, out var host, out var actualRouter);

            // 清空默认主机并添加带路由的新主机
            configuration.ListeningHosts.Clear();
            configuration.ListeningHosts.Add(new ListeningHost($"http://{_host}:{_port}", router));

            _server.Start();

            _isRunning = true;
            _logger?.LogInformation("Sisk HTTP server started at http://{Host}:{Port}", _host, _port);
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "Failed to start HTTP server: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 停止 HTTP 服务器
    /// </summary>
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }

    /// <summary>
    /// 内部停止逻辑
    /// </summary>
    private void StopInternal()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;

        // 关闭所有 WebSocket 连接
        foreach (var ws in _wsConnections.Values)
        {
            try
            {
                ws.CloseAsync().Wait(1000);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing WebSocket connection: {Message}", ex.Message);
            }
        }
        _wsConnections.Clear();

        // 停止服务器
        try
        {
            _server?.Stop();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping HTTP server: {Message}", ex.Message);
        }

        _logger?.LogInformation("Sisk HTTP server stopped.");
    }

    /// <summary>
    /// 处理 HTTP POST API 请求 - 复用 NetMQREQServer.ProcessMessage 逻辑
    /// </summary>
    private HttpResponse HandleApiRequest(HttpRequest request)
    {
        long requestId = GetNextRequestId();

        try
        {
            string? message = request.Body;

            if (string.IsNullOrEmpty(message))
            {
                return CreateErrorResponse("Empty request body", requestId, 400, request);
            }

            _logger?.LogDebug("Received HTTP API POST request (Request ID: {RequestId}): {Message}", requestId, message);

            return ProcessApiMessage(message, requestId, request);
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            _logger?.LogError(ex, "Error handling API request (Request ID: {RequestId}): {Message}", requestId, ex.Message);
            return CreateErrorResponse("Internal server error", requestId, 500, request);
        }
    }

    /// <summary>
    /// 处理 HTTP GET API 请求
    /// </summary>
    private HttpResponse HandleApiRequestGet(HttpRequest request)
    {
        long requestId = GetNextRequestId();
        return ProcessApiMessage("{\"command\":\"ping\"}", requestId);
    }

    /// <summary>
    /// 为响应添加 CORS 头（无请求上下文版本）
    /// </summary>
    private static HttpResponse AddCorsHeaders(HttpResponse response, bool isCorsEnabled, string allowedOrigins)
    {
        if (!isCorsEnabled)
        {
            return response;
        }

        // 当没有请求上下文时，如果 allowedOrigins 为空则不设置 CORS 头
        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            // 不设置 CORS 头（默认同源策略）
        }
        else
        {
            // 解析允许的 Origins
            var allowedSet = allowedOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 检查是否允许 "*" 通配符
            if (allowedSet.Contains("*"))
            {
                response.Headers["Access-Control-Allow-Origin"] = "*";
            }
            else
            {
                // 如果不是 "*"，设置为配置的 origins（可能有多个，用逗号分隔）
                response.Headers["Access-Control-Allow-Origin"] = allowedOrigins;
            }
        }

        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        response.Headers["Access-Control-Max-Age"] = "86400";
        return response;
    }

    /// <summary>
    /// 为响应添加 CORS 头
    /// </summary>
    private static HttpResponse AddCorsHeaders(HttpRequest request, HttpResponse response, bool isCorsEnabled, string allowedOrigins)
    {
        if (!isCorsEnabled)
        {
            return response;
        }

        // 获取请求的 Origin 头
        string? requestOrigin = request.Headers["Origin"];

        // 如果 allowedOrigins 为空，不设置 Access-Control-Allow-Origin（仅允许同源）
        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            // 不设置 CORS 头（默认同源策略）
        }
        else
        {
            // 解析允许的 Origins
            var allowedSet = allowedOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 检查是否允许 "*" 通配符
            if (allowedSet.Contains("*"))
            {
                response.Headers["Access-Control-Allow-Origin"] = "*";
            }
            // 检查请求 Origin 是否在允许列表中
            else if (!string.IsNullOrEmpty(requestOrigin) && allowedSet.Contains(requestOrigin))
            {
                response.Headers["Access-Control-Allow-Origin"] = requestOrigin;
            }
            // 如果请求 Origin 不在允许列表中，不设置 CORS 头
        }

        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        response.Headers["Access-Control-Max-Age"] = "86400";
        return response;
    }

    /// <summary>
    /// 处理 OPTIONS 预检请求
    /// </summary>
    private HttpResponse HandleOptionsPreflight(HttpRequest request)
    {
        var response = new HttpResponse(200, null);
        return AddCorsHeaders(request, response, _isCorsEnabled, _corsAllowedOrigins);
    }

    /// <summary>
    /// 处理 API 消息的通用逻辑
    /// </summary>
    private HttpResponse ProcessApiMessage(string message, long requestId, HttpRequest? request = null)
    {
        try
        {
            // 解析 JSON
            JsonParseResult parseResult = JsonParser.Parse(message);

            if (!parseResult.Success)
            {
                return CreateErrorResponse(parseResult.ErrorMessage ?? "Invalid JSON", requestId, 400, request);
            }

            // 调用 API 助手处理请求 - 复用 ClassIslandAPIHelper
            ApiHelperResult apiResult = ClassIslandAPIHelper.ProcessRequest(parseResult.ParsedData!.Value);

            // 根据状态码返回响应
            if (apiResult.StatusCode is >= 200 and < 300)
            {
                return CreateSuccessResponse(apiResult.Message, apiResult.Data, requestId, apiResult.StatusCode, request);
            }
            else
            {
                return CreateErrorResponse(apiResult.Message, requestId, apiResult.StatusCode, request);
            }
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            _logger?.LogError(ex, "Error processing API message (Request ID: {RequestId}): {Message}", requestId, ex.Message);
            return CreateErrorResponse("Internal server error", requestId, 500, request);
        }
    }

    /// <summary>
    /// 处理 WebSocket 连接 - 复用 NetMQPUBServer 消息推送逻辑
    /// </summary>
    private async Task<HttpResponse> HandleWebSocket(HttpRequest request)
    {
        try
        {
            using var ws = request.GetWebSocket();
            string connectionId = Guid.NewGuid().ToString();

            _wsConnections.TryAdd(connectionId, ws);

            _logger?.LogInformation("WebSocket client connected: {ConnectionId}, total connections: {Count}",
                connectionId, _wsConnections.Count);

            try
            {
                // 处理 WebSocket 消息循环
                var msg = await ws.ReceiveMessageAsync();
                while (msg != null)
                {
                    string message = msg.GetString();
                    _logger?.LogDebug("WebSocket received from {ConnectionId}: {Message}", connectionId, message);

                    // 处理订阅逻辑 - 可以扩展支持订阅特定主题
                    // 目前简单地返回确认消息，后续可以扩展为订阅/发布模式
                    var ack = new { type = "ack", message = message };
                    var json = JsonSerializer.Serialize(ack);
                    await ws.SendAsync(json);

                    msg = await ws.ReceiveMessageAsync();
                }
            }
            finally
            {
                _wsConnections.TryRemove(connectionId, out _);
                _logger?.LogInformation("WebSocket client disconnected: {ConnectionId}, remaining connections: {Count}",
                    connectionId, _wsConnections.Count);

                try
                {
                    await ws.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error closing WebSocket: {Message}", ex.Message);
                }
            }

            // 返回空响应，Sisk 会处理 WebSocket 升级
            return new HttpResponse(101, null);
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            ErrorOccurred?.Invoke(this, ex);
            _logger?.LogError(ex, "WebSocket error: {Message}", ex.Message);
            return new HttpResponse(500, new StringContent("WebSocket error"));
        }
    }

    /// <summary>
    /// 向所有 WebSocket 客户端广播消息 - 对应 NetMQPUBServer.Publish
    /// </summary>
    /// <param name="message">要广播的消息</param>
    public void Broadcast(string message)
    {
        if (!_isRunning || _disposed)
        {
            return;
        }

        var tasks = new List<ValueTask<bool>>();
        foreach (var ws in _wsConnections.Values)
        {
            tasks.Add(ws.SendAsync(message));
        }

        if (tasks.Count > 0)
        {
            try
            {
                var waitTasks = tasks.Select(t => t.AsTask()).ToList();
                Task.WaitAll(waitTasks.ToArray(), TimeSpan.FromSeconds(1));
                _logger?.LogDebug("Broadcasted message to {Count} clients: {Message}", tasks.Count, message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error broadcasting message: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    private HttpResponse CreateSuccessResponse(string message, object? data, long requestId, int statusCode = 200, HttpRequest? request = null)
    {
        var response = new
        {
            success = true,
            message,
            data,
            request_id = requestId,
            status_code = statusCode,
            version = ResponseVersion
        };

        var json = JsonSerializer.Serialize(response);
        var content = new StringContent(json);
        var httpResponse = new HttpResponse(statusCode, content);
        return request != null
            ? AddCorsHeaders(request, httpResponse, _isCorsEnabled, _corsAllowedOrigins)
            : AddCorsHeaders(httpResponse, _isCorsEnabled, _corsAllowedOrigins);
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    private HttpResponse CreateErrorResponse(string errorMessage, long requestId, int statusCode = 500, HttpRequest? request = null)
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

        var json = JsonSerializer.Serialize(response);
        var content = new StringContent(json);
        var httpResponse = new HttpResponse(statusCode, content);
        return request != null
            ? AddCorsHeaders(request, httpResponse, _isCorsEnabled, _corsAllowedOrigins)
            : AddCorsHeaders(httpResponse, _isCorsEnabled, _corsAllowedOrigins);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        StopInternal();
        GC.SuppressFinalize(this);
    }
}
