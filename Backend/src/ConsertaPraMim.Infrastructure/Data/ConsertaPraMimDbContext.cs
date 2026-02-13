using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using System.Linq;

namespace ConsertaPraMim.Infrastructure.Data;

public class ConsertaPraMimDbContext : DbContext
{
    public ConsertaPraMimDbContext(DbContextOptions<ConsertaPraMimDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ProviderProfile> ProviderProfiles { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatAttachment> ChatAttachments { get; set; }
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Value Conversion for List<ServiceCategory> to string in SQLite
        var splitStringConverter = new ValueConverter<List<ServiceCategory>, string>(
            v => v == null ? string.Empty : string.Join(",", v.Select(e => (int)e)),
            v => string.IsNullOrEmpty(v) 
                ? new List<ServiceCategory>() 
                : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => (ServiceCategory)int.Parse(s))
                   .ToList());

        var listComparer = new ValueComparer<List<ServiceCategory>>(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<ProviderProfile>()
            .Property(e => e.Categories)
            .HasConversion(splitStringConverter, listComparer);

        // Relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.ProviderProfile)
            .WithOne(p => p.User)
            .HasForeignKey<ProviderProfile>(p => p.UserId);

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(r => r.Client)
            .WithMany(u => u.Requests)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Proposal>()
            .HasOne(p => p.Request)
            .WithMany(r => r.Proposals)
            .HasForeignKey(p => p.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proposal>()
            .Property(p => p.EstimatedValue)
            .HasPrecision(18, 2);
            
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Request)
            .WithOne(s => s.Review)
            .HasForeignKey<Review>(r => r.RequestId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Request)
            .WithMany()
            .HasForeignKey(m => m.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Text)
            .HasMaxLength(2000);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => new { m.RequestId, m.ProviderId, m.CreatedAt });

        modelBuilder.Entity<ChatAttachment>()
            .HasOne(a => a.ChatMessage)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatAttachment>()
            .Property(a => a.FileUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ChatAttachment>()
            .Property(a => a.FileName)
            .HasMaxLength(255);

        modelBuilder.Entity<ChatAttachment>()
            .Property(a => a.ContentType)
            .HasMaxLength(100);

        modelBuilder.Entity<ChatAttachment>()
            .Property(a => a.MediaKind)
            .HasMaxLength(20);

        modelBuilder.Entity<AdminAuditLog>()
            .Property(a => a.ActorEmail)
            .HasMaxLength(320);

        modelBuilder.Entity<AdminAuditLog>()
            .Property(a => a.Action)
            .HasMaxLength(80);

        modelBuilder.Entity<AdminAuditLog>()
            .Property(a => a.TargetType)
            .HasMaxLength(80);

        modelBuilder.Entity<AdminAuditLog>()
            .Property(a => a.Metadata)
            .HasMaxLength(4000);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => a.CreatedAt);

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => new { a.TargetType, a.TargetId });
    }
}
