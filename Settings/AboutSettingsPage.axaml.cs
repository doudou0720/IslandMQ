using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using IslandMQ.ViewModels;

namespace IslandMQ.Settings;

/// <summary>
/// IslandMQ插件的关于页面，显示插件信息和服务器状态。
/// </summary>
[SettingsPageInfo(
    id: "islandmq.about",
    name: "关于",
    category: SettingsPageCategory.External
)]
public partial class AboutSettingsPage : SettingsPageBase
{
    private IslandMQSettingsViewModel _viewModel = null!;

    /// <summary>
    /// 初始化AboutSettingsPage的新实例。
    /// </summary>
    public AboutSettingsPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        Services.IslandMQSettingsService settingsService = IAppHost.GetService<Services.IslandMQSettingsService>();
        if (settingsService != null)
        {
            _viewModel = new IslandMQSettingsViewModel(settingsService.Settings, settingsService.Save);
            DataContext = _viewModel;
        }
    }
}
