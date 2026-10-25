using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;
using AlfaNews.Core.Configuration;

namespace AlfaNews.Core.Data;

public sealed class AlfaNewsDbContextFactory : IDesignTimeDbContextFactory<AlfaNewsDbContext>
{
    public AlfaNewsDbContext CreateDbContext(string[] args)
    {
        LocalEnvironment.Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                               ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection.");
        var options = new DbContextOptionsBuilder<AlfaNewsDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector()).Options;
        return new AlfaNewsDbContext(options);
    }
}