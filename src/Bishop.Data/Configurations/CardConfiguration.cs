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
        builder.HasOne(c => c.Lane)
               .WithMany(l => l.Cards)
               .HasForeignKey(c => c.LaneId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
