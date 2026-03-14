using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Data;
using PinkRooster.Data.Entities;
using PinkRooster.Shared.DTOs.Requests;
using PinkRooster.Shared.DTOs.Responses;
using PinkRooster.Shared.Enums;

namespace PinkRooster.Api.Services;

public sealed class AuthService(AppDbContext db) : IAuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MemorySize = 65536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(24);

    public async Task<AuthUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async (cancellation) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellation);

            // Check if this is the first user (atomically within serializable transaction)
            var hasUsers = await db.Users.AnyAsync(cancellation);

            // Check for duplicate email
            var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email, cancellation);
            if (emailExists)
                throw new InvalidOperationException("A user with this email already exists.");

            var user = new User
            {
                Email = request.Email,
                DisplayName = request.DisplayName,
                PasswordHash = HashPassword(request.Password),
                GlobalRole = hasUsers ? GlobalRole.User : GlobalRole.SuperUser,
                IsActive = true
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);

            return ToAuthUserResponse(user);
        }, ct);
    }

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<(LoginResponse Response, string SessionToken)?> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !user.IsActive)
            return null;

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTimeOffset.UtcNow)
            return null;

        // Verify password
        if (!VerifyPassword(user.PasswordHash, request.Password))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
                user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
            await db.SaveChangesAsync(ct);
            return null;
        }

        // Successful login — reset lockout counters
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;

        var token = Guid.NewGuid().ToString();
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionDuration);

        var session = new UserSession
        {
            UserId = user.Id,
            Token = HashToken(token),
            ExpiresAt = expiresAt
        };

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var response = new LoginResponse
        {
            User = ToAuthUserResponse(user),
            ExpiresAt = expiresAt
        };

        return (response, token);
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(sessionToken);
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Token == tokenHash, ct);
        if (session is not null)
        {
            db.UserSessions.Remove(session);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(string sessionToken, CancellationToken ct = default)
    {
        var user = await ValidateSessionAsync(sessionToken, ct);
        return user is null ? null : ToAuthUserResponse(user);
    }

    public async Task<User?> ValidateSessionAsync(string sessionToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(sessionToken);
        var session = await db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Token == tokenHash && s.ExpiresAt > DateTimeOffset.UtcNow, ct);

        if (session?.User is null || !session.User.IsActive)
            return null;

        return session.User;
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(ct);
    }

    // ── Session token hashing ──

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // ── Password hashing ──

    internal static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        argon2.DegreeOfParallelism = Parallelism;

        var hash = argon2.GetBytes(HashSize);

        // Combine salt + hash for storage
        var combined = new byte[SaltSize + HashSize];
        salt.CopyTo(combined, 0);
        hash.CopyTo(combined, SaltSize);

        return Convert.ToBase64String(combined);
    }

    internal static bool VerifyPassword(string storedHash, string password)
    {
        var combined = Convert.FromBase64String(storedHash);
        if (combined.Length != SaltSize + HashSize)
            return false;

        var salt = combined[..SaltSize];
        var expectedHash = combined[SaltSize..];

        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        argon2.DegreeOfParallelism = Parallelism;

        var actualHash = argon2.GetBytes(HashSize);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    // ── Mapping ──

    private static AuthUserResponse ToAuthUserResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        GlobalRole = user.GlobalRole.ToString(),
        IsActive = user.IsActive
    };
}
