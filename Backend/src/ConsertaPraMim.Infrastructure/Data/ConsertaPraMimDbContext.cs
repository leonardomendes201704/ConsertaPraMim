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
    }
}
