using System.Text.RegularExpressions;

namespace Alife.Function.QChat;

/// <summary>
/// OneBot CQ 码与富文本处理工具
/// </summary>
public static class OneBotSegment
{
    /// <summary>
    /// 检查消息是否提到特定的 QQ 号
    /// </summary>
    public static long? GetAtID(this OneBotMessageEvent message)
    {
        Match match = Regex.Match(message.RawMessage, @"\[CQ:at,qq=(?<id>-?\d+)");
        if (match.Success == false) return null;
        if (long.TryParse(match.Groups["id"].Value, out long id))
            return id;
        return null;
    }
    /// <summary>
    /// 提取消息中的回复消息 ID（如果存在）。
    /// </summary>
    public static long? GetReplyId(this OneBotMessageEvent message)
    {
        Match match = Regex.Match(message.RawMessage, @"\[CQ:reply,id=(?<id>-?\d+)");
        if (match.Success == false) return null;
        if (long.TryParse(match.Groups["id"].Value, out long id))
            return id;
        return null;
    }

    public static bool HasFile(this OneBotMessageEvent message)
    {
        return message.RawMessage.Contains("[CQ:file");
    }
    /// <summary>
    /// 尝试从消息中提取 CQ 码中的 file_id
    /// </summary>
    public static string? GetFileId(this OneBotMessageEvent message)
    {
        Match match = Regex.Match(message.RawMessage, @"\[CQ:file,.*?file_id=(?<id>[^,\]]+)");
        if (match.Success == false) return null;
        return match.Groups["id"].Value;
    }
    public static string? GetFileName(this OneBotMessageEvent message)
    {
        Match match = Regex.Match(message.RawMessage, @"file=(?<name>[^,\]]+)");
        if (match.Success == false) return null;
        return match.Groups["name"].Value;
    }
    public static long? GetFileSize(this OneBotMessageEvent message)
    {
        Match match = Regex.Match(message.RawMessage, @"file_size=(?<size>\d+)");
        if (match.Success == false) return null;
        if (long.TryParse(match.Groups["size"].Value, out long result))
            return result;
        return null;
    }
    /// <summary>
    /// 从消息中提取所有图片 URL
    /// </summary>
    public static List<string> GetImageUrls(this OneBotMessageEvent message)
    {
        List<string> urls = new();
        MatchCollection matches = Regex.Matches(message.RawMessage, @"\[CQ:image,.*?url=(?<url>http[s]?://[^,\]]+)");
        foreach (Match match in matches)
            urls.Add(match.Groups["url"].Value);
        return urls;
    }

    public static string GetSourceTag(this OneBotMessageEvent message)
    {
        string groupLabel = $"{message.GroupId}({message.GroupName})";
        string sayerLabel = $"{message.UserId}({message.Sender?.Nickname})";
        string source = message.MessageType == OneBotMessageType.Group
            ? $"[群聊 {groupLabel}, 发言人 {sayerLabel}]"
            : $"[私聊 {sayerLabel}]";
        return source;
    }
    public static async Task<string> GetReadableMessage(this OneBotMessageEvent messageEvent, OneBotClient oneBotClient)
    {
        //解读引用文本
        string message = messageEvent.RawMessage;
        long? replyId = GetReplyId(messageEvent);
        if (replyId != null)
        {
            OneBotMessageEvent? quoted = await oneBotClient.GetMessage(replyId.Value);
            string quotedText = quoted != null
                ? $"[回复 {quoted.UserId} 的消息: {GetPlainText(quoted.RawMessage)}]"
                : "[回复其他消息]";
            message = ReplaceReply(message, quotedText);
        }

        //解读@消息并清理 CQ 码
        message = ReplaceAt(message, oneBotClient.BotId);

        return message;
    }


    /// <summary>
    /// 将 [CQ:reply,...] 替换为可读文本。
    /// </summary>
    static string ReplaceReply(string message, string replacement)
    {
        return Regex.Replace(message, @"\[CQ:reply[^\]]*\]", replacement);
    }
    /// <summary>
    /// 将 [CQ:at,qq=...] 替换为可读的 @标记。
    /// </summary>
    static string ReplaceAt(string message, long botId)
    {
        message = message.Replace($"[CQ:at,qq={botId}]", "@我");
        return Regex.Replace(message, @"\[CQ:at,qq=(?<qq>\d+)[^\]]*\]", "@${qq}");
    }
    /// <summary>
    /// 转换为纯文本（移除或替换 CQ 码）
    /// </summary>
    static string GetPlainText(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        // 移除所有 CQ 码，保留文本部分
        return Regex.Replace(message, @"\[CQ:.*?\]", "").Trim();
    }
}
