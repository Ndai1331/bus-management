using HCS.Identity;
using Microsoft.EntityFrameworkCore;

namespace HCS.EntityFrameworkCore;

public static class HcsUserAvatarModelBuilderExtensions
{
    public static void ConfigureHcsUserAvatars(this ModelBuilder builder)
    {
        builder.Entity<UserAvatar>(b =>
        {
            b.ToTable("HcsUserAvatars");
            b.HasKey(x => x.UserId);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            b.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            b.Property(x => x.BlobName).IsRequired().HasMaxLength(512);
            b.Property(x => x.Size).IsRequired();
            b.Property(x => x.CreationTime).IsRequired();
            b.Property(x => x.LastModificationTime).IsRequired();
            b.HasIndex(x => x.BlobName).IsUnique();
        });
    }
}
