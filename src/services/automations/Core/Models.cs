using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;

namespace Automations.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}
public sealed class AppError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public record Account(Guid Id, string Username, string PasswordHash);
public record Package(string Key, JsonObject Manifest, string Digest)
{
    public bool Compatible => Manifest["platform"]!["os"]!.GetValue<string>() == Platform.Os && Manifest["platform"]!["arch"]!.GetValue<string>() == Platform.Arch;
}
public record Upload(Guid Id, string Name, string Filename);
public record Execution(Guid Id, Guid UserId, string Status, RunRecord Record, bool CancelRequested);
public sealed class RunRecord
{
    public Guid Id { get; set; }
    public Guid? InitiatedBy { get; set; }
    public string PackageKey { get; set; } = "";
    public string PackageId => PackageKey;
    public string Name { get; set; } = "";
    public string Status { get; set; } = "queued";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public int? ExitCode { get; set; }
    public string Message { get; set; } = "";
    public List<string> Logs { get; set; } = [];
    public List<string> ResourceRoots { get; set; } = [];
}
public static class Platform
{
    public static string Os => OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux";
    public static string Arch => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
}
public sealed class Storage(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public string Workspace(Guid userId) => Path.Combine(Root, "workspaces", userId.ToString());
    public string PackageDirectory(Guid userId, Package package) => Path.Combine(Workspace(userId), "packages", package.Manifest["id"]!.GetValue<string>(), package.Manifest["version"]!.GetValue<string>(), package.Digest);
    public string RunDirectory(Guid userId, Guid id) => Path.Combine(Workspace(userId), "runs", id.ToString());
    public string JobFile(Guid userId, Guid id) => Path.Combine(Workspace(userId), "jobs", id + ".json");
    public static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.Any(char.IsControl) || Path.IsPathRooted(relative) || relative.Split('/').Any(part => part is "." or "..")) throw new AppError(400, "Caminho inválido no pacote.");
        var result = Path.GetFullPath(Path.Combine(root, relative));
        if (!result.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new AppError(400, "Caminho fora do espaço de trabalho.");
        return result;
    }
    public static async Task PrivateFile(string file, string content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await using var stream = PrivateWrite(file);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), ct);
    }
    public static FileStream PrivateWrite(string file)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(file, options);
    }
    public static bool Terminal(string status) => status is "completed" or "failed" or "cancelled";
}

public sealed record RunMetrics(long Running, long Completed);
