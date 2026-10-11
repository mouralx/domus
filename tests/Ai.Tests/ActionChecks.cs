using Domus.Ai;
using System.Text.Json.Nodes;

public static class ActionChecks
{
    public static void Run()
    {
        var actions = new AssistantActions();
        var actor = new Identity(Guid.NewGuid(), Guid.NewGuid(), true, false);
        var arguments = new JsonObject { ["title"] = "Reviewed title" };
        var action = actions.Propose(actor, Guid.NewGuid(), "$workspace", "create_card", arguments);
        arguments["title"] = "Tampered title";
        if (actions.Get(action.Id, actor).Arguments["title"]!.GetValue<string>() != "Reviewed title") throw new Exception("Action arguments were not frozen.");
        void Denied(Identity other)
        {
            try { actions.Get(action.Id, other); throw new Exception("Action crossed an identity boundary."); }
            catch (AiError error) when (error.Status == 404) { }
        }
        Denied(actor with { UserId = Guid.NewGuid() });
        Denied(actor with { WorkspaceId = Guid.NewGuid() });
        AssistantActions.Begin(action);
        try { AssistantActions.Begin(action); throw new Exception("Action was replayed."); }
        catch (AiError error) when (error.Status == 409) { }
        Console.WriteLine("PASS: action confirmation binds actor/workspace, freezes arguments and rejects replay.");
    }
}
