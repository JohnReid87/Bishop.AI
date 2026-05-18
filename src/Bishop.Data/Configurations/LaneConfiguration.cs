using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class LaneConfiguration : IEntityTypeConfiguration<Lane>
{
    public void Configure(EntityTypeBuilder<Lane> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(l => l.Workspace)
               .WithMany(w => w.Lanes)
               .HasForeignKey(l => l.WorkspaceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
