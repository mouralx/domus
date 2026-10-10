using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Automations.Core;

public sealed record CatalogEntry(string AssetId, string AutomationId, string Name, string Version, string Description, long Size, string? InstalledVersion, string Action);
public static class CatalogEntries
{
    public static (string Id, string Version)? Identity(string filename, string tag)
    {
        var match = Regex.Match(filename, @"^(?<name>.+)-(?<os>linux|win|win32|macos|darwin)-(?<arch>amd64|x64|arm64)-v(?<version>\d+\.\d+\.\d+)\.zip$", RegexOptions.IgnoreCase);
        if (!match.Success || !System.Version.TryParse(match.Groups["version"].Value, out _)) return null;
        var os = match.Groups["os"].Value.ToLowerInvariant() switch { "win" => "win32", "macos" => "darwin", var value => value };
        var arch = match.Groups["arch"].Value.ToLowerInvariant() == "amd64" ? "x64" : match.Groups["arch"].Value.ToLowerInvariant();
        if (os != Platform.Os || arch != Platform.Arch) return null;
        var tagged = Regex.Match(tag, @"^(?<id>[a-z][a-z0-9-]*)-v(?<version>\d+\.\d+\.\d+)$");
        var id = tagged.Success && tagged.Groups["version"].Value == match.Groups["version"].Value ? tagged.Groups["id"].Value : Regex.Replace(Regex.Replace(match.Groups["name"].Value, "([A-Z]+)([A-Z][a-z])", "$1-$2"), "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
        return Regex.IsMatch(id, "^[a-z][a-z0-9-]*$") ? (id, match.Groups["version"].Value) : null;
    }
    public static CatalogEntry Describe(string assetId, string id, string version, string title, long size, IReadOnlyList<Package> installed)
    {
        var current = installed.Where(pkg => pkg.Compatible && pkg.Manifest["id"]!.GetValue<string>() == id).OrderByDescending(pkg => System.Version.Parse(pkg.Manifest["version"]!.GetValue<string>())).FirstOrDefault();
        var installedVersion = current?.Manifest["version"]!.GetValue<string>();
        var action = installedVersion is null ? "install" : System.Version.Parse(version) > System.Version.Parse(installedVersion) ? "update" : "installed";
        return new(assetId, id, current?.Manifest["name"]!.GetValue<string>() ?? title, version, current?.Manifest["description"]!.GetValue<string>() ?? "Automação disponível para instalar.", size, installedVersion, action);
    }
    public static string NormalizeRepository(string repository)
    {
        repository = repository.Trim().TrimEnd('/');
        if (repository.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(repository, UriKind.Absolute, out var url) || url.Host != "github.com" || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0) throw new AppError(400, "Use autor/repositorio ou o URL do repositório GitHub.");
            repository = url.AbsolutePath.Trim('/');
        }
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repository = repository[..^4];
        if (!Regex.IsMatch(repository, "^[A-Za-z0-9][A-Za-z0-9-]{0,99}/[A-Za-z0-9_.-]{1,100}$") || repository.Split('/')[1] is "." or "..") throw new AppError(400, "Use o formato autor/repositorio.");
        return repository;
    }
}
