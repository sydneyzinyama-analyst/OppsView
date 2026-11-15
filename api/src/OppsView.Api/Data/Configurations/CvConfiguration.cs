using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class CvConfiguration : IEntityTypeConfiguration<Cv>
{
    public void Configure(EntityTypeBuilder<Cv> builder)
    {
        builder.ToTable("Cvs");
        builder.HasKey(c => c.UserId); // one CV per user
        builder.Property(c => c.UserId).ValueGeneratedNever();
        builder.Property(c => c.FileName).HasMaxLength(Cv.FileNameMaxLength).IsRequired();
        builder.Property(c => c.Text).HasMaxLength(Cv.TextMaxLength).IsRequired();
        builder.HasOne<User>().WithOne().HasForeignKey<Cv>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
