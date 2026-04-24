namespace Alife.Framework;

public class ChatActivitySystem
{
    public event Action<ChatActivity>? Created;
    public event Action<ChatActivity>? Destroyed;

    public IEnumerable<ChatActivity> GetAllChatActivities()
    {
        return activities.Values;
    }

    public bool IsActivated(Character character)
    {
        return activities.ContainsKey(character.Name);
    }

    public async Task<ChatActivity> Play(Character character, IProgress<(string, float)>? progress = null)
    {
        ChatActivity chatActivity = await ChatActivity.Create(character, configuration, pluginSystem, progress, [
            configuration,
            storageSystem,
            pluginSystem
        ]);

        activities.Add(character.Name, chatActivity);
        Created?.Invoke(chatActivity);
        await chatActivity.Start();

        return chatActivity;
    }

    public async Task Stop(Character character)
    {
        ChatActivity chatActivity = activities[character.Name];
        await chatActivity.DisposeAsync();
        activities.Remove(character.Name);
        Destroyed?.Invoke(chatActivity);
    }

    public ChatActivitySystem(ConfigurationSystem configuration, StorageSystem storageSystem, PluginSystem pluginSystem)
    {
        this.configuration = configuration;
        this.storageSystem = storageSystem;
        this.pluginSystem = pluginSystem;
    }

    readonly ConfigurationSystem configuration;
    readonly StorageSystem storageSystem;
    readonly PluginSystem pluginSystem;
    readonly Dictionary<string, ChatActivity> activities = new();
}
