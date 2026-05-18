using Bishop.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bishop.Data.Configurations;

internal sealed class CardTagConfiguration : IEntityTypeConfiguration<CardTag>
{
    public void Configure(EntityTypeBuilder<CardTag> builder)
    {
        builder.HasKey(ct => new { ct.CardId, ct.TagId });
        builder.HasOne(ct => ct.Card)
               .WithMany(c => c.CardTags)
               .HasForeignKey(ct => ct.CardId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ct => ct.Tag)
               .WithMany(t => t.CardTags)
               .HasForeignKey(ct => ct.TagId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
