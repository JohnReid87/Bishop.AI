using Microsoft.EntityFrameworkCore;

namespace Bishop.Data;

public sealed class BishopDbContext : DbContext
{
    public BishopDbContext(DbContextOptions<BishopDbContext> options) : base(options) { }
}
