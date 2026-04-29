using System.Text;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Interpreter;
using Microsoft.SemanticKernel;

namespace Alife.Implement;

[Plugin("函数调用", "为AI增加一种基于Xml的流式函数执行功能，实现快速实时的交互能力。", launchOrder: -1000)]
public class InterpreterService : InteractivePlugin<InterpreterService>
{
    // [XmlFunction("help", order: 1000)]
    // [Description("查看指定工具的详细使用文档。当你发现系统提示中存在某个工具但没有详细说明时，可以调用此工具来获取详细说明。")]
    // public void InspectTool(
    //     XmlExecutorContext context,
    //     [Description("工具的名称（即来源名）")] string name)
    // {
    //     if (context.CallMode != CallMode.OneShot)
    //         return;
    //
    //     XmlHandler? handler = handlerTable.Handlers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    //     Poke(handler != null ? handlerTable.Document(handler) : $"未找到名为 '{name}' 的工具。");
    // }

    public void RegisterHandler(XmlHandler handler) => handlerTable.Register(handler);
    public void UnregisterHandler(XmlHandler handler) => handlerTable.Unregister(handler);

    public void RegisterHandler(object handler) => handlerTable.Register(new XmlHandler(handler));
    public void UnregisterHandler(object handler) => handlerTable.Unregister(new XmlHandler(handler));

    readonly XmlHandlerTable handlerTable = new();
    XmlStreamParser parser = null!;
    XmlStreamExecutor executor = null!;

    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        //创建xml解析执行器等
        parser = new XmlStreamParser("think");
        executor = new XmlStreamExecutor(
            parser,
            handlerTable,
            ["，", "。", "！", "？", "......", "~"],
            minBreakingLength: 9
        );

        //统计隐式工具
        StringBuilder implicitSummary = new();
        foreach (XmlHandler handler in handlerTable.Handlers)
        {
            if (handler.IsImplicit)
                implicitSummary.AppendLine($"- {handler.Name}：{handler.Description}");
        }

        //注入使用说明
        string prompt = $"""
                         一般情况下你可以直接输出文本，但有时你也可以通过输出特定的xml标签来实现功能调用（有些特殊场景你也必须要使用标签回复）。

                         ## 标准用法
                         <say> <!-- 将说话内容放在的say区域中以实现说话输出。 -->
                         主人你看我~
                         可以一边跳舞
                         <mtn /> <!-- say 期间嵌套‘&lt;dance&gt;’实现了边说话边执行动作。 -->
                         一边说话噢。
                         另外我还可以通过 左尖括号python右尖括号 执行脚本呢！ <!-- 通过用代词描述‘左尖括号、右尖括号’来避免输出xml符号。 -->
                         </say>
                         <python> <!-- 因为python执行需要时间，在结尾调用比较合适。 -->
                         print('Hello World!')
                         <python>

                         ## 目前支持的标签和说明文档
                         {handlerTable.Document()}
                         """;

        chatActivity.ChatBot.ChatHistory.AddSystemMessage(prompt);
        chatActivity.ChatBot.ChatReceived += OnChatReceived;
        chatActivity.ChatBot.ChatSent += OnChatSent;
        chatActivity.ChatBot.ChatOver += OnChatOver;
        executor.Error += (tag, exception) => OnError(tag, exception, chatActivity.ChatBot);
    }
    public override async Task DestroyAsync()
    {
        await Task.Run(async () => {
            while (executor.IsIdle == false)
                await Task.Yield();
        });
        await executor.DisposeAsync();

        await base.DestroyAsync();
    }
    void OnChatSent(string _)
    {
        executor.Reset();
    }
    void OnChatOver()
    {
        executor.Flush();
    }
    void OnChatReceived(string obj)
    {
        executor.Feed(obj);
    }
    void OnError(string tag, Exception exception, ChatBot chatBot)
    {
        chatBot.Poke($"""
                      [{nameof(InterpreterService)}] 执行 &lt;{tag}&gt; 时出错。

                      错误信息如下：
                      {exception.Message}

                      你可以尝试检查：
                      1. xml语法格式是否无误（比如你没有转义就直接把标签当普通文本输出了？）
                      2. 调用时是否满足文档的使用要求。
                      """);

        AlifeTerminal.LogInfo(exception.Message);
    }
}
