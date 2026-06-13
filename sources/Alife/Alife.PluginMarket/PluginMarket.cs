using Alife.Platform;
using Newtonsoft.Json;

namespace Alife.PluginMarket;

public interface IPluginProvider
{
    /// <summary>
    /// 获取托管的所有插件信息
    /// </summary>
    /// <returns></returns>
    public Task<Plugin[]> GetPluginsAsync();
}

public interface IPluginResolver
{
    /// <summary>
    /// 以某种途径解析出当前存在的插件id和version
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> GetPlugins();
}

public interface IEnvironmentInstaller
{
    /// <summary>
    /// 安装依赖环境清单
    /// key：包名。可能重复。
    /// value：版本要求.可能冲突。允许如下几种形式：
    ///     >=x.x.x : 最小版本
    ///     &lt;=x.x.x : 最大版本
    ///     ==x.x.x : 精确版本（同时限制上下限）
    ///     空字符串 : 等于 >=0.0.0（无限制）
    /// </summary>
    /// <param name="environment"></param>
    public void InstallEnvironment(IEnumerable<KeyValuePair<string, string>> environment);
}

public interface IPluginInstaller
{
    /// <summary>
    /// 不需要考虑环境依赖，仅重新安装插件本体
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="version"></param>
    public Task InstallPlugin(Plugin plugin, string version);
    public Task UninstallPlugin(Plugin plugin);
}

public class PluginMarket
{
    public event Action? OnInstalled;

    public IEnumerable<Plugin> GetAllPlugins()
    {
        return allPlugins.Values;
    }
    public Plugin[] GetHadPlugins()
    {
        return hadPlugins.Select(pair => allPlugins.GetValueOrDefault(pair.Key))
            .Where(plugin => plugin != null).Cast<Plugin>().ToArray();
    }

    public Dictionary<string, string> GetInstalledPlugins() => localPlugins.GetPlugins();

    public bool IsInstalled(string pluginId) => hadPlugins.ContainsKey(pluginId);

    public string? GetInstalledVersion(string pluginId) => hadPlugins.GetValueOrDefault(pluginId);

    public bool HasUpdate(Plugin plugin)
    {
        string? installedVersion = GetInstalledVersion(plugin.Id);
        if (installedVersion == null || plugin.Releases == null)
            return false;

        string? latestVersion = plugin.Releases.Keys
            .OrderByDescending(v => v)
            .FirstOrDefault();

        return latestVersion != null && latestVersion != installedVersion;
    }

    public string? GetLatestVersion(Plugin plugin)
    {
        return plugin.Releases?.Keys
            .OrderByDescending(v => v)
            .FirstOrDefault();
    }

    public async Task InstallPlugin(Plugin plugin, string version)
    {
        if (plugin.Releases == null)
            throw new Exception($"Plugin {plugin.Id} is not released");
        if (plugin.Releases.ContainsKey(version) == false)
            throw new Exception($"Plugin {plugin.Id} version {version} not released");

        var dependencies = plugin.GetDependencies(version);
        if (dependencies != null)
        {
            VersionResolver versionResolver = new();
            versionResolver.AddRange(dependencies);

            foreach (KeyValuePair<string, string> pair in dependencies)
            {
                string? hadVersion = hadPlugins.GetValueOrDefault(pair.Key);
                if (hadVersion != null && versionResolver.IsSatisfied(pair.Key, hadVersion))
                    continue;

                Plugin? dependentPlugin = allPlugins.GetValueOrDefault(pair.Key);
                if (dependentPlugin == null)
                    throw new Exception($"Plugin {plugin.Id} dependent unknown plugin type");
                IEnumerable<string>? versionList = dependentPlugin.Releases?.Keys;
                if (versionList == null)
                    throw new Exception($"Plugin {plugin.Id} dependent plugin is not released");

                string satisfiedVersion = versionResolver.Resolve(dependentPlugin.Id, versionList);
                await InstallPlugin(dependentPlugin, satisfiedVersion);
            }
        }

        var environments = plugin.GetEnvironments(version);
        if (environments != null)
        {
            List<KeyValuePair<string, string>> environmentManifest = new();
            foreach ((string type, Dictionary<string, string> environment) in environments)
            {
                IEnvironmentInstaller? environmentInstaller = environmentInstallers.GetValueOrDefault(type);
                if (environmentInstaller == null)
                    throw new Exception($"Plugin {plugin.Id} has no supported environment type");

                GetEnvironment(type, environmentManifest);
                environmentManifest.AddRange(environment);
                environmentInstaller.InstallEnvironment(environmentManifest);
            }
        }

        await pluginInstaller.InstallPlugin(plugin, version);
        OnInstalled?.Invoke();
    }

