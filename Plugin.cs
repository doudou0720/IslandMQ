using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IslandMQ;

[PluginEntrance]
public class Plugin : PluginBase
{
    private NetMQREQServer? _netMqServer;
    private NetMQPUBServer? _netMqPubServer;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<NetMQREQServer>();
        services.AddSingleton<NetMQPUBServer>();
        
        AppBase.Current.AppStarted += (_, _) => 
        {
            StartNetMqReqServer();
            StartNetMqPubServer();
        };
        AppBase.Current.AppStopping += (o, e) => 
        {
            StopNetMqReqServer();
            StopNetMqPubServer();
        };
    }

    private void StartNetMqReqServer()
    {
        var logger = IAppHost.GetService<ILogger<IslandMQ.Plugin>>();
        try
        {
            _netMqServer = IAppHost.GetService<NetMQREQServer>();
            _netMqServer.Start();
            logger.LogInformation("NetMQ server started successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start NetMQ server: {Message}", ex.Message);
        }
    }

    private void StopNetMqReqServer()
    {
        var logger = IAppHost.GetService<ILogger<IslandMQ.Plugin>>();
        if (_netMqServer != null)
        {
            try
            {
                _netMqServer.Stop();
                logger.LogInformation("NetMQ server stopped successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop NetMQ server: {Message}", ex.Message);
            }
            finally
            {
                try
                {
                    _netMqServer.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispose NetMQ server: {Message}", ex.Message);
                }
                _netMqServer = null;
            }
        }
    }

    private void StartNetMqPubServer()
    {
        var logger = IAppHost.GetService<ILogger<IslandMQ.Plugin>>();
        try
        {
            _netMqPubServer = IAppHost.GetService<NetMQPUBServer>();
            _netMqPubServer.Start();
            logger.LogInformation("NetMQ PUB server started successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start NetMQ PUB server: {Message}", ex.Message);
        }
    }

    private void StopNetMqPubServer()
    {
        var logger = IAppHost.GetService<ILogger<IslandMQ.Plugin>>();
        if (_netMqPubServer != null)
        {
            try
            {
                _netMqPubServer.Stop();
                logger.LogInformation("NetMQ PUB server stopped successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop NetMQ PUB server: {Message}", ex.Message);
            }
            finally
            {
                try
                {
                    _netMqPubServer.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispose NetMQ PUB server: {Message}", ex.Message);
                }
                _netMqPubServer = null;
            }
        }
    }
}
