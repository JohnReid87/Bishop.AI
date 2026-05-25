using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Name).HasMaxLength(500).IsRequired();
        builder.Property(b => b.BranchName).HasMaxLength(300).IsRequired();
        builder.Property(b => b.BaseBranch).HasMaxLength(300).IsRequired();
        builder.Property(b => b.WorktreePath).IsRequired();
        builder.Property(b => b.Status).IsRequired();
        builder.Property(b => b.GitHubPrUrl).HasMaxLength(2048);
        builder.HasIndex(b => b.BranchName).IsUnique().HasFilter("Status != 2");
    }
}
