using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Title).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.IsClosed).HasDefaultValue(false);
        builder.Property(c => c.TotalInputTokens).HasDefaultValue(0);
        builder.Property(c => c.TotalOutputTokens).HasDefaultValue(0);
        builder.Property(c => c.ClaudeRunCount).HasDefaultValue(0);
        builder.HasOne(c => c.Lane)
               .WithMany(l => l.Cards)
               .HasForeignKey(c => c.LaneId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Tag)
               .WithMany(t => t.Cards)
               .HasForeignKey(c => c.TagId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
