using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        builder.HasKey(f => f.WorkspaceId);
        builder.Property(f => f.UsdToGbp).HasColumnType("TEXT").IsRequired();
        builder.Property(f => f.FetchedAtUtc).IsRequired();
        builder.HasOne<Workspace>()
               .WithOne()
               .HasForeignKey<FxRate>(f => f.WorkspaceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
