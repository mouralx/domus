using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domus.Boards.Api;

/// <summary>Allows EF tooling to generate and apply migrations without starting the web host.</summary>
public sealed class BoardsDbContextFactory : IDesignTimeDbContextFactory<BoardsDbContext>
{
    public BoardsDbContext CreateDbContext(string[] args)
    {
        var directory = File.Exists("Domus.Boards.Api.csproj") ? Directory.GetCurrentDirectory() : Path.Combine(Directory.GetCurrentDirectory(), "api");
        var configuration = new ConfigurationBuilder().SetBasePath(directory).AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Database=domus;Username=domus";
        return new BoardsDbContext(new DbContextOptionsBuilder<BoardsDbContext>().UseNpgsql(connection).Options);
    }
}
