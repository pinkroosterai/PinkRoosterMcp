using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class UserService(AppDbContext db) : IUserService
{
    public async Task<List<AuthUserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => ToResponse(u))
            .ToListAsync(ct);
    }

    public async Task<AuthUserResponse?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToResponse(user);
    }

    public async Task<AuthUserResponse> CreateAsync(
        string email, string password, string displayName, GlobalRole globalRole, CancellationToken ct = default)
    {
        var emailExists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (emailExists)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = AuthService.HashPassword(password),
            GlobalRole = GlobalRole.User, // Always User — per-project roles handle access
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return ToResponse(user);
    }

    public async Task<AuthUserResponse?> UpdateAsync(
        long id, string? displayName, string? email, GlobalRole? globalRole, bool? isActive, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return null;

        if (displayName is not null) user.DisplayName = displayName;
        if (email is not null) user.Email = email;
        // Only allow promotion to SuperUser — no other global role changes
        if (globalRole == GlobalRole.SuperUser) user.GlobalRole = GlobalRole.SuperUser;
        if (isActive is not null) user.IsActive = isActive.Value;

        await db.SaveChangesAsync(ct);
        return ToResponse(user);
    }

    public async Task<bool> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return false;

        user.IsActive = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AuthUserResponse?> UpdateProfileAsync(
        long userId, string? displayName, string? email, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;

        if (displayName is not null) user.DisplayName = displayName;
        if (email is not null) user.Email = email;

        await db.SaveChangesAsync(ct);
        return ToResponse(user);
    }

    public async Task<bool> ChangePasswordAsync(
        long userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;

        if (!AuthService.VerifyPassword(user.PasswordHash, currentPassword))
            return false;

        // Invalidate all existing sessions first (before password is updated)
        await db.UserSessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

        user.PasswordHash = AuthService.HashPassword(newPassword);
        await db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> VerifyPasswordAsync(long userId, string password, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;
        return AuthService.VerifyPassword(user.PasswordHash, password);
    }

    private static AuthUserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        GlobalRole = user.GlobalRole.ToString(),
        IsActive = user.IsActive
    };
}
