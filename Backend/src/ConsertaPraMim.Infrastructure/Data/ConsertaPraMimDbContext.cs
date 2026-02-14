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
    public DbSet<ServiceChecklistTemplate> ServiceChecklistTemplates { get; set; }
    public DbSet<ServiceChecklistTemplateItem> ServiceChecklistTemplateItems { get; set; }
    public DbSet<ServiceRequest> ServiceRequests { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ProviderGalleryAlbum> ProviderGalleryAlbums { get; set; }
    public DbSet<ProviderGalleryItem> ProviderGalleryItems { get; set; }
    public DbSet<ProviderAvailabilityRule> ProviderAvailabilityRules { get; set; }
    public DbSet<ProviderAvailabilityException> ProviderAvailabilityExceptions { get; set; }
    public DbSet<ServiceAppointment> ServiceAppointments { get; set; }
    public DbSet<ServiceAppointmentHistory> ServiceAppointmentHistories { get; set; }
    public DbSet<ServiceAppointmentChecklistResponse> ServiceAppointmentChecklistResponses { get; set; }
    public DbSet<ServiceAppointmentChecklistHistory> ServiceAppointmentChecklistHistories { get; set; }
    public DbSet<AppointmentReminderDispatch> AppointmentReminderDispatches { get; set; }
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

        modelBuilder.Entity<ServiceChecklistTemplate>()
            .HasOne(t => t.CategoryDefinition)
            .WithMany(c => c.ChecklistTemplates)
            .HasForeignKey(t => t.CategoryDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceChecklistTemplate>()
            .Property(t => t.Name)
            .HasMaxLength(120);

        modelBuilder.Entity<ServiceChecklistTemplate>()
            .Property(t => t.Description)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceChecklistTemplate>()
            .HasIndex(t => new { t.CategoryDefinitionId, t.IsActive });

        modelBuilder.Entity<ServiceChecklistTemplate>()
            .HasIndex(t => t.CategoryDefinitionId)
            .IsUnique();

        modelBuilder.Entity<ServiceChecklistTemplateItem>()
            .HasOne(i => i.Template)
            .WithMany(t => t.Items)
            .HasForeignKey(i => i.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceChecklistTemplateItem>()
            .Property(i => i.Title)
            .HasMaxLength(180);

        modelBuilder.Entity<ServiceChecklistTemplateItem>()
            .Property(i => i.HelpText)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceChecklistTemplateItem>()
            .HasIndex(i => new { i.TemplateId, i.SortOrder });

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
            .HasOne(i => i.ServiceAppointment)
            .WithMany()
            .HasForeignKey(i => i.ServiceAppointmentId)
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
            .Property(i => i.EvidencePhase)
            .HasConversion<int?>();

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

        modelBuilder.Entity<ProviderGalleryItem>()
            .HasIndex(i => i.ServiceAppointmentId);

        modelBuilder.Entity<ProviderAvailabilityRule>()
            .HasOne(r => r.Provider)
            .WithMany()
            .HasForeignKey(r => r.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderAvailabilityRule>()
            .Property(r => r.SlotDurationMinutes)
            .HasDefaultValue(30);

        modelBuilder.Entity<ProviderAvailabilityRule>()
            .HasIndex(r => new { r.ProviderId, r.DayOfWeek, r.StartTime, r.EndTime });

        modelBuilder.Entity<ProviderAvailabilityRule>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProviderAvailabilityRules_StartBeforeEnd", "[EndTime] > [StartTime]");
                t.HasCheckConstraint("CK_ProviderAvailabilityRules_SlotDuration_Range", "[SlotDurationMinutes] BETWEEN 15 AND 240");
            });

        modelBuilder.Entity<ProviderAvailabilityException>()
            .HasOne(e => e.Provider)
            .WithMany()
            .HasForeignKey(e => e.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderAvailabilityException>()
            .Property(e => e.Reason)
            .HasMaxLength(500);

        modelBuilder.Entity<ProviderAvailabilityException>()
            .HasIndex(e => new { e.ProviderId, e.StartsAtUtc, e.EndsAtUtc });

        modelBuilder.Entity<ProviderAvailabilityException>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProviderAvailabilityExceptions_StartBeforeEnd", "[EndsAtUtc] > [StartsAtUtc]");
            });

        modelBuilder.Entity<ServiceAppointment>()
            .HasOne(a => a.ServiceRequest)
            .WithMany(r => r.Appointments)
            .HasForeignKey(a => a.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceAppointment>()
            .HasOne(a => a.Client)
            .WithMany()
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceAppointment>()
            .HasOne(a => a.Provider)
            .WithMany()
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceAppointment>()
            .Property(a => a.Reason)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointment>()
            .Property(a => a.RescheduleRequestReason)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointment>()
            .Property(a => a.ArrivedManualReason)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointment>()
            .Property(a => a.OperationalStatusReason)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => a.ServiceRequestId)
            .HasDatabaseName("IX_ServiceAppointments_ServiceRequestId");

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ServiceRequestId, a.WindowStartUtc, a.WindowEndUtc })
            .HasDatabaseName("IX_ServiceAppointments_Request_Window");

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ProviderId, a.WindowStartUtc, a.WindowEndUtc });

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ClientId, a.WindowStartUtc, a.WindowEndUtc });

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ProviderId, a.Status, a.WindowStartUtc });

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ProviderId, a.OperationalStatus, a.WindowStartUtc });

        modelBuilder.Entity<ServiceAppointment>()
            .HasIndex(a => new { a.ProviderId, a.ProposedWindowStartUtc, a.ProposedWindowEndUtc });

        modelBuilder.Entity<ServiceAppointment>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_ServiceAppointments_WindowStartBeforeEnd", "[WindowEndUtc] > [WindowStartUtc]");
            });

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .HasOne(h => h.ServiceAppointment)
            .WithMany(a => a.History)
            .HasForeignKey(h => h.ServiceAppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .HasOne(h => h.ActorUser)
            .WithMany()
            .HasForeignKey(h => h.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .Property(h => h.Reason)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .Property(h => h.Metadata)
            .HasMaxLength(4000);

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .HasIndex(h => new { h.ServiceAppointmentId, h.OccurredAtUtc });

        modelBuilder.Entity<ServiceAppointmentHistory>()
            .HasIndex(h => new { h.ServiceAppointmentId, h.NewOperationalStatus, h.OccurredAtUtc });

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .HasOne(r => r.ServiceAppointment)
            .WithMany(a => a.ChecklistResponses)
            .HasForeignKey(r => r.ServiceAppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .HasOne(r => r.TemplateItem)
            .WithMany(i => i.Responses)
            .HasForeignKey(r => r.TemplateItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .HasOne(r => r.CheckedByUser)
            .WithMany()
            .HasForeignKey(r => r.CheckedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .Property(r => r.Note)
            .HasMaxLength(1000);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .Property(r => r.EvidenceUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .Property(r => r.EvidenceFileName)
            .HasMaxLength(255);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .Property(r => r.EvidenceContentType)
            .HasMaxLength(100);

        modelBuilder.Entity<ServiceAppointmentChecklistResponse>()
            .HasIndex(r => new { r.ServiceAppointmentId, r.TemplateItemId })
            .IsUnique();

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .HasOne(h => h.ServiceAppointment)
            .WithMany(a => a.ChecklistHistory)
            .HasForeignKey(h => h.ServiceAppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .HasOne(h => h.TemplateItem)
            .WithMany(i => i.History)
            .HasForeignKey(h => h.TemplateItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .HasOne(h => h.ActorUser)
            .WithMany()
            .HasForeignKey(h => h.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .Property(h => h.PreviousNote)
            .HasMaxLength(1000);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .Property(h => h.NewNote)
            .HasMaxLength(1000);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .Property(h => h.PreviousEvidenceUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .Property(h => h.NewEvidenceUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<ServiceAppointmentChecklistHistory>()
            .HasIndex(h => new { h.ServiceAppointmentId, h.OccurredAtUtc });

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .HasOne(r => r.ServiceAppointment)
            .WithMany()
            .HasForeignKey(r => r.ServiceAppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .HasOne(r => r.RecipientUser)
            .WithMany()
            .HasForeignKey(r => r.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .Property(r => r.EventKey)
            .HasMaxLength(240);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .Property(r => r.Subject)
            .HasMaxLength(180);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .Property(r => r.Message)
            .HasMaxLength(1500);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .Property(r => r.ActionUrl)
            .HasMaxLength(500);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .Property(r => r.LastError)
            .HasMaxLength(1000);

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .HasIndex(r => r.EventKey)
            .IsUnique();

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .HasIndex(r => new { r.Status, r.NextAttemptAtUtc });

        modelBuilder.Entity<AppointmentReminderDispatch>()
            .HasIndex(r => new { r.ServiceAppointmentId, r.Channel, r.ReminderOffsetMinutes });

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
