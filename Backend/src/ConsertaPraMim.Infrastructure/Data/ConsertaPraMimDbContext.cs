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
    public DbSet<ProviderOnboardingDocument> ProviderOnboardingDocuments { get; set; }
    public DbSet<ProviderPlanSetting> ProviderPlanSettings { get; set; }
    public DbSet<ProviderPlanPromotion> ProviderPlanPromotions { get; set; }
    public DbSet<ProviderPlanCoupon> ProviderPlanCoupons { get; set; }
    public DbSet<ProviderPlanCouponRedemption> ProviderPlanCouponRedemptions { get; set; }
    public DbSet<ProviderCreditWallet> ProviderCreditWallets { get; set; }
    public DbSet<ProviderCreditLedgerEntry> ProviderCreditLedgerEntries { get; set; }
    public DbSet<ServiceCategoryDefinition> ServiceCategoryDefinitions { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ProviderGalleryAlbum> ProviderGalleryAlbums { get; set; }
    public DbSet<ProviderGalleryItem> ProviderGalleryItems { get; set; }
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
            (c1, c2) => (c1 ?? new List<ServiceCategory>()).SequenceEqual(c2 ?? new List<ServiceCategory>()),
            c => (c ?? new List<ServiceCategory>()).Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => (c ?? new List<ServiceCategory>()).ToList());

        modelBuilder.Entity<ProviderProfile>()
            .Property(e => e.Categories)
            .HasConversion(splitStringConverter, listComparer);

        modelBuilder.Entity<ProviderPlanSetting>()
            .Property(e => e.AllowedCategories)
            .HasConversion(splitStringConverter, listComparer);

        // Relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.ProviderProfile)
            .WithOne(p => p.User)
            .HasForeignKey<ProviderProfile>(p => p.UserId);

        modelBuilder.Entity<ProviderProfile>()
            .Property(p => p.OperationalComplianceNotes)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .HasOne(d => d.ProviderProfile)
            .WithMany(p => p.OnboardingDocuments)
            .HasForeignKey(d => d.ProviderProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .Property(d => d.FileName)
            .HasMaxLength(255);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .Property(d => d.MimeType)
            .HasMaxLength(100);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .Property(d => d.FileUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .Property(d => d.FileHashSha256)
            .HasMaxLength(128);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .Property(d => d.RejectionReason)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderOnboardingDocument>()
            .HasIndex(d => new { d.ProviderProfileId, d.DocumentType });

        modelBuilder.Entity<ProviderPlanSetting>()
            .HasIndex(s => s.Plan)
            .IsUnique();

        modelBuilder.Entity<ProviderPlanSetting>()
            .Property(s => s.MonthlyPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderPlanPromotion>()
            .Property(p => p.Name)
            .HasMaxLength(140);

        modelBuilder.Entity<ProviderPlanPromotion>()
            .Property(p => p.DiscountValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderPlanPromotion>()
            .HasIndex(p => new { p.Plan, p.IsActive, p.StartsAtUtc, p.EndsAtUtc });

        modelBuilder.Entity<ProviderPlanCoupon>()
            .Property(c => c.Code)
            .HasMaxLength(40);

        modelBuilder.Entity<ProviderPlanCoupon>()
            .Property(c => c.Name)
            .HasMaxLength(120);

        modelBuilder.Entity<ProviderPlanCoupon>()
            .Property(c => c.DiscountValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderPlanCoupon>()
            .HasIndex(c => c.Code)
            .IsUnique();

        modelBuilder.Entity<ProviderPlanCoupon>()
            .HasIndex(c => new { c.IsActive, c.StartsAtUtc, c.EndsAtUtc });

        modelBuilder.Entity<ProviderPlanCouponRedemption>()
            .HasOne(r => r.Coupon)
            .WithMany(c => c.Redemptions)
            .HasForeignKey(r => r.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderPlanCouponRedemption>()
            .Property(r => r.DiscountApplied)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderPlanCouponRedemption>()
            .HasIndex(r => new { r.CouponId, r.ProviderId });

        modelBuilder.Entity<ProviderCreditWallet>()
            .Property(w => w.CurrentBalance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderCreditWallet>()
            .HasIndex(w => w.ProviderId)
            .IsUnique();

        modelBuilder.Entity<ProviderCreditWallet>()
            .HasOne(w => w.Provider)
            .WithMany()
            .HasForeignKey(w => w.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.BalanceBefore)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.BalanceAfter)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.Reason)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.Source)
            .HasMaxLength(120);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.ReferenceType)
            .HasMaxLength(80);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.AdminEmail)
            .HasMaxLength(320);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .Property(e => e.Metadata)
            .HasMaxLength(4000);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .HasOne(e => e.Wallet)
            .WithMany(w => w.Entries)
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .HasIndex(e => new { e.ProviderId, e.EffectiveAtUtc });

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .HasIndex(e => new { e.ProviderId, e.EntryType, e.EffectiveAtUtc });

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .HasIndex(e => e.ExpiresAtUtc);

        modelBuilder.Entity<ProviderCreditLedgerEntry>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProviderCreditLedgerEntries_Amount_Positive", "[Amount] > 0");
                t.HasCheckConstraint("CK_ProviderCreditLedgerEntries_Balance_NonNegative", "[BalanceAfter] >= 0");
            });

        modelBuilder.Entity<ServiceCategoryDefinition>()
            .Property(c => c.Name)
            .HasMaxLength(100);

        modelBuilder.Entity<ServiceCategoryDefinition>()
            .Property(c => c.Slug)
            .HasMaxLength(120);

        modelBuilder.Entity<ServiceCategoryDefinition>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<ServiceCategoryDefinition>()
            .HasIndex(c => c.IsActive);

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(r => r.Client)
            .WithMany(u => u.Requests)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceRequest>()
            .HasOne(r => r.CategoryDefinition)
            .WithMany(c => c.Requests)
            .HasForeignKey(r => r.CategoryDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Proposal>()
            .HasOne(p => p.Request)
            .WithMany(r => r.Proposals)
            .HasForeignKey(p => p.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proposal>()
            .Property(p => p.EstimatedValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Proposal>()
            .Property(p => p.InvalidationReason)
            .HasMaxLength(500);
            
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Request)
            .WithOne(s => s.Review)
            .HasForeignKey<Review>(r => r.RequestId);

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .HasOne(a => a.Provider)
            .WithMany()
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .HasOne(a => a.ServiceRequest)
            .WithMany()
            .HasForeignKey(a => a.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .Property(a => a.Name)
            .HasMaxLength(120);

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .Property(a => a.Category)
            .HasMaxLength(80);

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .HasIndex(a => new { a.ProviderId, a.CreatedAt });

        modelBuilder.Entity<ProviderGalleryAlbum>()
            .HasIndex(a => new { a.ProviderId, a.ServiceRequestId });

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasOne(i => i.Provider)
            .WithMany()
            .HasForeignKey(i => i.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasOne(i => i.Album)
            .WithMany(a => a.Items)
            .HasForeignKey(i => i.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasOne(i => i.ServiceRequest)
            .WithMany()
            .HasForeignKey(i => i.ServiceRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.FileUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.FileName)
            .HasMaxLength(255);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.ContentType)
            .HasMaxLength(100);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.MediaKind)
            .HasMaxLength(20);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.Category)
            .HasMaxLength(80);

        modelBuilder.Entity<ProviderGalleryItem>()
            .Property(i => i.Caption)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasIndex(i => new { i.ProviderId, i.CreatedAt });

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasIndex(i => i.AlbumId);

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
