using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using IslandMQ.ViewModels;

namespace IslandMQ.Settings
{
    /// <summary>
    /// IslandMQ插件的设置页面，提供服务器配置的UI界面。
    /// </summary>
    [SettingsPageInfo(
        id: "islandmq.settings",
        name: "基本设置",
        category: SettingsPageCategory.External
    )]
    public partial class IslandMQSettingsPage : SettingsPageBase
    {
        private IslandMQSettingsViewModel _viewModel = null!;

        /// <summary>
        /// 初始化IslandMQSettingsPage的新实例。
        /// </summary>
        public IslandMQSettingsPage()
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

                // 默认显示基本设置
                _viewModel.CurrentContent = new BasicSettingsControl { DataContext = _viewModel };
            }
        }
    }
}
