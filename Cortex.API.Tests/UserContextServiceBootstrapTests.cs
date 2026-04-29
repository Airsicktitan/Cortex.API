using System.Security.Claims;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.API.Tests;

public sealed class UserContextServiceBootstrapTests
{
    [Fact]
    public async Task GetCurrentUserAsync_BootstrapsFirstAuthenticatedUser_AsActiveAdmin()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var principal = CreatePrincipal(
            auth0Id: "auth0|first-admin",
            email: "first.admin@syniti.com",
            name: "First Admin");

        var user = await service.GetCurrentUserAsync(principal, CancellationToken.None);

        Assert.Equal("auth0|first-admin", user.Auth0Id);
        Assert.Equal("first.admin@syniti.com", user.Email);
        Assert.Equal("First Admin", user.DisplayName);
        Assert.Equal(Auth0Roles.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(UserDepartmentPolicy.DefaultDeveloperDepartment, user.Department);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task GetCurrentUserAsync_DoesNotBootstrapSecondAuthenticatedUser_WhenActiveElevatedExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Auth0Id = "auth0|existing-admin",
            Email = "existing.admin@syniti.com",
            DisplayName = "Existing Admin",
            Role = Auth0Roles.Admin,
            IsActive = true,
            Department = UserDepartmentPolicy.DefaultDeveloperDepartment,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var principal = CreatePrincipal(
            auth0Id: "auth0|new-user",
            email: "new.user@syniti.com",
            name: "New User");

        var exception = await Assert.ThrowsAsync<AccessNotApprovedException>(() =>
            service.GetCurrentUserAsync(principal, CancellationToken.None));

        Assert.Equal(AccessNotApprovedException.Reasons.Inactive, exception.Reason);

        var createdShell = await db.Users.SingleAsync(u => u.Auth0Id == "auth0|new-user");
        Assert.Equal(Auth0Roles.User, createdShell.Role);
        Assert.False(createdShell.IsActive);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task GetCurrentUserAsync_LinksEmailMatchAndPromotes_WhenNoActiveElevatedExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Auth0Id = null,
            Email = "link.me@syniti.com",
            DisplayName = "Link Me",
            Role = Auth0Roles.User,
            IsActive = false,
            Department = null,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var principal = CreatePrincipal(
            auth0Id: "auth0|linked-user",
            email: "link.me@syniti.com",
            name: "Linked User");

        var user = await service.GetCurrentUserAsync(principal, CancellationToken.None);

        Assert.Equal("auth0|linked-user", user.Auth0Id);
        Assert.Equal(Auth0Roles.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(UserDepartmentPolicy.DefaultDeveloperDepartment, user.Department);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task GetCurrentUserAsync_FindsExistingByAuth0Id_WithoutCreatingDuplicateRow()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Auth0Id = "auth0|known",
            Email = "known.user@syniti.com",
            DisplayName = "Known User",
            Role = Auth0Roles.Admin,
            IsActive = true,
            Department = UserDepartmentPolicy.DefaultDeveloperDepartment,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var principal = CreatePrincipal(
            auth0Id: "auth0|known",
            email: "known.user@syniti.com",
            name: "Known User");

        var user = await service.GetCurrentUserAsync(principal, CancellationToken.None);

        Assert.Equal("auth0|known", user.Auth0Id);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    private static UserContextService CreateService(CortexDbContext dbContext)
    {
        return new UserContextService(
            new UserRepository(dbContext),
            new HttpContextAccessor(),
            new AccessApprovalService(),
            dbContext,
            NullLogger<UserContextService>.Instance);
    }

    private static ClaimsPrincipal CreatePrincipal(string auth0Id, string email, string name)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", auth0Id),
                new Claim("email", email),
                new Claim("name", name),
                new Claim("email_verified", "true")
            ],
            authenticationType: "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"user-context-bootstrap-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }
}
