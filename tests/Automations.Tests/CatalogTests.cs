using Automations.Core;
using System.Text.Json.Nodes;
using Xunit;

namespace Automations.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void RepositoryUrlsNormalizeAndRejectOtherHostsAndPathEscapes()
    {
        Assert.Equal("owner/repo", CatalogEntries.NormalizeRepository("https://github.com/owner/repo.git/"));
        Assert.Equal("owner/repo", CatalogEntries.NormalizeRepository("owner/repo"));
        Assert.Throws<AppError>(() => CatalogEntries.NormalizeRepository("https://attacker.invalid/owner/repo"));
        Assert.Throws<AppError>(() => CatalogEntries.NormalizeRepository("owner/.."));
    }
    [Fact]
    public void MatchingReleaseIdentifiesAutomationAndVersionForThisServer()
    {
        var arch = Platform.Arch == "x64" ? "amd64" : "arm64";
        var os = Platform.Os == "darwin" ? "macos" : Platform.Os;
        Assert.Equal(("pending-records", "1.10.0"), CatalogEntries.Identity($"PendingRecords-{os}-{arch}-v1.10.0.zip", "pending-records-v1.10.0"));
        Assert.Null(CatalogEntries.Identity("PendingRecords-unsupported-x64-v1.0.0.zip", "pending-records-v1.0.0"));
    }
    [Fact]
    public void CatalogDistinguishesInstallationUpdateAndAlreadyInstalledWithoutDowngrading()
    {
        var manifest = new JsonObject { ["id"]="pending-records", ["version"]="1.9.0", ["name"]="Verificar registos", ["description"]="Descrição", ["platform"]=new JsonObject {["os"]=Platform.Os,["arch"]=Platform.Arch} };
        var installed = new[] { new Package("key",manifest,"digest") };
        Assert.Equal("update", CatalogEntries.Describe("1","pending-records","1.10.0","Title",1,installed).Action);
        Assert.Equal("installed", CatalogEntries.Describe("1","pending-records","1.9.0","Title",1,installed).Action);
        Assert.Equal("installed", CatalogEntries.Describe("1","pending-records","1.8.0","Title",1,installed).Action);
        Assert.Equal("install", CatalogEntries.Describe("1","another","1.0.0","Title",1,installed).Action);
    }
}
