using System.Text;
using System.IO.Compression;

namespace Automations.Core;

public record ResultFile(string Id, string Name, long Size);
public sealed class ExecutionFiles(Storage storage)
{
    public List<ResultFile> List(Guid user, RunRecord run) => Enumerate(user, run).Take(10000).ToList();
    public IEnumerable<ResultFile> Enumerate(Guid user, RunRecord run)
    {
        var root = Path.Combine(storage.RunDirectory(user, run.Id), "workspace");
        if (!Directory.Exists(root)) yield break;
        IEnumerable<ResultFile> Visit(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(entry); var relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/');
                if (name.StartsWith('.') || name is "node_modules" or "browsers" or "data" or "build" or "dist" || run.ResourceRoots.Any(resource => relative == resource.TrimEnd('/') || relative.StartsWith(resource.TrimEnd('/') + '/', StringComparison.Ordinal))) continue;
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                if (Directory.Exists(entry)) { foreach (var file in Visit(entry)) yield return file; }
                else yield return new(Convert.ToBase64String(Encoding.UTF8.GetBytes(relative)).TrimEnd('=').Replace('+', '-').Replace('/', '_'), relative, new FileInfo(entry).Length);
            }
        }
        foreach (var file in Visit(root)) yield return file;
    }
    public async Task<string> Archive(Guid user, RunRecord run, CancellationToken ct)
    {
        var results = List(user, run);
        if (results.Count == 0) throw new AppError(404, "Esta execução não tem ficheiros de resultado.");
        var file = Path.Combine(storage.RunDirectory(user, run.Id), ".results-" + Guid.NewGuid() + ".zip");
        try
        {
            await using var output = Storage.PrivateWrite(file);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
            var root = Path.Combine(storage.RunDirectory(user, run.Id), "workspace");
            foreach (var result in results)
            {
                ct.ThrowIfCancellationRequested();
                var source = Storage.Resolve(root, result.Name);
                var entry = archive.CreateEntry(result.Name, CompressionLevel.Fastest);
                await using var input = File.OpenRead(source);
                await using var target = entry.Open();
                await input.CopyToAsync(target, ct);
            }
            return file;
        }
        catch { File.Delete(file); throw; }
    }
    public string Resolve(Guid user, RunRecord run, string id)
    {
        var item = Enumerate(user, run).FirstOrDefault(file => file.Id == id) ?? throw new AppError(404, "Resultado não encontrado.");
        return Storage.Resolve(Path.Combine(storage.RunDirectory(user, run.Id), "workspace"), item.Name);
    }
}
