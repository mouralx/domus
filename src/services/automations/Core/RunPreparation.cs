using System.Text.Json.Nodes;

namespace Automations.Core;

public static class RunPreparation
{
    public static async Task Validate(Database database, Storage storage, Guid user, Package package, JsonObject values)
    {
        var inputs = package.Manifest["inputs"]!.AsArray(); var ids = inputs.Select(input => input!["id"]!.GetValue<string>()).ToHashSet();
        if (values.Any(value => !ids.Contains(value.Key))) throw new AppError(400, "Campo desconhecido na execução.");
        foreach (var input in inputs)
        {
            var id = input!["id"]!.GetValue<string>(); var value = values[id] ?? input["default"];
            if (value is null || value.ToJsonString() == "\"\"") { if (input["required"]!.GetValue<bool>()) throw new AppError(400, "Preencha o campo: " + input["label"]!.GetValue<string>()); continue; }
            var type = input["type"]!.GetValue<string>();
            if (type == "integer")
            {
                if (value is not JsonValue numeric || !numeric.TryGetValue<long>(out var number) || (input["minimum"] is not null && number < input["minimum"]!.GetValue<long>()) || (input["maximum"] is not null && number > input["maximum"]!.GetValue<long>())) throw new AppError(400, "Número inválido: " + id);
                continue;
            }
            if (value is not JsonValue textNode || !textNode.TryGetValue<string>(out var text) || text.Contains('\0')) throw new AppError(400, "Texto inválido: " + id);
            if (type != "file") continue;
            if (!Guid.TryParse(text, out var uploadId)) throw new AppError(400, "Selecione um ficheiro: " + id);
            var upload = await database.Upload(user, uploadId) ?? throw new AppError(400, "Ficheiro não encontrado: " + id);
            var file = Storage.Resolve(Path.Combine(storage.Workspace(user), "uploads"), upload.Filename);
            if (!File.Exists(file)) throw new AppError(400, "Ficheiro de entrada em falta.");
            var extension = Path.GetExtension(upload.Name).ToLowerInvariant();
            if (input["extensions"] is JsonArray extensions && !extensions.Any(item => item!.GetValue<string>() == extension)) throw new AppError(400, "Extensão não suportada: " + id);
            if (input["columns"] is JsonArray columns && columns.Count > 0 && extension != ".xlsx") throw new AppError(400, "Este campo exige um Excel .xlsx.");
            if (extension == ".xlsx") PackageService.ValidateWorkbook(file, (input["columns"] as JsonArray ?? []).Select(column => column!.GetValue<string>()));
        }
    }
    public static void CopyResource(string source, string target)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new AppError(400, "O recurso contém links.");
        if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target, false); }
        else { Directory.CreateDirectory(target); foreach (var entry in Directory.EnumerateFileSystemEntries(source)) CopyResource(entry, Path.Combine(target, Path.GetFileName(entry))); }
    }
}
