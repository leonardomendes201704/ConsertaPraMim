using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConsertaPraMim.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

        var seedEnabled = configuration.GetValue<bool>("Seed:Enabled");
        var seedReset = configuration.GetValue<bool>("Seed:Reset");

        if (!seedEnabled) return;

        if (seedReset)
        {
            await context.Database.EnsureDeletedAsync();
        }

        // Apply migrations if any
        await context.Database.MigrateAsync();

        if (!seedReset && await context.Users.AnyAsync()) return;

        // Seed Providers (2)
        var providers = new List<User>
        {
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Prestador Alfa",
                Email = "prestador1@teste.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Phone = "21999990001",
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    RadiusKm = 20,
                    BaseLatitude = -22.9068,
                    BaseLongitude = -43.1729,
                    Categories = new List<ServiceCategory> { ServiceCategory.Cleaning, ServiceCategory.Electrical, ServiceCategory.Plumbing }
                }
            },
            new User
            {
                Id = Guid.NewGuid(),
                Name = "Prestador Beta",
                Email = "prestador2@teste.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Phone = "21999990002",
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    RadiusKm = 15,
                    BaseLatitude = -22.9130,
                    BaseLongitude = -43.2000,
                    Categories = new List<ServiceCategory> { ServiceCategory.Electronics, ServiceCategory.Appliances, ServiceCategory.Masonry }
                }
            }
        };

        // Seed Clients (5)
        var clients = new List<User>
        {
            new User { Id = Guid.NewGuid(), Name = "Cliente 01", Email = "cliente1@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Phone = "21911110001", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 02", Email = "cliente2@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Phone = "21911110002", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 03", Email = "cliente3@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Phone = "21911110003", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 04", Email = "cliente4@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Phone = "21911110004", Role = UserRole.Client, IsActive = true },
            new User { Id = Guid.NewGuid(), Name = "Cliente 05", Email = "cliente5@teste.com", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), Phone = "21911110005", Role = UserRole.Client, IsActive = true }
        };

        await context.Users.AddRangeAsync(providers);
        await context.Users.AddRangeAsync(clients);
        
        // Seed Default Admin
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = "Administrador",
            Email = "admin@teste.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Phone = "21988887777",
            Role = UserRole.Admin,
            IsActive = true
        };
        await context.Users.AddAsync(admin);

        await context.SaveChangesAsync();

        // Seed Service Requests (3 per client) - Status Created
        var categories = new[]
        {
            ServiceCategory.Electrical,
            ServiceCategory.Plumbing,
            ServiceCategory.Electronics,
            ServiceCategory.Appliances,
            ServiceCategory.Masonry,
            ServiceCategory.Cleaning
        };

        var requests = new List<ServiceRequest>();
        var baseLat = -22.9100;
        var baseLng = -43.1800;

        for (int i = 0; i < clients.Count; i++)
        {
            for (int j = 1; j <= 3; j++)
            {
                var idx = (i + j) % categories.Length;
                requests.Add(new ServiceRequest
                {
                    ClientId = clients[i].Id,
                    Category = categories[idx],
                    Description = $"Pedido {j} do {clients[i].Name}",
                    AddressStreet = $"Rua {i + 1}, {100 + j}",
                    AddressCity = "Rio de Janeiro",
                    AddressZip = "20000-000",
                    Latitude = baseLat + (i * 0.002) + (j * 0.001),
                    Longitude = baseLng - (i * 0.002) - (j * 0.001),
                    Status = ServiceRequestStatus.Created
                });
            }
        }

        await context.ServiceRequests.AddRangeAsync(requests);
        await context.SaveChangesAsync();
    }
}
