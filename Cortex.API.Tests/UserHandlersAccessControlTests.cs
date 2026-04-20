using System.Security.Claims;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.API.Tests;

/// <summary>
/// Regression guards for the Developer → Admin privilege escalation paths.
/// Every test here exercises a denial path. Mocks are strict so any unexpected
/// side-effect (SaveChanges, SyncRoleToAuth0, Auth0 calls) would fail the test —
/// that is the whole point of the access-control gate.
/// </summary>
public class UserHandlersAccessControlTests
{
    private const int DeveloperCallerId = 10;
    private const int AdminCallerId = 99;
    private const int OtherUserId = 20;
    private const int AdminTargetId = 30;

    [Fact]
    public async Task UpdateUser_BlocksNonAdminPromotingOtherUserToAdmin()
    {
        var target = new User
        {
            Id = OtherUserId,
            Email = "target@cortex.com",
            Role = Auth0Roles.User,
            IsActive = true,
        };

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest { Role = Auth0Roles.Admin, IsActive = true },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturning(developerCaller: true),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UpdateUser_BlocksNonAdminModifyingExistingAdmin()
    {
        var target = new User
        {
            Id = AdminTargetId,
            Email = "admin@cortex.com",
            Role = Auth0Roles.Admin,
            IsActive = true,
        };

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest { Role = Auth0Roles.User, IsActive = false },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturning(developerCaller: true),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UpdateUser_BlocksSelfRoleChange_EvenForAdminCaller()
    {
        var self = new User
        {
            Id = AdminCallerId,
            Email = "admin@cortex.com",
            Role = Auth0Roles.Admin,
            IsActive = true,
        };

        var result = await UserHandlers.UpdateUser(
            id: self.Id,
            request: new AdminUpdateUserRequest { Role = Auth0Roles.Developer, IsActive = true },
            repo: StrictRepoReturning(self),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturningUser(self),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UpdateUser_BlocksSelfActiveChange()
    {
        var self = new User
        {
            Id = AdminCallerId,
            Email = "admin@cortex.com",
            Role = Auth0Roles.Admin,
            IsActive = true,
        };

        var result = await UserHandlers.UpdateUser(
            id: self.Id,
            request: new AdminUpdateUserRequest
            {
                // Same role is fine; the governance-field change is what matters.
                Role = self.Role,
                IsActive = false,
            },
            repo: StrictRepoReturning(self),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturningUser(self),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksSelfTarget()
    {
        var self = new User
        {
            Id = DeveloperCallerId,
            Email = "dev@cortex.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            Auth0Id = "auth0|dev",
        };

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Developer },
            repo: StrictRepoReturning(self),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturningUser(self),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksNonAdminAddingAdmin()
    {
        var target = new User
        {
            Id = OtherUserId,
            Email = "target@cortex.com",
            Role = Auth0Roles.User,
            IsActive = true,
            Auth0Id = "auth0|target",
        };

        var result = await UserHandlers.MutateUserAuth0Role(
            id: target.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Admin },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturning(developerCaller: true),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksNonAdminRemovingAdmin()
    {
        var target = new User
        {
            Id = AdminTargetId,
            Email = "admin@cortex.com",
            Role = Auth0Roles.Admin,
            IsActive = true,
            Auth0Id = "auth0|admin-target",
        };

        var result = await UserHandlers.MutateUserAuth0Role(
            id: target.Id,
            request: new UserRoleMutationRequest { Action = "remove", RoleName = Auth0Roles.Admin },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: UserContextReturning(developerCaller: true),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    private static IUserRepository StrictRepoReturning(User user)
    {
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return repo.Object;
    }

    private static T StrictMock<T>() where T : class => new Mock<T>(MockBehavior.Strict).Object;

    private static IUserContextService UserContextReturning(bool developerCaller)
    {
        var caller = new User
        {
            Id = developerCaller ? DeveloperCallerId : AdminCallerId,
            Email = developerCaller ? "dev@cortex.com" : "admin@cortex.com",
            Role = developerCaller ? Auth0Roles.Developer : Auth0Roles.Admin,
            IsActive = true,
        };
        return UserContextReturningUser(caller);
    }

    private static IUserContextService UserContextReturningUser(User caller)
    {
        var mock = new Mock<IUserContextService>(MockBehavior.Strict);
        mock.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(caller);
        return mock.Object;
    }

    private static IHttpContextAccessor AuthedHttpContextAccessor(bool isAdmin, string sub)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, sub),
                new Claim("sub", sub),
                new Claim(ClaimTypes.Role, isAdmin ? Auth0Roles.Admin : Auth0Roles.Developer),
            ],
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        accessor.Setup(a => a.HttpContext).Returns(context);
        return accessor.Object;
    }
}
