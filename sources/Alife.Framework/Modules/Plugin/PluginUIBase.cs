using Microsoft.AspNetCore.Components;

namespace Alife.Framework;

/// <summary>
/// 插件 UI 基类。
/// 所有的插件自定义配置/管理界面应继承此类。
/// </summary>
public abstract class PluginUIBase : ComponentBase
{
    [Parameter] public Type PluginType { get; set; } = null!; //当前插件的类型。
    [Parameter] public Character? Character { get; set; } //当前关联的角色（如果有）。
    [Parameter] public ChatActivity? ChatActivity { get; set; } //当前关联的运行活动（如果有）。
    [Parameter] public Plugin? Plugin { get; set; }

    [Parameter] public RenderFragment DefaultUI { get; set; } = null!;
}
