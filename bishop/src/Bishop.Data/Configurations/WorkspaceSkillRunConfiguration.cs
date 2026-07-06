using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class WorkspaceSkillRunConfiguration : IEntityTypeConfiguration<WorkspaceSkillRun>
{
    public void Configure(EntityTypeBuilder<WorkspaceSkillRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SkillName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ProjectName).HasMaxLength(200);
        builder.Property(r => r.GitSha).HasMaxLength(40).IsRequired();
        builder.HasIndex(r => new { r.WorkspaceId, r.SkillName, r.ProjectName, r.BatchId }).IsUnique();
        builder.HasOne(r => r.Workspace)
               .WithMany()
               .HasForeignKey(r => r.WorkspaceId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Batch>()
               .WithMany()
               .HasForeignKey(r => r.BatchId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
