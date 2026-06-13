using System.Text.Json;
using Alife.Platform;

namespace Alife.PluginMarket;

public class NuGetEnvironmentInstaller(string packageListFile) : IEnvironmentInstaller
{
    public void InstallEnvironment(IEnumerable<KeyValuePair<string, string>> environment)
    {
        VersionResolver resolver = new();
        resolver.AddRange(environment);

        string tempDir = Path.Combine(Path.GetTempPath(), $"nuget_{Guid.NewGuid():N}");
        try
        {
            RestorePackages(resolver, tempDir);
            GeneratePackageList(packageListFile, tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    static void RestorePackages(VersionResolver resolver, string tempDir)
    {
        Directory.CreateDirectory(tempDir);

        string refs = string.Join("\n",
            resolver.GetAllRanges().Select(dep =>
            {
                string spec = FormatVersionSpec(dep.Min, dep.Max);
                return $"    <PackageReference Include=\"{dep.Name}\" Version=\"{spec}\" />";
            }));

        string csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
            {refs}
              </ItemGroup>
            </Project>
            """;

        File.WriteAllText(Path.Combine(tempDir, "temp.csproj"), csproj);
        AlifePlatform.Command("dotnet", $"restore {tempDir}/temp.csproj");
    }

    static string FormatVersionSpec(string min, string max)
    {
        bool hasMin = min != "0.0.0";
        bool hasMax = max != "99999.0.0";

        if (hasMin && hasMax && min == max)
            return min;
        if (hasMin && hasMax)
            return $"[{min},{max}]";
        if (hasMin)
            return $"[{min},)";
        if (hasMax)
            return $"(,{max}]";
        return "*";
    }

    static void GeneratePackageList(string packageListFile, string tempDir)
    {
        string assetsFile = Path.Combine(tempDir, "obj", "project.assets.json");
        if (!File.Exists(assetsFile))
        {
            File.WriteAllText(packageListFile, "");
            return;
        }

        string json = File.ReadAllText(assetsFile);
        using var doc = JsonDocument.Parse(json);

        string nugetCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        string rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

        HashSet<string> managedDirs = new();
        HashSet<string> nativeDirs = new();

        if (doc.RootElement.TryGetProperty("targets", out var targets))
        {
            string tfm = targets.EnumerateObject().First().Name;

            if (targets.TryGetProperty(tfm, out var tfmTarget))
            {
                foreach (var pkg in tfmTarget.EnumerateObject())
                {
                    string packageRoot = pkg.Name.Split('/')[0];
                    string packageVersion = pkg.Name.Split('/')[1];
                    string pkgDir = Path.Combine(nugetCache, packageRoot, packageVersion);

                    if (pkg.Value.TryGetProperty("compile", out var compile))
                    {
                        foreach (var dll in compile.EnumerateObject())
                        {
                            string dllPath = Path.Combine(pkgDir, dll.Name);
                            string? dir = Path.GetDirectoryName(dllPath);
                            if (dir != null && Directory.Exists(dir))
                                managedDirs.Add(dir);
                        }
                    }

                    string nativeDir = Path.Combine(pkgDir, "runtimes", rid, "native");
                    if (Directory.Exists(nativeDir))
                        nativeDirs.Add(nativeDir);
                }
            }
        }

        List<string> lines = new();
        foreach (string dir in managedDirs)
            lines.Add($"managed:{dir}");
        foreach (string dir in nativeDirs)
            lines.Add($"unmanaged:{dir}");
        File.WriteAllLines(packageListFile, lines);
    }

    public (string[] managed, string[] unmanaged) ReadPackageList()
    {
        if (!File.Exists(packageListFile))
            return ([], []);

        List<string> managed = new();
        List<string> unmanaged = new();

        foreach (string line in File.ReadAllLines(packageListFile))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("managed:"))
            {
                string path = line["managed:".Length..];
                if (Directory.Exists(path))
                    managed.Add(path);
            }
            else if (line.StartsWith("unmanaged:"))
            {
                string path = line["unmanaged:".Length..];
                if (Directory.Exists(path))
                    unmanaged.Add(path);
            }
        }

        return (managed.ToArray(), unmanaged.ToArray());
    }
}
