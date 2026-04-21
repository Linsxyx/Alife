using Alife.Framework;
using System.Collections.Concurrent;

namespace Alife.Components.Services;

public class ChatMessage
{
    public string? Content { get; set; }
    public bool IsUser { get; set; }
    public bool IsInputting { get; set; }
}

/// <summary>
/// UI层的聊天消息状态管理。在角色激活后立即挂接事件，确保后台对话也能被记录。
/// 采用名称索引以确保在活动重启（Character对象被Clone）时记录依然能够持久。
/// </summary>
public static class ChatUIState
{
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _botMessages = new();
    private static readonly HashSet<string> _hookedActivityNames = new();

    public static event Action<string>? OnUIMessageChanged;
    public static event Action<string>? OnUIMessageSent;

    /// <summary>
    /// 确保指定Activity的ChatBot事件已挂接到UI消息列表。
    /// 幂等操作，重复调用安全。
    /// </summary>
    public static void EnsureHooked(ChatActivity activity)
    {
        string name = activity.Character.Name;
        var list = _botMessages.GetOrAdd(name, _ => new List<ChatMessage>());
        
        if (_hookedActivityNames.Contains(name))
            return;
        
        _hookedActivityNames.Add(name);

        activity.ChatBot.ChatSent += (obj) =>
        {
            list.Add(new ChatMessage { Content = obj, IsUser = true });
            list.Add(new ChatMessage { IsUser = false, IsInputting = true });
            OnUIMessageSent?.Invoke(name);
            OnUIMessageChanged?.Invoke(name);
        };

        activity.ChatBot.ChatReceived += (obj) =>
        {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.Content += obj;
                OnUIMessageChanged?.Invoke(name);
            }
        };

        activity.ChatBot.ChatOver += () =>
        {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.IsInputting = false;
                OnUIMessageChanged?.Invoke(name);
            }
        };
    }

    public static List<ChatMessage> GetMessages(ChatActivity activity)
    {
        EnsureHooked(activity);
        return _botMessages.GetOrAdd(activity.Character.Name, _ => new List<ChatMessage>());
    }

    public static void ClearMessages(ChatActivity activity)
    {
        if (_botMessages.TryGetValue(activity.Character.Name, out var list))
        {
            list.Clear();
        }
    }

    /// <summary>
    /// 批量挂接：扫描系统中所有已激活的Activity，确保全部已挂接。
    /// </summary>
    public static void EnsureAllHooked(ChatActivitySystem system)
    {
        foreach (var activity in system.GetAllChatActivities())
            EnsureHooked(activity);
    }
}
