using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Json.Schema;
using ClosedXML.Excel;

namespace Automations.Core;

public sealed class PackageService(Database database, Storage storage, string schemaFile)
{
    public const long ArchiveLimit = 1536L * 1024 * 1024;
    public const long UploadLimit = 80L * 1024 * 1024;
    private readonly JsonSchema schema = JsonSchema.FromText(File.ReadAllText(schemaFile));
    public static async Task CopyBounded(Stream source, Stream target, long limit, CancellationToken ct)
    {
        var buffer = new byte[65536]; long count = 0; int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0) { count += read; if (count > limit) throw new AppError(413, "O ficheiro é demasiado grande."); await target.WriteAsync(buffer.AsMemory(0, read), ct); }
    }
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> mutations = new();
    public SemaphoreSlim MutationGate(Guid user) => mutations.GetOrAdd(user, _ => new SemaphoreSlim(1, 1));
    public async Task<Package> Install(Guid user, Stream content, string? expectedDigest, CancellationToken ct, string? expectedId = null, string? expectedVersion = null)
    {
        var gate = MutationGate(user); await gate.WaitAsync(ct);
        try { return await InstallArchive(user, content, expectedDigest, ct, expectedId, expectedVersion); }
        finally { gate.Release(); }
    }
    private async Task<Package> InstallArchive(Guid user, Stream content, string? expectedDigest, CancellationToken ct, string? expectedId = null, string? expectedVersion = null)
    {
        var temp = Path.Combine(storage.Workspace(user), ".import-" + Guid.NewGuid()); Directory.CreateDirectory(temp);
        var archiveFile = Path.Combine(temp, "archive.zip"); var extracted = Path.Combine(temp, "contents"); Directory.CreateDirectory(extracted);
        try
        {
            await using (var output = File.Create(archiveFile)) await CopyBounded(content, output, ArchiveLimit, ct);
            string digest; await using (var input = File.OpenRead(archiveFile)) digest = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).ToLowerInvariant();
            if (expectedDigest is not null && digest != expectedDigest.ToLowerInvariant()) throw new AppError(400, "O pacote falhou a verificação SHA-256.");
            using var archive = ZipFile.OpenRead(archiveFile);
            if (archive.Entries.Count > 60000) throw new AppError(400, "O pacote excede os limites de extração.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName.TrimEnd('/'); if (!names.Add(relative)) throw new AppError(400, "O pacote contém caminhos duplicados.");
                var path = Storage.Resolve(extracted, relative);
                var type = (entry.ExternalAttributes >> 16) & 0xf000;
                if (type == 0xa000 || (type != 0 && type != 0x8000 && type != 0x4000)) throw new AppError(400, "O pacote contém links ou ficheiros especiais.");
                if ((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new AppError(400, "O pacote contém links.");
                if (entry.Length > 800L * 1024 * 1024 || (total += entry.Length) > 2560L * 1024 * 1024) throw new AppError(400, "O pacote excede os limites de extração.");
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await using (var output = File.Create(path)) await CopyBounded(entry.Open(), output, Math.Min(entry.Length, 800L * 1024 * 1024), ct);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | (((entry.ExternalAttributes >> 16) & 0x49) != 0 ? UnixFileMode.UserExecute : 0));
            }
            var manifests = Directory.EnumerateFiles(extracted, "automation.json", SearchOption.AllDirectories).Where(file => Path.GetRelativePath(extracted, file).Split(Path.DirectorySeparatorChar).Length <= 2).ToList();
            if (manifests.Count != 1 || new FileInfo(manifests[0]).Length > 256 * 1024) throw new AppError(400, "O pacote deve conter um único automation.json válido.");
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifests[0], ct))!.AsObject();
            if (!schema.Evaluate(manifest).IsValid) throw new AppError(400, "O manifesto do pacote é inválido.");
            ValidateBindings(manifest);
            if (expectedId is not null && (manifest["id"]!.GetValue<string>() != expectedId || manifest["version"]!.GetValue<string>() != expectedVersion)) throw new AppError(400, "A identidade ou versão do pacote não corresponde ao catálogo.");
            if (expectedId is not null && (manifest["platform"]!["os"]!.GetValue<string>() != Platform.Os || manifest["platform"]!["arch"]!.GetValue<string>() != Platform.Arch)) throw new AppError(400, "O pacote não é compatível com o servidor.");
            var packageRoot = Path.GetDirectoryName(manifests[0])!;
            if (archive.Entries.Any(entry => !Path.GetFullPath(Path.Combine(extracted, entry.FullName)).StartsWith(packageRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFullPath(Path.Combine(extracted, entry.FullName)).TrimEnd(Path.DirectorySeparatorChar) != packageRoot)) throw new AppError(400, "Ficheiros fora da pasta do pacote.");
            var executable = Storage.Resolve(packageRoot, manifest["execution"]!["executable"]!.GetValue<string>());
            if (!File.Exists(executable)) throw new AppError(400, "Executável em falta no pacote.");
            foreach (var relative in manifest["execution"]!["packagePaths"]!.AsObject().Select(item => item.Value!.GetValue<string>()).Concat(manifest["execution"]!["workspaceResources"]!.AsArray().Select(item => item!.GetValue<string>())))
            {
                var file = Storage.Resolve(packageRoot, relative); if (!File.Exists(file) && !Directory.Exists(file)) throw new AppError(400, "Recurso em falta no pacote.");
            }
            var package = new Package($"{manifest["id"]!.GetValue<string>()}@{manifest["version"]!.GetValue<string>()}#{digest}", manifest, digest);
            var destination = storage.PackageDirectory(user, package);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (!Directory.Exists(destination)) Directory.Move(packageRoot, destination);
            if (expectedId is null) await database.SavePackage(user, package); else await database.ActivateCatalogPackage(user, package); return package;
        }
        catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException) { throw new AppError(400, "O pacote ZIP ou manifesto é inválido."); }
        finally { Directory.Delete(temp, true); }
    }
    public static void ValidateBindings(JsonObject manifest)
    {
        var ids = new HashSet<string>(); var bindings = new HashSet<string>();
        var execution = manifest["execution"]!;
        if (execution["packagePaths"]!.AsObject().ContainsKey(execution["homeEnvironmentVariable"]!.GetValue<string>())) throw new AppError(400, "O pacote não pode substituir o diretório da execução.");
        var reserved = new HashSet<string>(manifest["execution"]!["packagePaths"]!.AsObject().Select(pair => pair.Key)) { manifest["execution"]!["homeEnvironmentVariable"]!.GetValue<string>() };
        foreach (var input in manifest["inputs"]!.AsArray())
        {
            var type = input!["type"]!.GetValue<string>(); var binding = input["binding"]!.AsObject(); var name = binding.First().Value!.GetValue<string>();
            if (!ids.Add(input["id"]!.GetValue<string>()) || !bindings.Add(name) || reserved.Contains(name)) throw new AppError(400, "Campos ou bindings duplicados/reservados.");
            if (binding.ContainsKey("argument") && (name == "--dry-run" || name == execution["stateDirectoryArgument"]?.GetValue<string>())) throw new AppError(400, "Argumento reservado para a execução.");
            if (type == "file" && input.AsObject().ContainsKey("default")) throw new AppError(400, "Ficheiros exigem seleção explícita.");
            if (type != "file" && (input.AsObject().ContainsKey("columns") || input.AsObject().ContainsKey("extensions"))) throw new AppError(400, "Metadados de ficheiro num campo inválido.");
            if (type != "integer" && (input.AsObject().ContainsKey("minimum") || input.AsObject().ContainsKey("maximum"))) throw new AppError(400, "Limites numéricos num campo inválido.");
            if (input["columns"] is JsonArray columns && columns.Count > 0 && (input["extensions"] is not JsonArray extensions || extensions.Count != 1 || extensions[0]!.GetValue<string>() != ".xlsx")) throw new AppError(400, "Validação de colunas exige um campo Excel.");
            if (input["minimum"] is not null && input["maximum"] is not null && input["minimum"]!.GetValue<long>() > input["maximum"]!.GetValue<long>()) throw new AppError(400, "Limites numéricos inválidos.");
            if (input["default"] is JsonValue defaultValue)
            {
                if (type == "integer")
                {
                    if (!defaultValue.TryGetValue<long>(out var number) || (input["minimum"] is not null && number < input["minimum"]!.GetValue<long>()) || (input["maximum"] is not null && number > input["maximum"]!.GetValue<long>())) throw new AppError(400, "Valor predefinido inválido.");
                }
                else if (!defaultValue.TryGetValue<string>(out _)) throw new AppError(400, "Valor predefinido inválido.");
            }
            if (type == "secret" && (binding.ContainsKey("argument") || input.AsObject().ContainsKey("default"))) throw new AppError(400, "Segredos não podem ter argumentos ou valores predefinidos.");
        }
    }
    public async Task<Upload> Upload(Guid user, Stream content, string name, CancellationToken ct)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(extension, "^\\.[a-z0-9]{1,16}$")) throw new AppError(400, "Selecione um ficheiro com uma extensão válida.");
        var id = Guid.NewGuid(); var upload = new Upload(id, name[..Math.Min(name.Length, 160)], id + extension);
        var directory = Path.Combine(storage.Workspace(user), "uploads"); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, upload.Filename);
        try { await using (var output = Storage.PrivateWrite(file)) await CopyBounded(content, output, UploadLimit, ct); await database.SaveUpload(user, upload); return upload; }
        catch { File.Delete(file); throw; }
    }
    public static byte[] Template(JsonNode input)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add("Dados"); var columns = input["columns"]!.AsArray();
        for (var index = 0; index < columns.Count; index++) { sheet.Cell(1, index + 1).Value = columns[index]!.GetValue<string>(); sheet.Column(index + 1).Width = 26; sheet.Column(index + 1).Style.NumberFormat.Format = "@"; }
        sheet.Row(1).Style.Font.Bold = true; using var output = new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }
    public static void ValidateWorkbook(string file, IEnumerable<string> required)
    {
        try
        {
        using var book = new XLWorkbook(file); var sheet = book.Worksheets.FirstOrDefault() ?? throw new AppError(400, "O Excel não contém folhas.");
        var headers = sheet.Row(1).CellsUsed().Select(cell => cell.GetString().Trim()).ToHashSet(); var missing = required.Where(column => !headers.Contains(column)).ToArray();
        if (missing.Length > 0) throw new AppError(400, "Colunas em falta na primeira folha: " + string.Join(", ", missing));
        if ((sheet.LastRowUsed()?.RowNumber() ?? 0) <= 1) throw new AppError(400, "O Excel não contém linhas de dados.");
        }
        catch (Exception error) when (error is not AppError) { throw new AppError(400, "Não foi possível ler o Excel. Confirme que é um ficheiro .xlsx válido."); }
    }
}
