using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Domus.Ai;

public sealed record AssistantAction(Guid Id, Guid Actor, Guid Workspace, Guid Conversation, string Project, string Tool, JsonObject Arguments, DateTimeOffset Expires)
{
    public int Attempted;
}
public sealed class AssistantActions
{
    private readonly ConcurrentDictionary<Guid, AssistantAction> actions = new();
    public AssistantAction Propose(Identity actor, Guid conversation, string project, string tool, JsonObject arguments)
    {
        foreach (var expired in actions.Where(p => p.Value.Expires < DateTimeOffset.UtcNow)) actions.TryRemove(expired.Key, out _);
        if (actions.Count >= 500 || !AssistantActionTools.Mutations.Contains(tool) || arguments.ToJsonString().Length > 10000) throw new AiError(429, "Demasiadas ações pendentes.");
        var action = new AssistantAction(Guid.NewGuid(), actor.UserId, actor.WorkspaceId, conversation, project, tool, arguments.DeepClone().AsObject(), DateTimeOffset.UtcNow.AddMinutes(30));
        actions[action.Id] = action;
        return action;
    }
    public AssistantAction Get(Guid id, Identity actor)
    {
        if (!actions.TryGetValue(id, out var action) || action.Actor != actor.UserId || action.Workspace != actor.WorkspaceId || action.Expires < DateTimeOffset.UtcNow) throw new AiError(404, "Ação indisponível ou expirada.");
        return action;
    }
    public static void Begin(AssistantAction action)
    {
        // Never automatically repeat an operation with potentially external effects.
        if (Interlocked.CompareExchange(ref action.Attempted, 1, 0) != 0) throw new AiError(409, "Esta ação já foi confirmada. Verifique o resultado antes de pedir outra ação.");
    }
}
