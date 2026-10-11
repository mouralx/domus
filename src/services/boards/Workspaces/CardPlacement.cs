using System.Text.Json.Nodes;
namespace Domus.Boards.Api;
public static class CardPlacement
{
    public static void AssignDefaults(JsonObject state)
    {
        var buckets = WorkspaceJson.Items(state, "buckets");
        var lanes = WorkspaceJson.Items(state, "swimlanes");
        if (lanes.Count > 0)
        {
            var first = lanes[0]!;
            foreach (var project in WorkspaceJson.Items(state, "projects"))
            {
                var id = WorkspaceJson.Text(project, "id");
                if (lanes.Any(l => WorkspaceJson.Text(l, "project") == id)) continue;
                lanes.Add(new JsonObject { ["id"] = "lane-" + Guid.NewGuid().ToString("N"), ["project"] = id,
                    ["name"] = WorkspaceJson.Text(first, "name"), ["color"] = WorkspaceJson.Text(first, "color"), ["complete"] = false });
            }
        }
        foreach (var card in WorkspaceJson.Items(state, "tasks"))
        {
            if (WorkspaceJson.Text(card, "bucket") == "")
                card!["bucket"] = buckets.Count > 0 ? WorkspaceJson.Text(buckets[0], "name") : "";
            if (WorkspaceJson.Text(card, "swimlane") == "")
                card!["swimlane"] = WorkspaceJson.Text(lanes.FirstOrDefault(l => WorkspaceJson.Text(l, "project") == WorkspaceJson.Text(card, "project")), "name");
        }
    }
}
