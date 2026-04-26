using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;

namespace Alife.Function.Memory;

public record MemoryMeta(int Level, DateTime StartTime, DateTime EndTime);

/// <summary>
/// 记忆核心管理器。协调存储、索引和压缩逻辑。
/// 实现层级化索引关系：每个摘要记录都描述了其涵盖的对话范围和时间跨度。
/// </summary>
public class MemoryManager
{
    public MemoryManager(TextCompressor compressor, TextVectorizer vectorizer, string storagePath,
        int compressionThreshold, int compressionCount, int maxCompressionLevel)
    {
        this.compressionThreshold = compressionThreshold;
        this.compressionCount = compressionCount;
        this.maxCompressionLevel = maxCompressionLevel;

        this.compressor = compressor;
        historyStoragePath = $"{storagePath}/History.json";
        memoryStorage = new MemoryStorage(storagePath, vectorizer);

        if (!Directory.Exists(storagePath))
            Directory.CreateDirectory(storagePath);
    }

    public async Task<bool> Filter(ChatHistory chatHistory)
    {
        //跳过系统提示词
        int contentIndex = 0;
        for (; contentIndex < chatHistory.Count; contentIndex++)
        {
            if (chatHistory[contentIndex].Role != AuthorRole.System)
                break;
        }

        //遍历每个层级的聊天记录
        int areaLevel = -1;
        int areaStart = -1;
        int areaCount = -1;
        int areaCompressionThreshold = -1;
        int areaCompressionCount = -1;
        for (; contentIndex < chatHistory.Count; contentIndex++)
        {
            ChatMessageContent currentContent = chatHistory[contentIndex];
            MemoryMeta currentMemoryMeta = GetMemoryMetaData(currentContent);

            int currentLevel = currentMemoryMeta.Level;
            if (areaLevel != currentLevel)
            {
                //进入一个区域
                areaLevel = currentLevel;
                areaStart = contentIndex;
                areaCount = 1;
                areaCompressionThreshold = areaLevel == 0 ? compressionThreshold : 4;
                areaCompressionCount = areaLevel == 0 ? compressionCount : 3;
            }
            else
            {
                areaCount++;
            }

            if (areaCount >= areaCompressionThreshold && areaLevel < maxCompressionLevel)
            {
                //压缩记忆
                DateTime startTime = GetMemoryMetaData(chatHistory[areaStart]).StartTime;
                DateTime endTime = currentMemoryMeta.EndTime;
                string content = PickContent(chatHistory, areaStart, areaStart + areaCompressionCount);
                content = Regex.Replace(content, "^\\[记忆存档.*$", "", RegexOptions.Multiline);
                content = Regex.Replace(content, "^存档索引.*$", "", RegexOptions.Multiline);
                string? summary = await compressor.Compress(content);
                if (summary == null)
                    return false;

                //提取并保存旧的记录
                string name = await SaveMemory(new MemoryMeta(areaLevel, startTime, endTime), summary, content);
                for (int index = areaStart + areaCompressionCount - 1; index >= areaStart; index--)
                    memoryMetaDatas.Remove(chatHistory[index]);
                chatHistory.RemoveRange(areaStart, areaCompressionCount);

                //增加新的记录
                summary = $"""
                           [记忆存档(L{areaLevel})]
                           存档索引：{name}（可以通过索引查到被归档的原始完整内容）
                           发生时间：{startTime}到{endTime}
                           事件概述：{summary}
                           """;
                ChatMessageContent compressedContent = new(AuthorRole.Assistant, summary);
                chatHistory.Insert(areaStart, compressedContent);
                memoryMetaDatas[compressedContent] = new MemoryMeta(areaLevel + 1, startTime, endTime);

                Console.WriteLine($"压缩记忆：{name}");

                return true;
            }
        }

        return false;
    }
    public async Task InsertMemory(ChatHistory chatHistory, int level, string summary, string content, DateTime startTime, DateTime endTime)
    {
        // 保存到持久化存储
        string index = await SaveMemory(new MemoryMeta(level, startTime, endTime), summary, content);

        // 构建存档显示内容
        string archiveContent = $"""
                                 [记忆存档(L{level})]
                                 存档索引：{index}
                                 发生时间：{startTime}到{endTime}
                                 事件概述：{summary}
                                 """;
        ChatMessageContent compressedContent = new(AuthorRole.Assistant, archiveContent);

        // 寻找插入位置（同级区域的最下方）
        int insertIndex = -1;
        int contentIndex = 0;
        // 跳过系统提示词
        for (; contentIndex < chatHistory.Count; contentIndex++)
        {
            if (chatHistory[contentIndex].Role != AuthorRole.System)
                break;
        }

        // 查找该层级的最后一个位置
        bool foundLevel = false;
        for (int i = contentIndex; i < chatHistory.Count; i++)
        {
            int currentLevel = GetMemoryMetaData(chatHistory[i]).Level;
            if (currentLevel == level)
            {
                foundLevel = true;
                insertIndex = i + 1;
            }
            else if (foundLevel)
            {
                // 已经过了该层级的连续区域
                break;
            }
            else if (currentLevel < level)
            {
                // 还没找到该层级，但已经到了更低层级（说明该层级不存在，应插在更低层级之前）
                insertIndex = i;
                break;
            }
        }

        if (insertIndex == -1)
            insertIndex = chatHistory.Count;

        chatHistory.Insert(insertIndex, compressedContent);
        memoryMetaDatas[compressedContent] = new MemoryMeta(level + 1, startTime, endTime);
    }
    public void RemoveMemory(ChatHistory chatHistory, ChatMessageContent content)
    {
        if (chatHistory.Remove(content))
        {
            memoryMetaDatas.Remove(content);
        }
    }
    public void SaveHistory(ChatHistory chatHistory)
    {
        List<HistoryRecord> history = new List<HistoryRecord>();
        foreach (ChatMessageContent chatMessageContent in chatHistory.Where(content => content.Role != AuthorRole.System))
        {
            if (chatMessageContent.Content == null)
                continue;
            history.Add(new HistoryRecord(
                chatMessageContent.Role,
                chatMessageContent.Content,
                GetMemoryMetaData(chatMessageContent)
            ));
        }
        File.WriteAllText(historyStoragePath, JsonConvert.SerializeObject(history, Formatting.Indented));
    }
    public void LoadHistory(ChatHistory chatHistory)
    {
        if (File.Exists(historyStoragePath) == false)
            return;

        string historyJson = File.ReadAllText(historyStoragePath);
        List<HistoryRecord>? history = JsonConvert.DeserializeObject<List<HistoryRecord>>(historyJson);
        if (history == null)
            return;

        foreach (HistoryRecord historyRecord in history)
        {
            ChatMessageContent chatMessageContent = new(historyRecord.Role, historyRecord.Content);
            chatHistory.Add(chatMessageContent);
            memoryMetaDatas.Add(chatMessageContent, historyRecord.MemoryMeta);
        }
    }
    public Task<string?> ReadMemory(string index)
    {
        int level = int.Parse(index[..index.IndexOf('-')]);
        return memoryStorage.LoadAsync(level, index);
    }
    public async Task<List<SearchResult>> SearchMemory(string query, int count, DateTime? startTime, DateTime? endTime)
    {
        return await memoryStorage.SearchAsync(query, count, startTime, endTime);
    }
    public MemoryMeta GetMemoryMetaData(ChatMessageContent content)
    {
        if (memoryMetaDatas.TryGetValue(content, out MemoryMeta? data) == false)
        {
            data = new MemoryMeta(0, DateTime.Now, DateTime.Now);
            memoryMetaDatas.Add(content, data);
        }

        return data;
    }
    
