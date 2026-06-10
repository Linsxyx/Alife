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

        List<string> packagePaths = new();
        if (doc.RootElement.TryGetProperty("libraries", out var libraries))
        {
            foreach (var lib in libraries.EnumerateObject())
            {
                if (lib.Value.TryGetProperty("path", out var path))
                {
                    string packagePath = Path.Combine(nugetCache, path.GetString()!);
                    if (Directory.Exists(packagePath))
                        packagePaths.Add(packagePath);
                }
            }
        }

        File.WriteAllLines(packageListFile, packagePaths);
    }

    public static string[] ReadPackageList(string packageListFile)
    {
        if (!File.Exists(packageListFile))
            return [];

        return File.ReadAllLines(packageListFile)
            .Where(line => !string.IsNullOrWhiteSpace(line) && Directory.Exists(line))
            .ToArray();
    }
}
