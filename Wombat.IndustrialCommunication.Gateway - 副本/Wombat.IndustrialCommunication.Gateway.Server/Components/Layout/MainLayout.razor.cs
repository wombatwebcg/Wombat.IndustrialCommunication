using BootstrapBlazor.Components;
using Microsoft.AspNetCore.Components.Routing;
using System;

namespace Wombat.IndustrialCommunication.Gateway.Server.Components.Layout
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class MainLayout
    {
        private bool UseTabSet { get; set; } = true;

        private string Theme { get; set; } = "";

        private bool IsOpen { get; set; }

        private bool IsFixedHeader { get; set; } = true;

        private bool IsFixedTabHeader { get; set; } = true;

        private bool IsFixedFooter { get; set; } = true;

        private bool IsFullSide { get; set; } = true;

        private bool ShowFooter { get; set; } = true;

        private bool ShowTabInHeader { get; set; } = true;

        private List<MenuItem>? Menus { get; set; }

        /// <summary>
        /// OnInitialized 方法
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            Menus = GetIconSideMenuItems();
        }

        private static List<MenuItem> GetIconSideMenuItems()
        {
            List<MenuItem> menus = new List<MenuItem>();
            menus.Add(new MenuItem()
            {
                Text = "网关状态",
                Icon = "fa-solid fa-fw fa-flag",
                Url = "/",
                Match = NavLinkMatch.All
            });

            menus.Add(new MenuItem()
            {
                Text = "网关管理",
                Icon = "fa-solid fa-fw fa-flag",
                Items = new List<MenuItem>()
                {
                    new() { Text = "网关配置", Icon = "fa-solid fa-fw fa-flag", Url = "/gateway/gatewayconfiguration" },
                    new() { Text = "规则引擎", Icon = "fa-solid fa-fw fa-check-square", Url = "/gateway/gatewayrules" },
                    new() { Text = "网关日志", Icon = "fa-solid fa-fw fa-database", Url = "/gateway/gatewaylogs" },
                }
            });
            menus.Add(new MenuItem()
            {
                Text = "系统",
                Icon = "fa-solid fa-fw fa-database",
                Items = new List<MenuItem>()
                {
                    new() { Text = "后台日志", Icon = "fa-solid fa-fw fa-database", Url = "/system/systemlogs" },
                }
            });
            return menus;
        }

        private Task OnSideChanged(bool v)
        {
            IsFullSide = v;
            StateHasChanged();
            return Task.CompletedTask;
        }
    }
}
