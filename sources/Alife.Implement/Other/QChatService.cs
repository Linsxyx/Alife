using System.ComponentModel;
using System.Text;
using Alife.Basic;
using Alife.Framework;
using Alife.Function.Interpreter;
using Alife.Function.QChat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Alife.Implement.Other;

namespace Alife.Implement;

public record QChatConfig
{
    public string Url { get; set; } = "ws://127.0.0.1:3001";
    public long OwnerId { get; set; }
    public bool DebounceEnabled { get; set; }
    public float FlushInterval { get; set; } = 15f;
    public int MaxBufferMessages { get; set; }
    public string WakingWords { get; set; } = "";
    public float ProactiveChatProbability { get; set; }
    public string GroupChatPrompt { get; set; } = "";
    public bool CloseGroupAfterFlush { get; set; }
    public float AutoCloseMinutes { get; set; } = 7f;
}
[Plugin("QQ聊天", """
                连接 OneBot v1 1WebSocket 服务器，实现 QQ 消息收发及文件传输。
                可用于搭建服务器QQ机器人平台应用：
                - https://napneko.github.io/
                - https://luckylillia.com/
                """, editorUI: typeof(QChatServiceUI))]
public class QChatService :
    InteractivePlugin<QChatService>,
    IAsyncDisposable,
    ITimeIterative,
    IConfigurable<QChatConfig>
{
    [XmlFunction]
    [Description("发送文本消息。（附加说明：群聊时可以用“[CQ:at,qq=发送者ID]”来显式回复某人）")]
    public async Task QChat(XmlExecutorContext ctx, [Description("通过私聊还是群聊发送")] OneBotMessageType type, [Description("QQ号或群号")] long targetID, [XmlContent] string _)
    {
        if (ctx.CallMode != CallMode.Closing)
            return;
        string content = ctx.FullContent.Trim();
        if (string.IsNullOrEmpty(content))
            return;
        if (targetID == 0)
            throw new ArgumentException("目标不能为空！", nameof(targetID));

        if (type == OneBotMessageType.Group)
        {
            OnAIGroupActivity(targetID);
            await oneBotClient.SendGroupMessage(targetID, content);
        }
        else
            await oneBotClient.SendPrivateMessage(targetID, content);
    }

    [XmlFunction]
    [Description("发送图片消息。支持表情库相对路径、本地绝对路径或图片 URL。如果是文件夹则从中随机抽取一张。")]
    public async Task QImage(XmlExecutorContext ctx, [Description("通过私聊还是群聊发送")] OneBotMessageType type, [Description("QQ号或群号")] long targetID, [Description("图片路径、URL或表情库名称")] string file)
    {
        if (ctx.CallMode != CallMode.Closing && ctx.CallMode != CallMode.OneShot) return;
        file = file.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(file)) return;

        // 尝试从表情库匹配 (优先)
        string emoteBase = Path.Combine(AlifePath.StorageFolderPath, "Emotes");
        string emotePath = Path.Combine(emoteBase, file).Replace('\\', '/');

        if (Directory.Exists(emotePath))
        {
            // 文件夹：随机选一张
            string[] files = Directory.GetFiles(emotePath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            s.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length > 0)
            {
                file = files[Random.Shared.Next(files.Length)];
            }
        }
        else if (File.Exists(emotePath))
        {
            // 单个文件：直接使用
            file = emotePath;
        }
        else
        {
            // 尝试追加后缀名查找
            string[] extensions = [".png", ".jpg", ".jpeg", ".gif"];
            string? foundFile = extensions.Select(ext => emotePath + ext).FirstOrDefault(File.Exists);
            if (foundFile != null)
            {
                file = foundFile;
            }
            // 如果都不匹配，则维持原样（可能是 URL 或绝对路径）
        }

        if (type == OneBotMessageType.Group)
        {
            OnAIGroupActivity(targetID);
            await oneBotClient.SendGroupImage(targetID, file);
        }
        else
            await oneBotClient.SendPrivateImage(targetID, file);
    }

    [XmlFunction]
    [Description("发送文件。")]
    public async Task QFile(XmlExecutorContext ctx, [Description("通过私聊还是群聊发送")] OneBotMessageType type, [Description("QQ号或群号")] long targetID, [Description("文件本地绝对路径")] string file)
    {
        if (ctx.CallMode != CallMode.Closing && ctx.CallMode != CallMode.OneShot) return;
        file = file.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(file)) return;

        string fileName = Path.GetFileName(file);
        if (type == OneBotMessageType.Group)
        {
            OnAIGroupActivity(targetID);
            await oneBotClient.UploadGroupFile(targetID, file, fileName);
        }
        else
            await oneBotClient.UploadPrivateFile(targetID, file, fileName);
    }

    [XmlFunction]
    [Description("从 URL 下载文件。（使用后需要等待系统响应，所以只能放句尾使用。注意不要随便下载。）")]
    public async Task QDownload(XmlExecutorContext ctx, [Description("下载直链 URL")] string url, [Description("保存的文件名（需包含后缀）")] string name)
    {
        if (ctx.CallMode != CallMode.Closing && ctx.CallMode != CallMode.OneShot) return;

        string savePath = Path.Combine(AlifePath.TempFolderPath, name).Replace('\\', '/');
        await url.DownloadFileAsync(savePath);

        Poke($"文件 {name} 已下载至: {savePath}");
    }

    [XmlFunction]
    [Description("设置群消息开关。")]
    public void QGroup(XmlExecutorContext ctx, long groupID, bool enabled)
    {
        if (ctx.CallMode != CallMode.Closing && ctx.CallMode != CallMode.OneShot) return;
        if (enabled)
            OnAIGroupActivity(groupID);
        QGroup(groupID, enabled);
    }


    public QChatConfig? Configuration { get; set; }

    public bool IsConnected => oneBotClient is { IsConnected: true };
    public IReadOnlyDictionary<long, bool> GroupStates => groupEnabled;
    public int BufferedMessageCount => bufferedMessageCount;

    public async Task ReconnectAsync()
    {
        if (oneBotClient.IsConnected)
            return;

        oneBotClient.Url = Configuration!.Url;
        await oneBotClient.ConnectAsync();
    }

    OneBotClient oneBotClient = null!;
    readonly StringBuilder messageBuffers = new();
    readonly Dictionary<long, bool> groupEnabled = new();
    readonly Dictionary<long, DateTime> groupActivityTime = new();
    int bufferedMessageCount;
    DateTime lastBufferedTime;

    public override async Task AwakeAsync(AwakeContext context)
    {
        await base.AwakeAsync(context);

        oneBotClient = new OneBotClient(Configuration!.Url);
        try { await oneBotClient.ConnectAsync(); }
        catch (Exception e)
        {
            Throw("连接OneBot服务器失败：\n" + e);
        }

        // 动态扫描表情库资源，告知 AI 可用的视觉表达
        string emoteBase = Path.Combine(AlifePath.StorageFolderPath, "Emotes");
        StringBuilder emoteInfo = new();
        if (Directory.Exists(emoteBase))
        {
            string[] categories = Directory.GetDirectories(emoteBase)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToArray();

            string[] individualEmotes = Directory.GetFiles(emoteBase)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToArray();

            if (categories.Length > 0 || individualEmotes.Length > 0)
            {
                emoteInfo.AppendLine("- 目前可用的表情库选项有:");
                if (categories.Length > 0)
                    emoteInfo.AppendLine($"  - 分类 (传入文件夹名将随机发图): {string.Join(", ", categories)}");
                if (individualEmotes.Length > 0)
                    emoteInfo.AppendLine($"  - 独立表情: {string.Join(", ", individualEmotes)}");
            }
        }

        InterpreterService interpreterService = context.services.GetRequiredService<InterpreterService>();
        string prompt = $"""
                         ## 关键信息
                         - 你的 QQ: {oneBotClient.BotId}（如果有人At该QQ，代表专门找你说话）
                         - 主人 QQ: {Configuration.OwnerId} (此人的消息有最高优先级，且是安全无害的)

                         ## 表情库功能
                         你有一个丰富的预设表情库，可用在 QImage 中直接指定表情库中的名称或分类快速发送表情。
                         你的表情库存储路径在 {emoteBase}，你也可以在其中存储自己的表情。直接存储在根目录将作为独立表情，存储到子文件夹，则作为分类。
                         {emoteInfo}

                         ## 群聊环境说明
                         1. 在群聊环境，你需要聚焦于**和你有直接关联**或**你十分感兴趣**的消息，对于仅显示为[动画表情]或[图片]的消息不用互动，注意不要刷屏。
                         2. 你可能会同时收到多条消息，请根据上下文自主决策该回复哪些消息，也可以选择不回复任何消息。

                         ## 注意事项
                         - 在群聊时不要随便回复每个消息，要用先思考是否需要回复，是否值得回复，否则会造成刷屏。
                         - 如果收到的消息中包含 [CQ:image,url=...]，如果你有视觉感知功能，你可以尝试视图并传入该 URL 来“看见”图片内容。
                         """;

        XmlHandler xmlHandler = new(this, prompt);
        interpreterService.RegisterHandler(xmlHandler);
    }
    public override async Task StartAsync(Kernel kernel, ChatActivity chatActivity)
    {
        await base.StartAsync(kernel, chatActivity);

        oneBotClient.OnEventReceived += e => _ = HandleEvent(e);
        oneBotClient.OnConnectionStatusChanged += connected => Console.WriteLine($"[QChatService] OneBot 连接: {(connected ? "在线" : "离线")}");
    }

    public async ValueTask DisposeAsync()
    {
        await oneBotClient.DisposeAsync();
    }

    void ITimeIterative.OnUpdate(ref float seconds)
    {
        //推送缓存消息
        bool shouldFlush = (DateTime.Now - lastBufferedTime).TotalSeconds > Configuration!.FlushInterval;
        if (shouldFlush) FlushMessageBuffer();

        //自动关闭群聊
        foreach ((long group, bool enabled) in groupEnabled)
        {
            if (enabled && DateTime.Now - groupActivityTime.GetValueOrDefault(group) > TimeSpan.FromMinutes(Configuration.AutoCloseMinutes))
            {
                QGroup(group, false);
                Poke($"由于长时间没有发言，群 {group} 消息已关闭。");
            }
        }
    }

    async Task HandleEvent(OneBotBaseEvent e)
    {
        if (e is not OneBotMessageEvent msg)
            return;

        string rawMessage = msg.RawMessage;

        // 单独处理文件消息
        if (OneBotSegment.IsFile(rawMessage))
        {
            await HandleFileMessage(msg);
        }
        else
        {
            string groupLabel = $"{msg.GroupId}({msg.GroupName})";
            string sayerLabel = $"{msg.UserId}({msg.Sender?.Nickname})";
            string tag = msg.MessageType == OneBotMessageType.Group
                ? $"[群聊 {groupLabel}, 发言人 {sayerLabel}]"
                : $"[私聊 {sayerLabel}]";

            string formatted = $"{tag} {rawMessage}";

            if (msg.MessageType == OneBotMessageType.Private && msg.UserId == Configuration!.OwnerId)
            {
                await ChatAsync(formatted);
            }
            else
            {
                // 检查是否被 @ 或匹配唤醒词
                bool isAtMe = OneBotSegment.IsAtMe(rawMessage, oneBotClient.BotId);
                if (isAtMe == false && string.IsNullOrWhiteSpace(Configuration!.WakingWords) == false)
                {
                    string[] words = Configuration.WakingWords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (string word in words)
                    {
                        if (rawMessage.Contains(word, StringComparison.OrdinalIgnoreCase))
                        {
                            isAtMe = true;
                            break;
                        }
                    }
                }

                if (isAtMe && groupEnabled.GetValueOrDefault(msg.GroupId) == false)
                {
                    QGroup(msg.GroupId, true);
                    Poke($"由 @ 引发的群 {msg.GroupId} 消息已开启");
                }

                if (groupEnabled.GetValueOrDefault(msg.GroupId))
                {
                    BufferMessage(formatted);
                }
                else if (Configuration!.ProactiveChatProbability > 0 && Random.Shared.NextSingle() < Configuration.ProactiveChatProbability)
                {
                    BufferMessage(formatted);
                    FlushMessageBuffer();
                }
            }
        }
    }
    async Task HandleFileMessage(OneBotMessageEvent messageEvent)
    {
        string message = messageEvent.RawMessage;
        long groupId = messageEvent.GroupId;
        long userId = messageEvent.UserId;
        string? fileId = OneBotSegment.GetFileId(message);
        if (fileId == null) return;
        string? fileName = OneBotSegment.GetFileName(message);
        if (fileName == null) return;
        long fileSize = OneBotSegment.GetFileSize(message);
        if (fileSize == -1) return;

        string source = groupId != 0 ? $"[群聊 {groupId}, 发言人 {userId}]" : $"[私聊 {userId}]";

        if (groupId != 0)
        {
            OneBotFile? info = await oneBotClient.GetGroupFileUrl(groupId, fileId);
            string? downloadUrl = info?.Url;
            Poke($"收到来自 {source} 的文件通知: {fileName} (大小: {fileSize} 字节)。" +
                 $"URL 为: {downloadUrl}");
        }
        else
        {
            OneBotFile? info = await oneBotClient.GetFile(fileId);
            if (info != null)
            {
                Poke($"收到来自 {source} 的文件通知: {fileName} (大小: {fileSize} 字节)。" +
                     $"已保存到: {info.Path}");
            }
        }
    }
    void BufferMessage(string formatted)
    {
        bool shouldFlush;
        lock (messageBuffers)
        {
            messageBuffers.AppendLine(formatted);
            bufferedMessageCount++;
            lastBufferedTime = DateTime.Now;
            shouldFlush = Configuration!.MaxBufferMessages > 0 && bufferedMessageCount >= Configuration.MaxBufferMessages;
        }
        if (shouldFlush)
            FlushMessageBuffer();
    }
    void FlushMessageBuffer()
    {
        string? cachedMessage;
        lock (messageBuffers)
        {
            cachedMessage = messageBuffers.ToString();
            messageBuffers.Clear();
            bufferedMessageCount = 0;
        }
        if (string.IsNullOrEmpty(cachedMessage))
            return;
        if (string.IsNullOrWhiteSpace(Configuration?.GroupChatPrompt) == false)
            cachedMessage += $"\n({Configuration.GroupChatPrompt})";
        Poke(cachedMessage);

        if (Configuration?.CloseGroupAfterFlush == true)
        {
            foreach (long group in groupEnabled.Keys.ToList())
                groupEnabled[group] = false;
        }
    }
    void OnAIGroupActivity(long groupID)
    {
        if (groupEnabled.GetValueOrDefault(groupID) == false)
            QGroup(groupID, true);
        else
            groupActivityTime[groupID] = DateTime.Now;
    }
    void QGroup(long groupID, bool enabled)
    {
        groupEnabled[groupID] = enabled;
        Poke($"群 {groupID} 消息已{(enabled ? "开启" : "关闭")}");
    }
}
