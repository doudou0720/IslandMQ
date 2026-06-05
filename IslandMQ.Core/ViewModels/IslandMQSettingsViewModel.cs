using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IslandMQ.Models;

namespace IslandMQ.ViewModels;

/// <summary>
/// IslandMQ设置页面的视图模型，负责处理设置界面与数据模型的绑定。
/// </summary>
public partial class IslandMQSettingsViewModel : ObservableObject
{
    private readonly IslandMQSettings _settings;
    private readonly Action _saveAction;
    private Control? _currentContent;

    /// <summary>
    /// 初始化IslandMQSettingsViewModel的新实例。
    /// </summary>
    /// <param name="settings">设置数据模型。</param>
    /// <param name="saveAction">保存设置的操作。</param>
    public IslandMQSettingsViewModel(IslandMQSettings settings, Action saveAction)
    {
        _settings = settings;
        _saveAction = saveAction;
    }

    /// <summary>
    /// 获取或设置当前显示的内容控件。
    /// </summary>
    public Control? CurrentContent
    {
        get => _currentContent;
        set
        {
            if (_currentContent != value)
            {
                _currentContent = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 获取或设置 NetMQ 服务器的 IP 地址。
    /// </summary>
    public string NetMqServerIp
    {
        get => _settings.NetMqServerIp;
        set
        {
            if (_settings.NetMqServerIp != value)
            {
                HasChanges = true;
                _settings.NetMqServerIp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取或设置 HTTP 服务器的 IP 地址。
    /// </summary>
    public string HttpServerIp
    {
        get => _settings.HttpServerIp;
        set
        {
            if (_settings.HttpServerIp != value)
            {
                HasChanges = true;
                _settings.HttpServerIp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取或设置是否启用REQ服务器。
    /// </summary>
    public bool IsReqServerEnabled
    {
        get => _settings.IsReqServerEnabled;
        set
        {
            if (_settings.IsReqServerEnabled != value)
            {
                _settings.IsReqServerEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReqServerSettingsVisible));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
                HasChanges = true;
            }
        }
    }

    /// <summary>
    /// 获取REQ服务器设置是否可见（当服务器启用时显示）。
    /// </summary>
    public bool IsReqServerSettingsVisible => IsReqServerEnabled;

    /// <summary>
    /// 获取或设置REQ服务器的端口号。
    /// </summary>
    public int ReqServerPort
    {
        get => _settings.ReqServerPort;
        set
        {
            if (_settings.ReqServerPort != value)
            {
                _settings.ReqServerPort = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
                HasChanges = true;
            }
        }
    }

    /// <summary>
    /// 获取或设置是否启用PUB服务器。
    /// </summary>
    public bool IsPubServerEnabled
    {
        get => _settings.IsPubServerEnabled;
        set
        {
            if (_settings.IsPubServerEnabled != value)
            {
                _settings.IsPubServerEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPubServerSettingsVisible));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取PUB服务器设置是否可见（当服务器启用时显示）。
    /// </summary>
    public bool IsPubServerSettingsVisible => IsPubServerEnabled;

    /// <summary>
    /// 获取或设置PUB服务器的端口号。
    /// </summary>
    public int PubServerPort
    {
        get => _settings.PubServerPort;
        set
        {
            if (_settings.PubServerPort != value)
            {
                _settings.PubServerPort = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
                HasChanges = true;
            }
        }
    }

    /// <summary>
    /// 获取或设置是否启用HTTP服务器。
    /// </summary>
    public bool IsHttpServerEnabled
    {
        get => _settings.IsHttpServerEnabled;
        set
        {
            if (_settings.IsHttpServerEnabled != value)
            {
                _settings.IsHttpServerEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHttpServerSettingsVisible));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取HTTP服务器设置是否可见（当服务器启用时显示）。
    /// </summary>
    public bool IsHttpServerSettingsVisible => IsHttpServerEnabled;

    /// <summary>
    /// 获取或设置HTTP服务器的端口号。
    /// </summary>
    public int HttpServerPort
    {
        get => _settings.HttpServerPort;
        set
        {
            if (_settings.HttpServerPort != value)
            {
                _settings.HttpServerPort = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PortConflictError));
                OnPropertyChanged(nameof(CanSave));
                HasChanges = true;
            }
        }
    }

    /// <summary>
    /// 获取或设置是否启用 CORS。
    /// </summary>
    public bool IsCorsEnabled
    {
        get => _settings.IsCorsEnabled;
        set
        {
            if (_settings.IsCorsEnabled != value)
            {
                _settings.IsCorsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCorsSettingsVisible));
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取 CORS 设置是否可见。
    /// </summary>
    public bool IsCorsSettingsVisible => IsHttpServerEnabled && IsCorsEnabled;

    /// <summary>
    /// 获取或设置允许的 CORS 来源。
    /// </summary>
    public string CorsAllowedOrigins
    {
        get => _settings.CorsAllowedOrigins;
        set
        {
            if (_settings.CorsAllowedOrigins != value)
            {
                HasChanges = true;
                _settings.CorsAllowedOrigins = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    /// <summary>
    /// 获取或设置插件版本号。
    /// </summary>
    public string PluginVersion { get; } = typeof(IslandMQSettingsViewModel).Assembly.GetName().Version?.ToString() ?? "未知";

    /// <summary>
    /// 获取是否有未保存的更改。
    /// </summary>
    public bool HasChanges { get; private set; }

    /// <summary>
    /// 获取端口冲突错误信息，如果没有冲突则为空。
    /// </summary>
    public string PortConflictError
    {
        get
        {
            if (IsReqServerEnabled && IsPubServerEnabled && ReqServerPort == PubServerPort)
            {
                return "REQ 和 PUB 端口不能相同";
            }
            if (IsReqServerEnabled && IsHttpServerEnabled && ReqServerPort == HttpServerPort)
            {
                return "REQ 和 HTTP 端口不能相同";
            }
            if (IsPubServerEnabled && IsHttpServerEnabled && PubServerPort == HttpServerPort)
            {
                return "PUB 和 HTTP 端口不能相同";
            }
            return "";
        }
    }

    /// <summary>
    /// 获取是否可以保存（没有端口冲突）。
    /// </summary>
    public bool CanSave => string.IsNullOrEmpty(PortConflictError);

    /// <summary>
    /// 获取保存成功提示信息。
    /// </summary>
    public string SaveSuccessMessage { get; private set; } = "";

    /// <summary>
    /// 保存设置。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrEmpty(PortConflictError))
        {
            _saveAction?.Invoke();
            HasChanges = false;
            OnPropertyChanged(nameof(CanSave));

            // 显示保存成功提示
            SaveSuccessMessage = "保存成功";
            OnPropertyChanged(nameof(SaveSuccessMessage));

            // 3秒后清除成功提示
            Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SaveSuccessMessage = "";
                    OnPropertyChanged(nameof(SaveSuccessMessage));
                });
            });
        }
    }

    /// <summary>
    /// 重置为默认值。
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        _settings.NetMqServerIp = "127.0.0.1";
        _settings.HttpServerIp = "127.0.0.1";
        _settings.IsReqServerEnabled = true;
        _settings.ReqServerPort = 5555;
        _settings.IsPubServerEnabled = true;
        _settings.PubServerPort = 5556;
        _settings.IsHttpServerEnabled = false;
        _settings.HttpServerPort = 8080;
        _settings.IsCorsEnabled = false;
        _settings.CorsAllowedOrigins = "";
        HasChanges = true;
        OnPropertyChanged(nameof(NetMqServerIp));
        OnPropertyChanged(nameof(HttpServerIp));
        OnPropertyChanged(nameof(IsReqServerEnabled));
        OnPropertyChanged(nameof(ReqServerPort));
        OnPropertyChanged(nameof(IsPubServerEnabled));
        OnPropertyChanged(nameof(PubServerPort));
        OnPropertyChanged(nameof(IsHttpServerEnabled));
        OnPropertyChanged(nameof(HttpServerPort));
        OnPropertyChanged(nameof(IsCorsEnabled));
        OnPropertyChanged(nameof(CorsAllowedOrigins));
        OnPropertyChanged(nameof(PortConflictError));
        OnPropertyChanged(nameof(CanSave));
    }
}
