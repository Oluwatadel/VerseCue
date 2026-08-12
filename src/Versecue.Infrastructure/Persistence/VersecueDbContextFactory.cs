using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Versecue.Infrastructure.Persistence;

public sealed class VersecueDbContextFactory
    : IDesignTimeDbContextFactory<VersecueDbContext>
{
    public VersecueDbContext CreateDbContext(
        string[] args)
    {
        var databasePath =
            Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "bin",
                    "Debug",
                    "net8.0-windows",
                    "versecue.db"));

        var connectionString =
            $"Data Source={databasePath}";

        var optionsBuilder =
            new DbContextOptionsBuilder<VersecueDbContext>();

        optionsBuilder.UseSqlite(
            connectionString);

        return new VersecueDbContext(
            optionsBuilder.Options);
    }
}