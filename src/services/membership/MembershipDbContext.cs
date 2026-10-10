using Kanbada.Api;
using Microsoft.EntityFrameworkCore;

namespace Domus.Membership;

public sealed class MembershipDbContext(DbContextOptions<KanbadaDbContext> options) : KanbadaDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        var types = new[] { typeof(UserEntity), typeof(SessionEntity), typeof(IdentityEntity), typeof(RecoveryCodeEntity) };
        foreach (var entity in model.Model.GetEntityTypes().ToArray())
            if (!types.Contains(entity.ClrType)) model.Ignore(entity.ClrType);
    }
}
public sealed class BoardsData(KanbadaDbContext context) : IDisposable
{
    public KanbadaDbContext Context { get; } = context;
    public void Dispose() => Context.Dispose();
}
