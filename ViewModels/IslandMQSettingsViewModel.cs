using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
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
    /// 获取或设置服务器的IP地址。
    /// </summary>
    public string ServerIp
    {
        get => _settings.ServerIp;
        set
        {
            if (_settings.ServerIp != value)
            {
                _settings.ServerIp = value;
                OnPropertyChanged();
                Save();
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
                Save();
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
                Save();
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
                Save();
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
                Save();
            }
        }
    }

    /// <summary>
    /// 执行保存操作。
    /// </summary>
    private void Save()
    {
        _saveAction?.Invoke();
    }
}