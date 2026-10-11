using Domus.Boards.Api;
using Microsoft.EntityFrameworkCore;

namespace Domus.Membership;

public sealed class MembershipDbContext(DbContextOptions<BoardsDbContext> options) : BoardsDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        var types = new[] { typeof(UserEntity), typeof(SessionEntity), typeof(IdentityEntity), typeof(RecoveryCodeEntity), typeof(DirectoryEvent) };
        model.Entity<DirectoryEvent>(entity =>
        {
            entity.ToTable("membership_directory_outbox"); entity.HasKey(row => row.UserId);
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.Payload).HasColumnName("payload");
            entity.Property(row => row.Version).HasColumnName("version");
        });
        foreach (var entity in model.Model.GetEntityTypes().ToArray())
            if (!types.Contains(entity.ClrType)) model.Ignore(entity.ClrType);
    }
}
