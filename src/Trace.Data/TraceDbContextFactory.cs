using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trace.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can construct the context
/// without booting the web host. The connection string here is only used for
/// scaffolding migrations (which needs a provider, not a live database); the
/// running app supplies its own via configuration in <c>Program.cs</c>.
/// </summary>
public class TraceDbContextFactory : IDesignTimeDbContextFactory<TraceDbContext>
{
    public TraceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TRACE_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trace;Username=postgres;Password=wombles";

        var options = new DbContextOptionsBuilder<TraceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TraceDbContext(options);
    }
}
