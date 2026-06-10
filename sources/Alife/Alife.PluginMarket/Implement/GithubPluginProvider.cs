using Newtonsoft.Json;

namespace Alife.PluginMarket;

public class GithubPluginProvider(string owner, string repo, string branch = "main") : IPluginProvider
{
    readonly HttpClient httpClient = new();
    readonly string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents?ref={branch}";

    public Plugin[] GetPlugins()
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Alife/1.0");
        string json = httpClient.GetStringAsync(apiUrl).Result;
        var items = JsonConvert.DeserializeObject<List<GithubContentItem>>(json) ?? new();

        List<Plugin> plugins = new();
        foreach (var item in items)
        {
            if (item.Type != "file" || item.Name == null || !item.Name.EndsWith(".json") || item.DownloadUrl == null)
                continue;

            try
            {
                string pluginJson = httpClient.GetStringAsync(item.DownloadUrl).Result;
                Plugin? plugin = JsonConvert.DeserializeObject<Plugin>(pluginJson);
                if (plugin != null && !string.IsNullOrEmpty(plugin.Id))
                    plugins.Add(plugin);
            }
            catch
            {
                // 忽略解析失败的文件
            }
        }

        return plugins.ToArray();
    }

    class GithubContentItem
    {
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("type")]
        public string? Type { get; set; }
        [JsonProperty("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
