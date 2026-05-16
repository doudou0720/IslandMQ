using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using IslandMQ.ViewModels;

namespace IslandMQ.Settings
{
    /// <summary>
    /// IslandMQ插件的设置页面，提供服务器配置的UI界面。
    /// </summary>
    [SettingsPageInfo(
        id: "islandmq.settings",
        name: "IslandMQ"
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

                BasicSettingsControl basicControl = new BasicSettingsControl { DataContext = _viewModel };
                _viewModel.CurrentContent = basicControl;

                MainNavigation.SelectedItem = MainNavigation.MenuItems[0];
            }
        }

        private void MainNavigation_ItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
        {
            if (e.InvokedItem is NavigationViewItem item && item.Tag is string tag)
            {
                switch (tag)
                {
                    case "basic":
                        _viewModel.CurrentContent = new BasicSettingsControl { DataContext = _viewModel };
                        break;
                    case "about":
                        _viewModel.CurrentContent = new AboutControl { DataContext = _viewModel };
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
