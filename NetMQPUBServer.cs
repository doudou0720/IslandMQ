using System;
using System.Threading;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace IslandMQ;

public class NetMQPUBServer : IDisposable
{
    private PublisherSocket? _serverSocket;
    private Thread? _serverThread;
    private volatile bool _isRunning;
    private readonly string _endpoint;
    private readonly ILogger<NetMQPUBServer> _logger;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    

    public event EventHandler<string>? ErrorOccurred;

    public NetMQPUBServer(string endpoint = "tcp://127.0.0.1:5556")
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQPUBServer>>();
        _endpoint = endpoint;
    }

    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQPUBServer));
        }
    }

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
                _logger.LogWarning("Previous PUB server thread still alive, waiting for exit...");
                bool waited = false;
                lock (_disposeLock)
                {
                    if (!_disposed)
                    {
                        waited = _threadExitEvent.Wait(3000);
                    }
                }
                if (!waited && !_disposed)
                {
                    _logger.LogError("Previous thread still running, cannot start new PUB server.");
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

    public void Stop()
    {
        CheckDisposed();
        lock (_threadLock)
        {
            CheckDisposed();
            _isRunning = false;
            if (_serverThread != null)
            {
                bool needsForcedDispose = false;
                
                if (_serverThread.IsAlive)
                {
                    bool eventSignaled = false;
                    lock (_disposeLock)
                    {
                        if (!_disposed)
                        {
                            eventSignaled = _threadExitEvent.Wait(2000);
                        }
                    }
                    
                    if (!eventSignaled && !_disposed)
                    {
                        _logger.LogWarning("PUB server thread did not signal exit within 2000ms, forcing join.");
                        if (!_serverThread.Join(5000))
                        {
                            _logger.LogError("PUB server thread still running after 5000ms, proceeding with socket disposal.");
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

            _logger.LogInformation("NetMQ PUB server started at {Endpoint}", _endpoint);

            while (_isRunning)
            {
                // PUB服务只需要等待消息发布，不需要接收消息
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            _logger.LogError(ex, "PUB server error: {Message}", ex.Message);
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

    public void Publish(string message)
    {
        CheckDisposed();
        try
        {
            var socket = _serverSocket;
            if (socket != null && _isRunning)
            {
                socket.SendFrame(message);
                _logger.LogInformation("Published: {Message}", message);
            }
            else
            {
                _logger.LogWarning("Cannot publish message, server not running.");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            _logger.LogError(ex, "Error publishing message: {Message}", ex.Message);
        }
    }

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
                _logger.LogError(ex, "Error disposing PUB socket: {Message}", ex.Message);
            }
        }
    }

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
        
        Stop();
        
        lock (_disposeLock)
        {
            _threadExitEvent.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}