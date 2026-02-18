using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using IslandMQ.Services.NotificationProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IslandMQ;

/// <summary>
/// IslandMQ 插件的入口类
/// </summary>
/// <remarks>
/// 负责初始化和管理 NetMQ 服务器，处理课程事件并发布通知
/// </remarks>
[PluginEntrance]
public class Plugin : PluginBase
{
    private NetMQREQServer? _netMqReqServer;
    private NetMQPUBServer? _netMqPubServer;
    private ILessonsService? _lessonsService;
    private ILogger<IslandMQ.Plugin>? _logger;

    /// <summary>
    /// 初始化插件
    /// </summary>
    /// <param name="context">主机构建器上下文</param>
    /// <param name="services">服务集合</param>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddNotificationProvider<IslandMQNotificationProvider>();
        services.AddSingleton<NetMQREQServer>();
        services.AddSingleton<NetMQPUBServer>();

        
        var app = AppBase.Current;
        // Start/Stop Server
        app.AppStarted += (_, _) => 
        {
            _logger = IAppHost.GetService<ILogger<IslandMQ.Plugin>>();
            StartNetMqReqServer();
            StartNetMqPubServer();
            RegisterLessonEvents();
        };
        app.AppStopping += (o, e) => 
        {
            UnregisterLessonEvents();
            StopNetMqReqServer();
            StopNetMqPubServer();
        };
    }

    /// <summary>
    /// 注册课程事件处理器
    /// </summary>
    private void RegisterLessonEvents()
    {
        _lessonsService = IAppHost.GetService<ILessonsService>();
        if (_lessonsService == null)
        {
            _logger?.LogError("Failed to register lesson events: ILessonsService is not available!");
            return;
        }
        _lessonsService.OnClass += OnClassHandler;
        _lessonsService.OnBreakingTime += OnBreakingTimeHandler;
        _lessonsService.OnAfterSchool += OnAfterSchoolHandler;
        _lessonsService.CurrentTimeStateChanged += CurrentTimeStateChangedHandler;
    }

    /// <summary>
    /// 取消注册课程事件处理器
    /// </summary>
    private void UnregisterLessonEvents()
    {
        if (_lessonsService != null)
        {
            _lessonsService.OnClass -= OnClassHandler;
            _lessonsService.OnBreakingTime -= OnBreakingTimeHandler;
            _lessonsService.OnAfterSchool -= OnAfterSchoolHandler;
            _lessonsService.CurrentTimeStateChanged -= CurrentTimeStateChangedHandler;
            _lessonsService = null;
        }
    }

    /// <summary>
    /// 上课事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnClassHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnClass");
    }

    /// <summary>
    /// 课间休息事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnBreakingTimeHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnBreakingTime");
    }

    /// <summary>
    /// 放学事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnAfterSchoolHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnAfterSchool");
    }

    /// <summary>
    /// 当前时间状态改变事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void CurrentTimeStateChangedHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("CurrentTimeStateChanged");
    }

    /// <summary>
    /// 启动 NetMQ REQ 服务器
    /// </summary>
    private void StartNetMqReqServer()
    {
        try
        {
            _netMqReqServer = IAppHost.GetService<NetMQREQServer>();
            if (_netMqReqServer == null)
            {
                _logger?.LogError("Failed to start NetMQ server: NetMQREQService is not available!");
                return;
            }
            _netMqReqServer.Start();
            _netMqReqServer.ErrorOccurred += OnNetMqReqServerError;
            _logger?.LogInformation("NetMQ server started successfully!");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start NetMQ server: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 停止 NetMQ REQ 服务器
    /// </summary>
    private void StopNetMqReqServer()
    {
        if (_netMqReqServer != null)
        {
            try
            {
                _netMqReqServer.Dispose();
                _logger?.LogInformation("NetMQ server stopped successfully!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop/dispose NetMQ server: {Message}", ex.Message);
            }
            _netMqReqServer = null;
        }
    }

    /// <summary>
    /// 启动 NetMQ PUB 服务器
    /// </summary>
    private void StartNetMqPubServer()
    {
        try
        {
            _netMqPubServer = IAppHost.GetService<NetMQPUBServer>();
            if (_netMqPubServer == null)
            {
                _logger?.LogError("Failed to start NetMQ PUB server: NetMQPUBServer is not available!");
                return;
            }
            _netMqPubServer.Start();
            _netMqPubServer.ErrorOccurred += OnNetMqPubServerError;
            _logger?.LogInformation("NetMQ PUB server started successfully!");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start NetMQ PUB server: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 停止 NetMQ PUB 服务器
    /// </summary>
    private void StopNetMqPubServer()
    {
        if (_netMqPubServer != null)
        {
            try
            {
                _netMqPubServer.Dispose();
                _logger?.LogInformation("NetMQ PUB server stopped successfully!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to stop/dispose NetMQ PUB server: {Message}", ex.Message);
            }
            _netMqPubServer = null;
        }
    }

    /// <summary>
    /// NetMQ REQ 服务器错误事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnNetMqReqServerError(object? sender, Exception e)
    {
        _logger?.LogError(e, "NetMQ REQ server error: {Message}", e.Message);
    }

    /// <summary>
    /// NetMQ PUB 服务器错误事件处理器
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnNetMqPubServerError(object? sender, Exception e)
    {
        _logger?.LogError(e, "NetMQ PUB server error: {Message}", e.Message);
    }
}
