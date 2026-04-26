using System.Text.RegularExpressions;

namespace Alife.Function.QChat;

/// <summary>
/// OneBot CQ 码与富文本处理工具
/// </summary>
public static class OneBotSegment
{
    public static string GetSourceTag(this OneBotMessageEvent message)
    {
        string groupLabel = $"{message.GroupId}({message.GroupName})";
        string sayerLabel = $"{message.UserId}({message.Sender?.Nickname})";
        string source = message.MessageType == OneBotMessageType.Group
            ? $"[群聊 {groupLabel}, 发言人 {sayerLabel}]"
            : $"[私聊 {sayerLabel}]";
        return source;
    }
    public static async Task<string> GetPlainMessage(this OneBotMessageEvent message, OneBotClient oneBotClient)
    {
        //解读引用文本
        string rawMessage = message.RawMessage;
        long? replyId = GetReplyId(rawMessage);
        if (replyId != null)
        {
            OneBotMessageEvent? quoted = await oneBotClient.GetMessage(replyId.Value);
            string quotedText = quoted != null
                ? $"[回复 {quoted.UserId} 的消息: {ToPlainText(quoted.RawMessage)}]"
                : "[回复其他消息]";
            rawMessage = ReplaceReply(rawMessage, quotedText);
        }

        //解读@消息
        return rawMessage;
    }

    /// <summary>
    /// 检查消息是否提到特定的 QQ 号
    /// </summary>
    public static bool IsAtMe(this OneBotMessageEvent message, long selfId)
    {
        return message.RawMessage.Contains($"[CQ:at,qq={selfId}]") || message.RawMessage.Contains($"[CQ:at,qq={selfId},");
    }

    /// <summary>
    /// 将 [CQ:at,qq=...] 替换为可读的 @标记。
    /// </summary>
    public static string ReplaceAt(string message, long botId)
    {
        message = message.Replace($"[CQ:at,qq={botId}]", "@我");
        return Regex.Replace(message, @"\[CQ:at,qq=(?<qq>\d+)[^\]]*\]", "@${qq}");
    }
    public static bool IsFile(string message)
    {
        return message.Contains("[CQ:file");
    }

    /// <summary>
    /// 提取消息中的回复消息 ID（如果存在）。
    /// </summary>
    public static long? GetReplyId(string message)
    {
        Match match = Regex.Match(message, @"\[CQ:reply,id=(?<id>-?\d+)");
        if (match.Success == false) return null;
        if (long.TryParse(match.Groups["id"].Value, out long id))
            return id;
        return null;
    }

    /// <summary>
    /// 将 [CQ:reply,...] 替换为可读文本。
    /// </summary>
    public static string ReplaceReply(string message, string replacement)
    {
        return Regex.Replace(message, @"\[CQ:reply[^\]]*\]", replacement);
    }

    /// <summary>
    /// 尝试从消息中提取 CQ 码中的 file_id
    /// </summary>
    public static string? GetFileId(string message)
    {
        Match match = Regex.Match(message, @"\[CQ:file,.*?file_id=(?<id>[^,\]]+)");
        if (match.Success == false) return null;
        return match.Groups["id"].Value;
    }
    public static string? GetFileName(string message)
    {
        Match match = Regex.Match(message, @"file=(?<name>[^,\]]+)");
        if (match.Success == false) return null;
        return match.Groups["name"].Value;
    }
    public static long GetFileSize(string message)
    {
        Match match = Regex.Match(message, @"file_size=(?<size>\d+)");
        if (match.Success == false) return -1;
        if (long.TryParse(match.Groups["size"].Value, out long result))
            return result;
        return -1;
    }


    /// <summary>
    /// 构造 At 消息片段
    /// </summary>
    public static string At(long userId) => $"[CQ:at,qq={userId}]";

    /// <summary>
    /// 构造表情片段
    /// </summary>
    public static string Face(int id) => $"[CQ:face,id={id}]";

    /// <summary>
    /// 构造图片片段
    /// </summary>
    public static string Image(string file) => $"[CQ:image,file={file}]";


    /// <summary>
    /// 从消息中提取所有图片 URL
    /// </summary>
    public static List<string> ExtractImageUrls(string message)
    {
        var urls = new List<string>();
        if (string.IsNullOrEmpty(message)) return urls;

        var matches = Regex.Matches(message, @"\[CQ:image,.*?url=(?<url>http[s]?://[^,\]]+)");
        foreach (Match match in matches)
        {
            urls.Add(match.Groups["url"].Value);
        }
        return urls;
    }

    /// <summary>
    /// 转换为纯文本（移除或替换 CQ 码）
    /// </summary>
    public static string ToPlainText(string message)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        // 移除所有 CQ 码，保留文本部分
        return Regex.Replace(message, @"\[CQ:.*?\]", "").Trim();
    }
}
