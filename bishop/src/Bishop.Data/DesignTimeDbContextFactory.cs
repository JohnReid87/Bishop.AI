using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bishop.Data;

internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BishopDbContext>
{
    public BishopDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "bishop-design.db")}")
            .Options;
        return new BishopDbContext(options);
    }
}
