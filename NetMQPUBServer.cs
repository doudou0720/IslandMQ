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
    private NetMQPUBTaskQueue? _taskQueue;
    

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
    
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
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

            _taskQueue = new NetMQPUBTaskQueue(InternalPublish);
            _taskQueue.Start();

            while (_isRunning)
            {
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
            if (_taskQueue != null)
            {
                try
                {
                    _taskQueue.Stop();
                    _taskQueue.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing task queue: {Message}", ex.Message);
                }
                _taskQueue = null;
            }
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
            if (_taskQueue != null && _isRunning)
            {
                _taskQueue.EnqueueMessage(message);
                _logger.LogDebug("Message queued for publishing: {Message}", message);
            }
            else
            {
                _logger.LogWarning("Cannot publish message, server not running or task queue not initialized.");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
            _logger.LogError(ex, "Error queueing message: {Message}", ex.Message);
        }
    }

    private void InternalPublish(string message)
    {
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