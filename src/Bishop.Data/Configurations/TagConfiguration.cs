using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Colour).HasMaxLength(20).IsRequired();
        builder.HasIndex(t => new { t.WorkspaceId, t.Name }).IsUnique();
        builder.HasOne(t => t.Workspace)
               .WithMany(w => w.Tags)
               .HasForeignKey(t => t.WorkspaceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
