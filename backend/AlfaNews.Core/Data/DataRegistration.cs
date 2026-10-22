using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Core.Data;

public static class DataRegistration
{
    public static IServiceCollection AddAlfaNewsData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AlfaNewsDbContext>(options =>
            options.UseNpgsql(connectionString, postgres => postgres.UseVector()));
        return services;
    }
}