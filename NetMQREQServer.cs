using System;
using System.Threading;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;

namespace IslandMQ;

public class NetMQREQServer : IDisposable
{
    private ResponseSocket? _serverSocket;
    private Thread? _serverThread;
    private volatile bool _isRunning;
    private readonly string _endpoint;
    private readonly ILogger<NetMQREQServer> _logger;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    

    public event EventHandler<string>? MessageReceived;
    public event EventHandler<Exception>? ErrorOccurred;

    public NetMQREQServer(string endpoint = "tcp://127.0.0.1:5555")
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQREQServer>>();
        _endpoint = endpoint;
    }

    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQREQServer));
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
                _logger.LogWarning("Previous server thread still alive, waiting for exit...");
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
                    _logger.LogError("Previous thread still running, cannot start new server.");
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
                        _logger.LogWarning("Server thread did not signal exit within 2000ms, forcing join.");
                        if (!_serverThread.Join(5000))
                        {
                            _logger.LogError("Server thread still running after 5000ms, proceeding with socket disposal.");
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
            _serverSocket = new ResponseSocket();
            _serverSocket.Bind(_endpoint);

            _logger.LogInformation("NetMQ server started at {Endpoint}", _endpoint);

            while (_isRunning)
            {
                
                try
                {
                    var socket = _serverSocket;
                    if (socket != null && socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out var message))
                    {
                        //TODO:测试代码待删除，业务逻辑入口
                        MessageReceived?.Invoke(this, message);
                        _logger.LogInformation("Received: {Message}", message);

                        var response = ProcessMessage(message);
                        socket.SendFrame(response);
                        _logger.LogInformation("Sent: {Response}", response);
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                    _logger.LogError(ex, "Error: {Message}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger.LogError(ex, "Server error: {Message}", ex.Message);
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

    private string ProcessMessage(string message)
    {
        return $"Server response: {message}";
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
                _logger.LogError(ex, "Error disposing socket: {Message}", ex.Message);
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
