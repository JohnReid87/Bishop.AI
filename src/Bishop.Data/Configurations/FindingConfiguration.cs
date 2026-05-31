using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.IdentityHash).HasMaxLength(64).IsRequired();
        builder.Property(f => f.Status).HasMaxLength(50).IsRequired();
        builder.Property(f => f.ProjectName).HasMaxLength(200);
        builder.Property(f => f.File).HasMaxLength(1000);
        builder.Property(f => f.Symbol).HasMaxLength(500);
        builder.Property(f => f.Rule).HasMaxLength(200);
        builder.Property(f => f.Severity).HasMaxLength(50);
        builder.Property(f => f.Title).HasMaxLength(500).IsRequired();
        builder.Property(f => f.Body).IsRequired();
        builder.Property(f => f.RebuttalText);

        builder.HasIndex(f => new { f.WorkspaceSkillRunId, f.IdentityHash }).IsUnique();

        builder.HasOne(f => f.Run)
               .WithMany(r => r.Findings)
               .HasForeignKey(f => f.WorkspaceSkillRunId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
