using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ConsertaPraMimDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
    
    // Check if we already have users
    if (!await context.Users.AnyAsync())
    {
        Console.WriteLine("Creating test users...");
        
        // Create a CLIENT user
        var client = new User
        {
            Id = Guid.NewGuid(),
            Name = "Cliente Teste",
            Email = "cliente@teste.com",
            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
            Phone = "(11) 99999-1111",
            Role = UserRole.Client,
            CreatedAt = DateTime.UtcNow
        };
        
        // Create a PROVIDER user
        var provider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Teste",
            Email = "prestador@teste.com",
            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
            Phone = "(11) 99999-2222",
            Role = UserRole.Provider,
            CreatedAt = DateTime.UtcNow
        };
        
        // Create provider profile
        var providerProfile = new ProviderProfile
        {
            Id = Guid.NewGuid(),
            UserId = provider.Id,
            Plan = ProviderPlan.Trial,
            RadiusKm = 10.0,
            BaseLatitude = -23.5505,
            BaseLongitude = -46.6333,
            IsVerified = false,
            Categories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing },
            Rating = 0,
            ReviewCount = 0
        };
        
        await context.Users.AddAsync(client);
        await context.Users.AddAsync(provider);
        await context.ProviderProfiles.AddAsync(providerProfile);
        await context.SaveChangesAsync();
        
        Console.WriteLine("✅ Test users created successfully!");
        Console.WriteLine();
        Console.WriteLine("CLIENT:");
        Console.WriteLine($"  Email: {client.Email}");
        Console.WriteLine("  Password: 123456");
        Console.WriteLine();
        Console.WriteLine("PROVIDER:");
        Console.WriteLine($"  Email: {provider.Email}");
        Console.WriteLine("  Password: 123456");
    }
    else
    {
        Console.WriteLine("Database already has users. Skipping seed.");
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
