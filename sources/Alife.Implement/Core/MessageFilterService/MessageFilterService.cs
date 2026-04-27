using Alife.Framework;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

public class MessageFilterData
{
    public bool EnableTimestamp { get; set; } = true;
    public string MessagePrompt { get; set; } = "回复保持精简，不要插入旁白，say区域最多用一个！";
    public string PokeHeader { get; set; } = "[系统缓存消息](下方为非用户消息，请思考后再回复)";
}
[Plugin("消息滤网", "统一管理消息的提示词注入和格式化。负责添加时间戳、通用提示词以及系统消息头。", LaunchOrder = 900, EditorUI = typeof(MessageFilterServiceUI))]
public class MessageFilterService : InteractivePlugin<MessageFilterService>, IConfigurable<MessageFilterData>
{
    public MessageFilterData? Configuration { get; set; }

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);
        ChatBot.ChatSend += OnChatSend;
        ChatBot.PokeSend += OnPokeSend;
    }

    string OnChatSend(string message)
    {
        string result = message;
        if (Configuration?.EnableTimestamp == true)
            result = $"[当前时间：{DateTime.Now}]{result}";

        return $"{result}\n({Configuration?.MessagePrompt})";
    }

    string OnPokeSend(string message)
    {
        return $"{Configuration?.PokeHeader}\n{message}";
    }
}
