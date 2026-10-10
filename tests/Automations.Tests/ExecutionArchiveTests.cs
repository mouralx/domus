using Automations.Core;
using System.IO.Compression;
using Xunit;

namespace Automations.Tests;

public sealed class ExecutionArchiveTests
{
    [Fact]
    public async Task ZipPreservesResultFoldersAndExcludesSecretsAndPackagedResources()
    {
        var directory = Directory.CreateTempSubdirectory("robolex-zip-test-");
        try
        {
            var storage = new Storage(directory.FullName); var user = Guid.NewGuid();
            var run = new RunRecord { Id = Guid.NewGuid(), Status = "failed", ResourceRoots = ["runtime"] };
            var root = Path.Combine(storage.RunDirectory(user, run.Id), "workspace");
            Directory.CreateDirectory(Path.Combine(root, "downloads")); Directory.CreateDirectory(Path.Combine(root, "runtime"));
            await File.WriteAllTextAsync(Path.Combine(root, "downloads", "documento.pdf"), "PDF result");
            await File.WriteAllTextAsync(Path.Combine(root, "workbook.xlsx"), "Updated workbook");
            await File.WriteAllTextAsync(Path.Combine(root, ".env"), "PRIVATE_TOKEN");
            await File.WriteAllTextAsync(Path.Combine(root, "runtime", "executable"), "Package resource");
            var file = await new ExecutionFiles(storage).Archive(user, run, CancellationToken.None);
            using var zip = ZipFile.OpenRead(file);
            Assert.Equal(new[] { "downloads/documento.pdf", "workbook.xlsx" }, zip.Entries.Select(entry => entry.FullName).Order().ToArray());
            using var reader = new StreamReader(zip.GetEntry("downloads/documento.pdf")!.Open());
            Assert.Equal("PDF result", await reader.ReadToEndAsync());
        }
        finally { directory.Delete(true); }
    }
}
