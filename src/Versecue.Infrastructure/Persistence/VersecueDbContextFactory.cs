using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;
using Versecue.Infrastructure.Common;

namespace Versecue.Infrastructure.Persistence;

public sealed class VersecueDbContextFactory : IDesignTimeDbContextFactory<VersecueDbContext>
{
    public VersecueDbContext CreateDbContext(
        string[] args)
    {
        var dbPath =
            VerseCueDatabasePath.GetDatabasePath();

        var optionsBuilder =
            new DbContextOptionsBuilder<VersecueDbContext>();

        optionsBuilder.UseSqlite(
            $"Data Source={dbPath}");

        return new VersecueDbContext(
            optionsBuilder.Options);
    }
}