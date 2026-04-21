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
    static readonly ConcurrentDictionary<string, List<ChatMessage>> BotMessages = new();
    static readonly HashSet<string> HookedActivityNames = new();

    public static event Action<string>? OnUIMessageChanged;
    public static event Action<string>? OnUIMessageSent;

    /// <summary>
    /// 全局初始化：绑定活动系统的创建事件，确保所有活动一诞生就被UI录制器“粘”上。
    /// </summary>
    public static void Initialize(ChatActivitySystem system)
    {
        system.Created += OnActivityCreated;
        system.Destroyed += OnActivityDestroyed;
    }
    public static List<ChatMessage> GetMessagesByName(string name)
    {
        return BotMessages.GetOrAdd(name, _ => new List<ChatMessage>());
    }

    public static void ClearMessages(ChatActivity activity)
    {
        if (BotMessages.TryGetValue(activity.Character.Name, out var list))
        {
            list.Clear();
        }
    }


    /// <summary>
    /// 确保指定Activity的ChatBot事件已挂接到UI消息列表。
    /// 幂等操作，重复调用安全。
    /// </summary>
    static void OnActivityCreated(ChatActivity activity)
    {
        string name = activity.Character.Name;
        var list = BotMessages.GetOrAdd(name, _ => new List<ChatMessage>());

        if (HookedActivityNames.Contains(name))
            return;

        HookedActivityNames.Add(name);

        activity.ChatBot.ChatSent += (obj) => {
            list.Add(new ChatMessage { Content = obj, IsUser = true });
            list.Add(new ChatMessage { IsUser = false, IsInputting = true });
            OnUIMessageSent?.Invoke(name);
            OnUIMessageChanged?.Invoke(name);
        };

        activity.ChatBot.ChatReceived += (obj) => {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.Content += obj;
                OnUIMessageChanged?.Invoke(name);
            }
        };

        activity.ChatBot.ChatOver += () => {
            var aiMessage = list.LastOrDefault(m => !m.IsUser && m.IsInputting);
            if (aiMessage != null)
            {
                aiMessage.IsInputting = false;
                OnUIMessageChanged?.Invoke(name);
            }
        };
        
        Actvi
    }
    static void OnActivityDestroyed(ChatActivity activity)
    {
        
    }
}