    public List<string> GetDependents(string pluginId)
    {
        List<string> dependents = new();
        foreach ((string id, string version) in hadPlugins)
        {
            if (id == pluginId)
                continue;

            Plugin? plugin = allPlugins.GetValueOrDefault(id);
            if (plugin == null)
                continue;

            Dictionary<string, string>? dependencies = plugin.GetDependencies(version);
            if (dependencies != null && dependencies.ContainsKey(pluginId))
                dependents.Add(id);
        }
        return dependents;
    }

    public async Task UninstallPlugin(Plugin plugin)
    {
        List<string> dependents = GetDependents(plugin.Id);
        if (dependents.Count > 0)
            throw new Exception($"无法卸载，以下插件依赖 {plugin.Id}: {string.Join(", ", dependents)}");

        await pluginInstaller.UninstallPlugin(plugin);
        OnInstalled?.Invoke();
    }

    /// <summary>
    /// 刷新本地已安装插件列表
    /// </summary>
    public void RefreshLocalPlugins()
    {
        hadPlugins = localPlugins.GetPlugins();
    }

    public void GetEnvironment(string type, List<KeyValuePair<string, string>> environments)
    {
        environments.Clear();
        foreach ((string pluginID, string version) in hadPlugins)
        {
            Plugin? plugin = allPlugins.GetValueOrDefault(pluginID);
            if (plugin == null)
                continue;
            Dictionary<string, string>? environment = plugin.GetEnvironments(version)?.GetValueOrDefault(type);
            if (environment == null)
                continue;

            environments.AddRange(environment);
        }
    }

    readonly IPluginProvider onlinePlugins;
    readonly IPluginResolver localPlugins;
    readonly IPluginInstaller pluginInstaller;
    readonly Dictionary<string, IEnvironmentInstaller> environmentInstallers;
    readonly string cachePath;
    Dictionary<string, Plugin> allPlugins;
    Dictionary<string, string> hadPlugins;

    public PluginMarket(IPluginProvider onlinePlugins, IPluginResolver localPlugins, IPluginInstaller pluginInstaller, Dictionary<string, IEnvironmentInstaller> environmentInstallers)
    {
        this.onlinePlugins = onlinePlugins;
        this.localPlugins = localPlugins;
        this.pluginInstaller = pluginInstaller;
        this.environmentInstallers = environmentInstallers;

        cachePath = Path.Combine(AlifePath.RuntimeFolderPath, "plugin_market_cache.json");
        allPlugins = new Dictionary<string, Plugin>();
        hadPlugins = [];

        LoadCache();
        RefreshLocalPlugins();
    }

    /// <summary>
    /// 从云端拉取插件列表，并写入缓存
    /// </summary>
    public async Task FetchOnlinePluginsAsync()
    {
        allPlugins = (await onlinePlugins.GetPluginsAsync()).ToDictionary(plugin => plugin.Id, plugin => plugin);
        SaveCache();
    }

    void SaveCache()
    {
        try
        {
            if (allPlugins.Count == 0) return;
            string json = JsonConvert.SerializeObject(allPlugins.Values.ToArray(), Formatting.Indented);
            File.WriteAllText(cachePath, json);
        }
        catch
        {
            // ignore cache write errors
        }
    }

    void LoadCache()
    {
        try
        {
            if (!File.Exists(cachePath))
                return;
            string json = File.ReadAllText(cachePath);
            var plugins = JsonConvert.DeserializeObject<Plugin[]>(json);
            if (plugins == null || plugins.Length == 0)
                return;
            allPlugins = plugins.ToDictionary(p => p.Id, p => p);
            return;
        }
        catch
        {
            return;
        }
    }
}
