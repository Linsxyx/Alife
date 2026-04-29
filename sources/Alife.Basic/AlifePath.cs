using System.IO;

namespace Alife.Basic;

public static class AlifePath
{
    public static string StorageFolderPath { get; private set; }
    public static string OutputsFolderPath { get; }
    public static string TempFolderPath { get; }
    public static void SetStorageFolderPath(string path)
    {
        string oldPath = StorageFolderPath;
        string newPath = path;

        // 如果路径没变，直接返回
        if (string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            // 场景：目标地址不存在，且旧地址存在 -> 执行搬家
            if (!Directory.Exists(newPath) && Directory.Exists(oldPath))
            {
                // 确保父目录存在
                string? parent = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);

                Directory.Move(oldPath, newPath);
                AlifeTerminal.LogHint($"检测到存储位置变更，数据已从 {oldPath} 迁移至 {newPath}");
            }
            else if (!Directory.Exists(newPath))
            {
                // 目标地址不存在且旧地址也不存在，直接创建
                Directory.CreateDirectory(newPath);
            }
        }
        catch (Exception ex)
        {
            AlifeTerminal.LogError($"存储位置迁移失败: {ex.Message}");
            // 如果迁移失败，至少保证新目录存在，避免后续业务崩溃
            if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);
        }

        // 更新配置
        StorageFolderPath = newPath;
        File.WriteAllText(Path.Combine(OutputsFolderPath, "storage_path.txt"), newPath);
    }

    static AlifePath()
    {
        OutputsFolderPath = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!;

        string configPath = Path.Combine(OutputsFolderPath, "storage_path.txt");
        StorageFolderPath = File.Exists(configPath)
            ? File.ReadAllText(configPath).Trim()
            : Path.Combine(Path.GetDirectoryName(OutputsFolderPath)!, "Storage");
        Directory.CreateDirectory(StorageFolderPath);

        TempFolderPath = Path.Combine(Path.GetTempPath(), "Alife");
        Directory.CreateDirectory(TempFolderPath);
    }
}
