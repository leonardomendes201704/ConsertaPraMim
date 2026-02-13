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
        var entry = _context.Entry(user);
        if (entry.State != EntityState.Detached)
        {
            await EnsureOnboardingGraphStateAsync(user);
            await _context.SaveChangesAsync();
            return;
        }

        var existing = await _context.Users
            .Include(u => u.ProviderProfile)
            .ThenInclude(p => p!.OnboardingDocuments)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (existing == null)
        {
            throw new InvalidOperationException($"User '{user.Id}' not found for update.");
        }

        _context.Entry(existing).CurrentValues.SetValues(user);
        existing.ProfilePictureUrl = user.ProfilePictureUrl;

        if (user.ProviderProfile != null)
        {
            if (existing.ProviderProfile == null)
            {
                existing.ProviderProfile = user.ProviderProfile;
                existing.ProviderProfile.UserId = existing.Id;
            }
            else
            {
                _context.Entry(existing.ProviderProfile).CurrentValues.SetValues(user.ProviderProfile);

                foreach (var incomingDoc in user.ProviderProfile.OnboardingDocuments)
                {
                    var currentDoc = existing.ProviderProfile.OnboardingDocuments
                        .FirstOrDefault(d => d.Id == incomingDoc.Id);

                    if (currentDoc == null)
                    {
                        existing.ProviderProfile.OnboardingDocuments.Add(incomingDoc);
                    }
                    else
                    {
                        _context.Entry(currentDoc).CurrentValues.SetValues(incomingDoc);
                    }
                }
            }
        }

        await EnsureOnboardingGraphStateAsync(existing);
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

    private async Task EnsureOnboardingGraphStateAsync(User user)
    {
        if (user.ProviderProfile == null)
        {
            return;
        }

        var profile = user.ProviderProfile;
        var profileEntry = _context.Entry(profile);

        if (profileEntry.State is EntityState.Modified or EntityState.Detached)
        {
            var profileExists = await _context.ProviderProfiles
                .AsNoTracking()
                .AnyAsync(p => p.Id == profile.Id);

            if (!profileExists)
            {
                if (profileEntry.State == EntityState.Detached)
                {
                    _context.ProviderProfiles.Add(profile);
                }
                else
                {
                    profileEntry.State = EntityState.Added;
                }
            }
            else if (profileEntry.State == EntityState.Detached)
            {
                _context.ProviderProfiles.Attach(profile);
                _context.Entry(profile).State = EntityState.Modified;
            }
        }

        foreach (var doc in profile.OnboardingDocuments)
        {
            var docEntry = _context.Entry(doc);
            if (docEntry.State is EntityState.Unchanged or EntityState.Added or EntityState.Deleted)
            {
                continue;
            }

            var docExists = await _context.ProviderOnboardingDocuments
                .AsNoTracking()
                .AnyAsync(d => d.Id == doc.Id);

            if (!docExists)
            {
                if (docEntry.State == EntityState.Detached)
                {
                    _context.ProviderOnboardingDocuments.Add(doc);
                }
                else
                {
                    docEntry.State = EntityState.Added;
                }
            }
            else if (docEntry.State == EntityState.Detached)
            {
                _context.ProviderOnboardingDocuments.Attach(doc);
                _context.Entry(doc).State = EntityState.Modified;
            }
        }
    }
}
