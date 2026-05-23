using System.Reflection;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Services.Registry;
using ClassIsland.Shared;
using IslandMQ.Services;
using IslandMQ.Services.NotificationProviders;
using IslandMQ.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IslandMQ
{
    /// <summary>
    /// IslandMQ 插件的入口类
    /// </summary>
    /// <remarks>
    /// 负责初始化和管理 NetMQ 服务器，处理课程事件并发布通知
    /// </remarks>
    [PluginEntrance]
    public class Plugin : PluginBase, IDisposable
    {
        private NetMQREQServer? _netMqReqServer;
        private NetMQPUBServer? _netMqPubServer;
        private SiskHttpServer? _siskHttpServer;
        private ILessonsService? _lessonsService;
        private ILogger<Plugin>? _logger;
        private IslandMQSettingsService? _settingsService;

        /// <summary>
        /// 配置并注册 IslandMQ 插件所需的服务，并在应用启动和停止时初始化/停止 NetMQ 服务器并注册/注销课表事件处理器。
        /// </summary>
        /// <param name="context">宿主构建器的上下文，用于访问注册时的环境与配置信息。</param>
        /// <param name="services">用于注册通知提供者和 NetMQ 服务器实例的依赖注入服务集合。</param>
        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            _ = services.AddSingleton<IslandMQSettingsService>();
            _ = services.AddSettingsPage<Settings.IslandMQSettingsPage>();
            _ = services.AddSettingsPage<Settings.AboutSettingsPage>();
            _ = services.AddNotificationProvider<IslandMQNotificationProvider>();

            // 动态反射，实现在低 PluginSdk 上使用高版本功能
            List<SettingsPageInfo> registeredSettingsPageInfos = [.. SettingsWindowRegistryService.Registered.Where(info => info.Id.StartsWith("islandmq") && info.Category == SettingsPageCategory.External)];

            Console.WriteLine($"[IslandMQ] Registered settings pages count: {registeredSettingsPageInfos.Count}");

            if (InjectService.TryGetAddSettingsPageGroupMethod(out MethodInfo? addSettingsPageGroupMethod))
            {
                Console.WriteLine("[IslandMQ] AddSettingsPageGroup method found, creating group...");
                try
                {
                    _ = addSettingsPageGroupMethod.Invoke(typeof(SettingsWindowRegistryExtensions), [services, "islandmq", "\uEA33", "IslandMQ"]);

                    if (InjectService.TryGetSettingsPageInfoGroupIdProperty(out PropertyInfo? groupIdProperty))
                    {
                        foreach (SettingsPageInfo info in registeredSettingsPageInfos)
                        {
                            groupIdProperty.SetValue(info, "islandmq");
                        }
                    }
                    Console.WriteLine("[IslandMQ] Group created successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IslandMQ] Failed to create settings page group: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[IslandMQ] AddSettingsPageGroup method not found, using fallback...");
                try
                {
                    if (InjectService.TryGetSettingsPageInfoNameField(out FieldInfo? nameField))
                    {
                        foreach (SettingsPageInfo info in registeredSettingsPageInfos)
                        {
                            nameField.SetValue(info, "IslandMQ·" + (string)nameField.GetValue(info)!);
                        }
                    }
                    Console.WriteLine("[IslandMQ] Fallback applied");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IslandMQ] Failed to apply fallback grouping: {ex.Message}");
                }
            }

            AppBase app = AppBase.Current;
            app.AppStarted += (_, _) =>
            {
                _logger = IAppHost.GetService<ILogger<Plugin>>();
                _settingsService = IAppHost.GetService<IslandMQSettingsService>();
                StartNetMqReqServer();
                StartNetMqPubServer();
                StartSiskHttpServer();
                RegisterLessonEvents();
            };
            app.AppStopping += (o, e) =>
            {
                UnregisterLessonEvents();
                StopNetMqReqServer();
                StopNetMqPubServer();
                StopSiskHttpServer();
            };
        }

        /// <summary>
        /// 订阅 ILessonsService 的课时相关事件并注册相应的处理程序。
        /// </summary>
        /// <remarks>
        /// 从 IAppHost 获取 ILessonsService；若服务不可用，会记录错误并提前返回。成功获取后订阅 OnClass、OnBreakingTime、OnAfterSchool 和 CurrentTimeStateChanged 事件。
        /// </remarks>
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
        /// 注销已注册的课程事件处理器并将内部 ILessonsService 引用设为 null。
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
        /// 在课程开始时向 NetMQ PUB 服务器发布 "OnClass" 通知。
        /// </summary>
        /// <param name="sender">触发事件的来源。</param>
        /// <param name="e">事件参数（未使用）。</param>
        private void OnClassHandler(object? sender, EventArgs e)
        {
            _netMqPubServer?.Publish("OnClass");
            _siskHttpServer?.Broadcast("OnClass");
        }

        /// <summary>
        /// 在课间时间发生时通过 PUB 服务器发布 "OnBreakingTime" 通知。
        /// </summary>
        /// <remarks>如果 PUB 服务器未注册或不可用，则不会执行任何操作。</remarks>
        private void OnBreakingTimeHandler(object? sender, EventArgs e)
        {
            _netMqPubServer?.Publish("OnBreakingTime");
            _siskHttpServer?.Broadcast("OnBreakingTime");
        }

        /// <summary>
        /// 将表示放学的通知 "OnAfterSchool" 发布到 NetMQ PUB 服务器。
        /// </summary>
        private void OnAfterSchoolHandler(object? sender, EventArgs e)
        {
            _netMqPubServer?.Publish("OnAfterSchool");
            _siskHttpServer?.Broadcast("OnAfterSchool");
        }

        /// <summary>
        /// 在课程时间状态改变时通过已注册的 NetMQ PUB 服务器发布 "CurrentTimeStateChanged" 通知。
        /// </summary>
        /// <param name="sender">触发事件的源对象。</param>
        /// <param name="e">事件参数（未使用）。</param>
        private void CurrentTimeStateChangedHandler(object? sender, EventArgs e)
        {
            _netMqPubServer?.Publish("CurrentTimeStateChanged");
            _siskHttpServer?.Broadcast("CurrentTimeStateChanged");
        }

        /// <summary>
        /// 启动 NetMQ REQ 服务器并订阅其错误事件。
        /// </summary>
        /// <remarks>
        /// 如果无法从依赖注入解析到服务器实例或启动过程中发生异常，将记录错误日志；启动成功后会订阅服务器的 &lt;c&gt;ErrorOccurred&lt;/c&gt; 事件并记录成功信息日志。
        /// </remarks>
        private void StartNetMqReqServer()
        {
            if (_settingsService == null || !_settingsService.Settings.IsReqServerEnabled)
            {
                _logger?.LogInformation("REQ server is disabled in settings.");
                return;
            }

            try
            {
                string endpoint = $"tcp://{_settingsService.Settings.ServerIp}:{_settingsService.Settings.ReqServerPort}";
                _netMqReqServer = new NetMQREQServer(endpoint);
                if (_netMqReqServer == null)
                {
                    _logger?.LogError("Failed to start NetMQ server: NetMQREQService is not available!");
                    return;
                }
                _netMqReqServer.ErrorOccurred += OnNetMqReqServerError;
                _netMqReqServer.Start();
                _logger?.LogInformation("NetMQ REQ server started at {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start NetMQ REQ server: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// 停止并释放内部的 NetMQ REQ 服务器（若存在），将其引用置空；在成功或失败时记录相应日志。
        /// </summary>
        private void StopNetMqReqServer()
        {
            if (_netMqReqServer != null)
            {
                try
                {
                    _netMqReqServer.ErrorOccurred -= OnNetMqReqServerError;
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
        /// 启动并注册 NetMQ 的 PUB 服务器实例以开始发布消息。
        /// </summary>
        private void StartNetMqPubServer()
        {
            if (_settingsService == null || !_settingsService.Settings.IsPubServerEnabled)
            {
                _logger?.LogInformation("PUB server is disabled in settings.");
                return;
            }

            try
            {
                string endpoint = $"tcp://{_settingsService.Settings.ServerIp}:{_settingsService.Settings.PubServerPort}";
                _netMqPubServer = new NetMQPUBServer(endpoint);
                if (_netMqPubServer == null)
                {
                    _logger?.LogError("Failed to start NetMQ PUB server: NetMQPUBServer is not available!");
                    return;
                }
                _netMqPubServer.ErrorOccurred += OnNetMqPubServerError;
                _netMqPubServer.Start();
                _logger?.LogInformation("NetMQ PUB server started at {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start NetMQ PUB server: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// 停止并释放插件持有的 NetMQ PUB 服务器实例（如果存在），并将内部引用置为 null；在释放失败时记录错误信息。
        /// </summary>
        private void StopNetMqPubServer()
        {
            if (_netMqPubServer != null)
            {
                try
                {
                    _netMqPubServer.ErrorOccurred -= OnNetMqPubServerError;
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
        /// 记录并报告 NetMQ REQ 服务器发生的异常。
        /// </summary>
        /// <param name="sender">触发事件的对象，可能为 null。</param>
        /// <param name="e">引发的异常对象，包含错误信息和堆栈。</param>
        private void OnNetMqReqServerError(object? sender, Exception e)
        {
            _logger?.LogError(e, "NetMQ REQ server error: {Message}", e.Message);
        }

        /// <summary>
        /// 处理来自 NetMQ PUB 服务器的错误事件并记录错误信息。
        /// </summary>
        /// <param name="sender">事件发送者，可能为 null。</param>
        /// <param name="e">触发的异常；其消息和详细信息会被记录。</param>
        private void OnNetMqPubServerError(object? sender, Exception e)
        {
            _logger?.LogError(e, "NetMQ PUB server error: {Message}", e.Message);
        }

        /// <summary>
        /// 启动 Sisk HTTP 服务器。
        /// </summary>
        private void StartSiskHttpServer()
        {
            if (_settingsService == null || !_settingsService.Settings.IsHttpServerEnabled)
            {
                _logger?.LogInformation("HTTP server is disabled in settings.");
                return;
            }

            try
            {
                _siskHttpServer = new SiskHttpServer(
                    _settingsService.Settings.ServerIp,
                    (ushort)_settingsService.Settings.HttpServerPort,
                    _settingsService.Settings.IsCorsEnabled,
                    _settingsService.Settings.CorsAllowedOrigins);
                _siskHttpServer.ErrorOccurred += OnSiskHttpServerError;
                _siskHttpServer.Start();
                _logger?.LogInformation("Sisk HTTP server started at http://{Host}:{Port}",
                    _settingsService.Settings.ServerIp, _settingsService.Settings.HttpServerPort);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start Sisk HTTP server: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// 停止 Sisk HTTP 服务器。
        /// </summary>
        private void StopSiskHttpServer()
        {
            if (_siskHttpServer != null)
            {
                try
                {
                    _siskHttpServer.ErrorOccurred -= OnSiskHttpServerError;
                    _siskHttpServer.Dispose();
                    _logger?.LogInformation("Sisk HTTP server stopped successfully!");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to stop/dispose Sisk HTTP server: {Message}", ex.Message);
                }
                _siskHttpServer = null;
            }
        }

        /// <summary>
        /// 处理 Sisk HTTP 服务器的错误事件。
        /// </summary>
        private void OnSiskHttpServerError(object? sender, Exception e)
        {
            _logger?.LogError(e, "Sisk HTTP server error: {Message}", e.Message);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
