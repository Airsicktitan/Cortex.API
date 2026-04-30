using System.Security.Claims;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests.TestDoubles;

/// <summary>Returns the lowest-id user in the database (test DBs typically have one actor).</summary>
public sealed class FirstUserContextService(CortexDbContext db) : IUserContextService
{
    private readonly CortexDbContext _db = db;

    public Task<User> GetCurrentUserAsync() =>
        _db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync();

    public Task<User> GetCurrentUserAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default) =>
        _db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync(cancellationToken);

    public Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request) =>
        throw new NotSupportedException();
}
