using Alife.Framework;

namespace Alife.Components.Services;

public class ChatMessage
{
    public string? Content { get; set; }
    public bool IsUser { get; set; }
    public bool IsInputting { get; set; }
}

/// <summary>
/// UI层的聊天消息状态管理。在角色激活后立即挂接事件，确保后台对话也能被记录。
/// 纯UI层逻辑，不侵入Framework。
/// </summary>
public static class ChatUIState
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Character, List<ChatMessage>> _botMessages = new();
    private static readonly HashSet<Character> _hookedCharacters = new();

    public static event Action<Character>? OnUIMessageChanged;
    public static event Action<Character>? OnUIMessageSent;

    /// <summary>
    /// 确保指定Activity的ChatBot事件已挂接到UI消息列表。
    /// 幂等操作，重复调用安全。
    /// </summary>
    public static void EnsureHooked(ChatActivity activity)
    {
        var list = _botMessages.GetOrCreateValue(activity.Character);
        if (_hookedCharacters.Contains(activity.Character))
            return;
        
        _hookedCharacters.Add(activity.Character);

        activity.ChatBot.ChatSent += (obj) =>
        {
            list.Add(new ChatMessage { Content = obj, IsUser = true });
            list.Add(new ChatMessage { IsUser = false, IsInputting = true });
            OnUIMessageSent?.Invoke(activity.Character);
            OnUIMessageChanged?.Invoke(activity.Character);
        };

        activity.ChatBot.ChatReceived += (obj) =>
        {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.Content += obj;
                OnUIMessageChanged?.Invoke(activity.Character);
            }
        };

        activity.ChatBot.ChatOver += () =>
        {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.IsInputting = false;
                OnUIMessageChanged?.Invoke(activity.Character);
            }
        };
    }

    public static List<ChatMessage> GetMessages(ChatActivity activity)
    {
        EnsureHooked(activity);
        return _botMessages.GetOrCreateValue(activity.Character);
    }

    public static void ClearMessages(ChatActivity activity)
    {
        var list = _botMessages.GetOrCreateValue(activity.Character);
        list.Clear();
    }

    /// <summary>
    /// 批量挂接：扫描系统中所有已激活的Activity，确保全部已挂接。
    /// 用于自启动等场景。
    /// </summary>
    public static void EnsureAllHooked(ChatActivitySystem system)
    {
        foreach (var activity in system.GetAllChatActivities())
            EnsureHooked(activity);
    }
}
