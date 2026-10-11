using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

public sealed class AssistantWorkspaceTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture fixture;
    public AssistantWorkspaceTests(ApiFixture fixture) => this.fixture = fixture;
    [Fact]
    public async Task WorkspaceToolsPageMetadataAndRejectStaleChangesAndForeignWorkspaces()
    {
        using var client = await fixture.AuthenticatedClient();
        var workspace = (await client.GetFromJsonAsync<JsonArray>("/api/workspaces"))![0]!["id"]!.GetValue<string>();
        workspace = (await client.GetFromJsonAsync<JsonObject>($"/api/domus/workspaces/{workspace}/context"))!["workspaceId"]!.GetValue<string>();
        var path = $"/api/domus/workspaces/{workspace}";
        var page = (await client.GetFromJsonAsync<JsonObject>(path + "/data?collection=projects&offset=0&limit=1"))!;
        Assert.Single(page["items"]!.AsArray());
        Assert.Null(page["tasks"]);
        var create = new { collection = "projects", operation = "create", id = (string?)null, expectedVersion = page["version"]!.GetValue<long>(), fields = new { name = "Assistant created project", icon = "folder", description = "Reviewed change" } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/items/change", create)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/items/change", create)).StatusCode);
        using var stranger = await fixture.AuthenticatedClient();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path + "/data?collection=projects")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(path + "/projects/$workspace/cards/search?limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/projects/$workspace/context")).StatusCode);
        var current = (await client.GetFromJsonAsync<JsonObject>(path + "/data?collection=projects"))!;
        var created = current["items"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "Assistant created project")!;
        var forbidden = new { collection = "projects", operation = "update", id = created["id"]!.GetValue<string>(), expectedVersion = current["version"]!.GetValue<long>(), fields = new { system = "activities" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/items/change", forbidden)).StatusCode);
        var cardResponse = await client.PostAsJsonAsync($"/api/workspaces/{workspace}/cards", new { title = "Workspace assistant search target", description = "", project = created["id"]!.GetValue<string>(), priority = "Medium" });
        Assert.Equal(HttpStatusCode.Created, cardResponse.StatusCode);
        var card = (await cardResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var search = (await client.GetFromJsonAsync<JsonObject>(path + "/projects/$workspace/cards/search?q=assistant&limit=1"))!;
        Assert.Single(search["cards"]!.AsArray());
        Assert.Equal(card["id"]!.GetValue<string>(), search["cards"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + $"/projects/$workspace/cards/{card["id"]!.GetValue<string>()}/context")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + $"/projects/my-activities/cards/{card["id"]!.GetValue<string>()}/context")).StatusCode);
    }
}
