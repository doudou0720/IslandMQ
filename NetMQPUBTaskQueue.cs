using System;
using System.Collections.Concurrent;
using System.Threading;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace IslandMQ;

public class NetMQPUBTaskQueue : IDisposable
{
    private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private Thread? _processingThread;
    private volatile bool _isRunning;
    private readonly ManualResetEventSlim _threadExitEvent = new ManualResetEventSlim(true);
    private readonly object _threadLock = new object();
    private volatile bool _disposed;
    private readonly object _disposeLock = new object();
    private readonly ILogger<NetMQPUBTaskQueue> _logger;
    private readonly Action<string> _publishAction;

    public NetMQPUBTaskQueue(Action<string> publishAction)
    {
        _logger = IAppHost.GetService<ILogger<IslandMQ.NetMQPUBTaskQueue>>();
        _publishAction = publishAction;
    }

    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NetMQPUBTaskQueue));
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
            if (_processingThread != null && _processingThread.IsAlive)
            {
                _logger.LogWarning("Previous processing thread still alive, waiting for exit...");
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
                    _logger.LogError("Previous thread still running, cannot start new task queue.");
                    return;
                }
            }

            _isRunning = true;
            _processingThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "NetMqPubTaskQueueThread"
            };
            _processingThread.Start();
        }
    }

    private void StopInternal()
    {
        lock (_threadLock)
        {
            _isRunning = false;
            if (_processingThread != null)
                {
                    if (_processingThread.IsAlive)
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
                            _logger.LogWarning("Task queue thread did not signal exit within 2000ms, forcing join.");
                            if (!_processingThread.Join(5000))
                            {
                                _logger.LogError("Task queue thread still running after 5000ms, proceeding with disposal.");
                            }
                        }
                    }
                    
                    _processingThread = null;
                }
        }
    }
    
    public void Stop()
    {
        CheckDisposed();
        StopInternal();
    }

    private void ProcessQueue()
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
            _logger.LogInformation("NetMQ PUB task queue started processing messages");

            while (_isRunning)
            {
                try
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            _publishAction(message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing message from queue: {Message}", ex.Message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message queue: {Message}", ex.Message);
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task queue processing thread error: {Message}", ex.Message);
        }
        finally
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _threadExitEvent.Set();
                }
            }
        }
    }

    public void EnqueueMessage(string message)
    {
        CheckDisposed();
        try
        {
            _messageQueue.Enqueue(message);
            _logger.LogDebug("Message enqueued for publishing: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing message: {Message}", ex.Message);
        }
    }

    public int QueueCount => _messageQueue.Count;

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
