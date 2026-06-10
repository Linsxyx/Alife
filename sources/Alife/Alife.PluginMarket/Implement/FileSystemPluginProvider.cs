using Newtonsoft.Json;

namespace Alife.PluginMarket;

public class FileSystemPluginProvider(string directoryPath) : IPluginProvider
{
    public Plugin[] GetPlugins()
    {
        if (!Directory.Exists(directoryPath))
            return [];

        string[] files = Directory.GetFiles(directoryPath, "*.json");
        List<Plugin> plugins = new();

        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                Plugin? plugin = JsonConvert.DeserializeObject<Plugin>(json);
                if (plugin != null)
                    plugins.Add(plugin);
            }
            catch
            {
                // 忽略解析失败的文件
            }
        }

        return plugins.ToArray();
    }
}
