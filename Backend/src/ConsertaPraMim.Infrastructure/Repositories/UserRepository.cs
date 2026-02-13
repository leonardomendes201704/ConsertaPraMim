using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public UserRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.ProviderProfile)
            .ThenInclude(p => p!.OnboardingDocuments)
            .FirstOrDefaultAsync(u => u.Email == email);
    }
    
    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }
    
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.ProviderProfile)
            .ThenInclude(p => p!.OnboardingDocuments)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.ProviderProfile)
            .ThenInclude(p => p!.OnboardingDocuments)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }
}
