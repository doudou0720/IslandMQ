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

[PluginEntrance]
public class Plugin : PluginBase
{
    private NetMQREQServer? _netMqReqServer;
    private NetMQPUBServer? _netMqPubServer;
    private ILessonsService? _lessonsService;
    private ILogger<IslandMQ.Plugin>? _logger;

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

    // Register events
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

    // 上课钩子
    private void OnClassHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnClass");
    }

    // 课间钩子
    private void OnBreakingTimeHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnBreakingTime");
    }

    // 放学钩子
    private void OnAfterSchoolHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("OnAfterSchool");
    }

    // 当前时间状态改变钩子
    private void CurrentTimeStateChangedHandler(object? sender, EventArgs e)
    {
        _netMqPubServer?.Publish("CurrentTimeStateChanged");
    }

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
            _logger?.LogInformation("NetMQ server started successfully!");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start NetMQ server: {Message}", ex.Message);
        }
    }

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
            _logger?.LogInformation("NetMQ PUB server started successfully!");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start NetMQ PUB server: {Message}", ex.Message);
        }
    }

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
}