    record HistoryRecord(AuthorRole Role, string Content, MemoryMeta MemoryMeta);

    readonly int compressionThreshold;
    readonly int compressionCount;
    readonly int maxCompressionLevel;
    readonly TextCompressor compressor;
    readonly MemoryStorage memoryStorage;
    readonly string historyStoragePath;
    readonly Dictionary<ChatMessageContent, MemoryMeta> memoryMetaDatas = new Dictionary<ChatMessageContent, MemoryMeta>();


    async Task<string> SaveMemory(MemoryMeta memoryMeta, string summary, string content)
    {
        string name = $"{memoryMeta.Level}-{memoryMeta.StartTime:yyyyMMddHHmmss}-{memoryMeta.EndTime:yyyyMMddHHmmss}";
        await memoryStorage.SaveAsync(name, memoryMeta.Level, summary, content, memoryMeta.StartTime, memoryMeta.EndTime);
        return name;
    }
    string PickContent(ChatHistory chatHistory, int start, int end)
    {
        StringBuilder stringBuilder = new();

        for (int index = start; index < end; index++)
        {
            ChatMessageContent content = chatHistory[index];
            stringBuilder.AppendLine($"【{content.Role}】：\n{content.Content}\n");
        }

        return stringBuilder.ToString();
    }
}
