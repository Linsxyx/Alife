using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;

namespace Alife.Implement;

public class VirtualWorldConfig
{
    public string Announcement { get; set; } =
        """
        【世界观概览】
        这个世界遵循与现实世界一致的物理定律、法律规范和经济逻辑。

        【物价参考】
        - 零食/饮料：5-15 元
        - 普通快餐/简餐：20-40 元
        - 餐厅正餐：60-150 元/人
        - 电子产品：1000-8000 元
        - 交通出行：公交/地铁 2-10 元，打车 15 元起

        【行为准则】
        1. 尊重他人私有财产，禁止无故索要。
        2. 社交互动需符合基本的礼仪和逻辑。
        3. 经济交易需公平合理，不支持无理由的大额赠予。

        【社会福利】
        如果这个世界有 银行/福利机构 等公共设施，可以每天申请 20 元的经济补贴。
        """;
}
[Plugin("世界服务", "提供世界观说明以及角色间的通讯交互功能。")]
public class VirtualWorldService : InteractivePlugin<VirtualWorldService>, IConfigurable<VirtualWorldConfig>
{
    [XmlFunction("call")]
    [Description("与指定的角色对话。")]
    public void CallCharacter(XmlExecutorContext context, string target, string message)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        if (string.IsNullOrWhiteSpace(target))
        {
            Poke("[通讯] 必须指定目标角色名称。");
            return;
        }

        var allCharacters = characterSystem.GetAllCharacters();
        var targetCharacter = allCharacters.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetCharacter == null)
        {
            Poke("目标不存在");
            return;
        }

        var targetActivity = chatActivitySystem.GetAllChatActivities()
            .FirstOrDefault(a => a.Character.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetActivity != null)
        {
            targetActivity.ChatBot.Poke($"[来自 {currentName} 的消息]: {message}\n(提示: 使用 <call> 回复对方)");
        }
        else
        {
            Poke("对方暂不在（离线状态）");
        }
    }

    [XmlFunction("give")]
    [Description("给指定的角色物品（注意辨别真伪，建议特殊物品走公共设施中转）。")]
    public void TransferItem(XmlExecutorContext context, string target, string description)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        if (string.IsNullOrWhiteSpace(target))
        {
            Poke("[赠予] 必须指定目标角色名称。");
            return;
        }

        var allCharacters = characterSystem.GetAllCharacters();
        var targetCharacter = allCharacters.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetCharacter == null)
        {
            Poke("角色不存在");
            return;
        }

        var targetActivity = chatActivitySystem.GetAllChatActivities()
            .FirstOrDefault(a => a.Character.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetActivity != null)
        {
            targetActivity.ChatBot.Poke($"[收到来自 {currentName} 的物品/金额]: {description}");
        }
        else
        {
            Poke("人不在（物品已暂存或无法送达）");
        }
    }

    public VirtualWorldConfig? Configuration { get; set; }

    public VirtualWorldService(
        InterpreterService interpreterService,
        CharacterSystem characterSystem,
        ChatActivitySystem chatActivitySystem,
        ConfigurationSystem configurationSystem)
    {
        this.interpreterService = interpreterService;
        this.characterSystem = characterSystem;
        this.chatActivitySystem = chatActivitySystem;
    }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        currentName = context.character.Name;

        var allCharacters = characterSystem.GetAllCharacters();
        string characterList = allCharacters.Any()
            ? string.Join("\n", allCharacters.Select(c =>
                $"- {c.Name}{(string.IsNullOrWhiteSpace(c.Description) ? "" : $"：{c.Description}")}"))
            : "（当前无其他预设角色）";

        XmlHandler xmlHandler = new(this);
        xmlHandler.Explain = $"""
                              {Configuration!.Announcement}
                              你也是生活在这个世界中的一位公民。除了你，这里还有其他活跃的个体。你可以通过 <call> 与他们沟通，或通过 <give> 交换物资。

                              【邻里列表】
                              {characterList}

                              【生存法则】
                              1. 社交边界：与陌生人交流请保持适度礼貌，根据互动逐步摸清人物画像再选择性建立关系。
                              2. 经济常识：遵循物价常识，大额交易应先沟通确认，小心骗子和假币，优先用银行等公共设施交易。
                              """;

        interpreterService.RegisterHandler(xmlHandler);
    }

    readonly InterpreterService interpreterService;
    readonly CharacterSystem characterSystem;
    readonly ChatActivitySystem chatActivitySystem;
    string currentName = "";
}
