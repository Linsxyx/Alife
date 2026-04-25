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
    public static bool IsAtMe(string content, long selfId)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return content.Contains($"[CQ:at,qq={selfId}]") || content.Contains($"[CQ:at,qq={selfId},");
    }
    public static bool IsFile(string message)
    {
        return message.Contains("[CQ:file");
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
