using HCS.Auditing;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.EntityFrameworkCore.Auditing;

public static class HcsAuditProjectionModelBuilderExtensions
{
    public static void ConfigureHcsAuditProjection(this ModelBuilder builder)
    {
        builder.Entity<AuditRecordProjection>(entity =>
        {
            entity.ToTable("HcsAuditRecordProjections");
            entity.ConfigureByConvention();
            entity.Property(record => record.SourceService).IsRequired().HasMaxLength(128);
            entity.Property(record => record.ApplicationName).HasMaxLength(128);
            entity.Property(record => record.UserName).HasMaxLength(256);
            entity.Property(record => record.ActionName).HasMaxLength(256);
            entity.Property(record => record.HttpMethod).HasMaxLength(16);
            entity.Property(record => record.Url).HasMaxLength(2048);
            entity.Property(record => record.CorrelationId).HasMaxLength(128);
            entity.Property(record => record.ClientIpAddress).HasMaxLength(64);
            entity.Property(record => record.ActionsJson).HasColumnType("text");
            entity.Property(record => record.EntityChangesJson).HasColumnType("text");
            entity.HasIndex(record => record.ExecutionTime);
            entity.HasIndex(record => record.UserId);
            entity.HasIndex(record => record.CorrelationId);
            entity.HasIndex(record => new { record.SourceService, record.ActionName });
        });
    }
}
