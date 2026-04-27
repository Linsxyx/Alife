using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Reflection;

// ================= 配置区域 =================
var endpoint = "https://api.deepseek.com/v1"; 
var modelId = "deepseek-reasoner"; 
var apiKey = "sk-c5318627b5f14f41b724040b3f921992";
// ===========================================

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: modelId, 
    apiKey: apiKey, 
    endpoint: new Uri(endpoint)
);

var kernel = builder.Build();
var chat = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();
history.AddUserMessage("1+1等于几？进行深度思考。");

var settings = new OpenAIPromptExecutionSettings()
{
    ExtensionData = new Dictionary<string, object>
    {
        ["extra_body"] = new { thinking = new { type = "enabled" } }
    }
};

Console.WriteLine("--- 开始测试 ---");
await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, settings, kernel))
{
    if (chunk.Content != null) Console.Write(chunk.Content);

    object? reasoning = null;
    // 1. 尝试从 Metadata 获取
    if (chunk.Metadata != null)
    {
        if (chunk.Metadata.TryGetValue("ReasoningContent", out reasoning) || 
            chunk.Metadata.TryGetValue("reasoning_content", out reasoning)) { }
    }

    // 2. 尝试通过反射从 InnerContent 获取 (探测所有字段和属性)
    if (reasoning == null && chunk.InnerContent != null)
    {
        var type = chunk.InnerContent.GetType();
        var allMembers = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var member in allMembers)
        {
            if (member.Name.Contains("reasoning", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[发现可能相关的成员: {member.Name}]");
            }
        }
    }
}

Console.WriteLine("\n--- 结束 ---");
