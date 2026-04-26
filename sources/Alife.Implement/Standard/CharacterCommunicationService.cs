using System.ComponentModel;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.Extensions.DependencyInjection;

namespace Alife.Implement;

[Plugin("角色通讯", "允许 AI 在多个角色之间进行实时对话。你可以联系任何系统内的角色，即便对方暂未开启，系统也会告知状态。")]
public class CharacterCommunicationService : InteractivePlugin<CharacterCommunicationService>
{
    [XmlFunction("call")]
    [Description("与指定的角色对话。")]
    public void CallCharacter(XmlExecutorContext context, string target, string message)
    {
        // 仅在流式输出结束时执行一次
        if (context.CallMode != CallMode.OneShot)
            return;

        if (string.IsNullOrWhiteSpace(target))
        {
            Poke("[角色通讯] 必须指定目标角色名称。");
            return;
        }

        // 1. 先检查角色是否存在于系统中
        var allCharacters = characterSystem.GetAllCharacters();
        var targetCharacter = allCharacters.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetCharacter == null)
        {
            Poke("目标不存在");
            return;
        }

        // 2. 再检查目标活动是否在线
        var targetActivity = chatActivitySystem.GetAllChatActivities()
            .FirstOrDefault(a => a.Character.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetActivity != null)
        {
            // 目标在线，直接发送实时通讯消息
            targetActivity.ChatBot.Poke($"[来自 {currentName} 的消息]: {message}\n(Tips: 使用 <call> 回复对方)");
        }
        else
        {
            // 目标离线
            Poke("对方暂不在");
        }
    }

    [XmlFunction("give")]
    [Description("给指定的角色东西。")]
    public void TransferItem(XmlExecutorContext context, string target, string description)
    {
        if (context.CallMode != CallMode.OneShot)
            return;

        if (string.IsNullOrWhiteSpace(target))
        {
            Poke("[物品传递] 必须指定目标角色名称。");
            return;
        }

        // 1. 先检查角色是否存在于系统中
        var allCharacters = characterSystem.GetAllCharacters();
        var targetCharacter = allCharacters.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetCharacter == null)
        {
            Poke("角色不存在");
            return;
        }

        // 2. 再检查目标活动是否在线
        var targetActivity = chatActivitySystem.GetAllChatActivities()
            .FirstOrDefault(a => a.Character.Name.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (targetActivity != null)
        {
            targetActivity.ChatBot.Poke($"[收到来自 {currentName} 的物品]: {description}");
        }
        else
        {
            // 目标离线
            Poke("人不在");
        }
    }

    public CharacterCommunicationService(
        InterpreterService interpreterService,
        CharacterSystem characterSystem,
        ChatActivitySystem chatActivitySystem)
    {
        this.interpreterService = interpreterService;
        this.characterSystem = characterSystem;
        this.chatActivitySystem = chatActivitySystem;
    }

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);
        currentName = context.character.Name;

        // 获取所有角色并生成带有简介的列表
        var allCharacters = characterSystem.GetAllCharacters();
        string characterList = allCharacters.Any()
            ? string.Join("\n", allCharacters.Select(c =>
                $"- {c.Name}{(string.IsNullOrWhiteSpace(c.Description) ? "" : $"：{c.Description}")}"))
            : "（当前无预设角色）";

        XmlHandler xmlHandler = new(this);
        xmlHandler.Explain = $"""
                              你生活的世界除了你还有些其他人，可以通过 <call> 与他们对话，或使用 <give> 交换东西。

                              你当前可以联系其他人有：
                              {characterList}

                              社交逻辑注意：
                              1. 遵循自然的社交边界：如果你们之前从未交流过，请保持基本的礼貌但不要表现得过于熟络，你可以交朋友但也要注意分辨他们的为人，学会保户自己。
                              2. 遵循现实的经济逻辑：如果你是商贩或店主，请不要无偿赠送贵重物品。在 <give> 之前，你应该先通过 <call> 谈妥价格、交换条件或确认对方的意图。
                              """;

        interpreterService.RegisterHandler(xmlHandler);
    }

    readonly InterpreterService interpreterService;
    readonly CharacterSystem characterSystem;
    readonly ChatActivitySystem chatActivitySystem;
    string currentName = "";
}
