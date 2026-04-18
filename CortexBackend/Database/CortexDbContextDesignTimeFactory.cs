using System.IO;
using Cortex.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Cortex.API.Database;

/// <summary>
/// Enables EF Core CLI (<c>dotnet ef migrations</c> / <c>database update</c>) to build a <see cref="CortexDbContext"/>
/// using the same connection string resolution as the running app (appsettings + environment).
/// </summary>
public sealed class CortexDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CortexDbContext>
{
    public CortexDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(CortexDbContext).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = DatabaseConnectionConfiguration.ResolveFirstNonEmpty(configuration)
            ?? throw new InvalidOperationException(
                "No database connection string found for EF Core tools. " +
                "Use ASPNETCORE_ENVIRONMENT=Development with appsettings.Development.json, " +
                "or set ConnectionStrings__CortexDb, ConnectionStrings__AzureCortexDb, or ConnectionStrings__CortexDB.");

        var optionsBuilder = new DbContextOptionsBuilder<CortexDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new CortexDbContext(optionsBuilder.Options);
    }
}
