using System;
using Microsoft.EntityFrameworkCore;

namespace negosuite_api.Models;

public class UserPagePreference
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string PageKey { get; set; }
    public string ColumnsJson { get; set; }
    public long Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public partial class negosuiteContext
{
    public DbSet<UserPagePreference> UserPagePreferences { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserPagePreference>(entity =>
        {
            entity.ToTable("user_page_preference");
            entity.HasKey(p => new { p.UserId, p.CompanyId, p.PageKey });
            entity.Property(p => p.PageKey).HasMaxLength(64);
            entity.Property(p => p.ColumnsJson).HasColumnType("json").IsRequired();
            entity.Property(p => p.UpdatedAtUtc).HasColumnType("datetime(6)");
            entity.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Config>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
