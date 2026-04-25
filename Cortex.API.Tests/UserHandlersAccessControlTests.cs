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
/// Regression guards for role-management authority checks. These tests keep the
/// backend focused on actor authority and target role instead of blanket self-edit
/// denial, while preserving the Developer -> Admin escalation block.
/// </summary>
public class UserHandlersAccessControlTests
{
    private const int DeveloperCallerId = 10;
    private const int AdminCallerId = 99;
    private const int OtherAdminId = 100;
    private const int OtherUserId = 20;
    private const int AdminTargetId = 30;
    private const string ReportingRole = "Reporting";
    private const string DashboardRole = "Dashboard";
    private const string BusinessOwnerRole = "Business Owner";
    private const string SynitiOwnerRole = "Syniti Owner";

    [Fact]
    public async Task UpdateUser_BlocksNonAdminPromotingOtherUserToAdmin()
    {
        var target = NewUser(OtherUserId, Auth0Roles.User, "target@cortex.com");

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
        var target = NewUser(AdminTargetId, Auth0Roles.Admin, "admin@cortex.com");

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
    public async Task UpdateUser_AllowsAdminSelfRoleChange_WhenAnotherAdminExists()
    {
        var self = NewUser(AdminCallerId, Auth0Roles.Admin, "admin@cortex.com");
        var otherAdmin = NewUser(OtherAdminId, Auth0Roles.Admin, "other-admin@cortex.com");
        var repo = StrictRepoReturningMock(self, [self, otherAdmin], expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.UpdateUser(
            id: self.Id,
            request: new AdminUpdateUserRequest { Role = Auth0Roles.Developer, IsActive = true },
            repo: repo.Object,
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: roleSync.Object,
            userContext: UserContextReturningUser(self),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Developer, self.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUser_BlocksSelfActiveChange()
    {
        var self = NewUser(AdminCallerId, Auth0Roles.Admin, "admin@cortex.com");

        var result = await UserHandlers.UpdateUser(
            id: self.Id,
            request: new AdminUpdateUserRequest
            {
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
    public async Task MutateUserAuth0Role_AllowsAdminAddingRoleToSelf()
    {
        var self = NewUser(AdminCallerId, Auth0Roles.Admin, "admin@cortex.com", "auth0|admin");
        var developerRole = Auth0Role(Auth0Roles.Developer);
        var auth0Management = Auth0MutationMock(
            self,
            action: "add",
            role: developerRole,
            currentRoles: [Auth0Role(Auth0Roles.Admin)],
            freshRoles: [Auth0Role(Auth0Roles.Admin), developerRole]);
        var repo = StrictRepoReturningMock(self, expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Developer },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_AllowsAdminRemovingNonCriticalRoleFromSelf()
    {
        var self = NewUser(AdminCallerId, Auth0Roles.Admin, "admin@cortex.com", "auth0|admin");
        var developerRole = Auth0Role(Auth0Roles.Developer);
        var auth0Management = Auth0MutationMock(
            self,
            action: "remove",
            role: developerRole,
            currentRoles: [Auth0Role(Auth0Roles.Admin), developerRole],
            freshRoles: [Auth0Role(Auth0Roles.Admin)]);
        var repo = StrictRepoReturningMock(self, expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "remove", RoleName = Auth0Roles.Developer },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Admin, self.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_AllowsAdminAssigningAdminToAnotherUser()
    {
        var target = NewUser(OtherUserId, Auth0Roles.User, "target@cortex.com", "auth0|target");
        var adminRole = Auth0Role(Auth0Roles.Admin);
        var auth0Management = Auth0MutationMock(
            target,
            action: "add",
            role: adminRole,
            currentRoles: [Auth0Role(Auth0Roles.User)],
            freshRoles: [adminRole, Auth0Role(Auth0Roles.User)]);
        var repo = StrictRepoReturningMock(target, expectSave: true);
        var roleSync = StrictRoleSyncFor(target);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: target.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Admin },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Admin, target.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(target, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksDeveloperAssigningAdminToSelf()
    {
        var self = NewUser(DeveloperCallerId, Auth0Roles.Developer, "dev@cortex.com", "auth0|dev");

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Admin },
            repo: StrictRepoReturning(self),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_AllowsDeveloperAddingLowerRoleToSelf()
    {
        var self = NewUser(DeveloperCallerId, Auth0Roles.Developer, "dev@cortex.com", "auth0|dev");
        var businessManagerRole = Auth0Role(Auth0Roles.BusinessManager);
        var auth0Management = Auth0MutationMock(
            self,
            action: "add",
            role: businessManagerRole,
            currentRoles: [Auth0Role(Auth0Roles.Developer)],
            freshRoles: [Auth0Role(Auth0Roles.Developer), businessManagerRole]);
        var repo = StrictRepoReturningMock(self, expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.BusinessManager },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Developer, self.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_AllowsDeveloperAddingFutureCapabilityRoleToSelf()
    {
        var self = NewUser(DeveloperCallerId, Auth0Roles.Developer, "dev@cortex.com", "auth0|dev");
        var reportingRole = Auth0Role(ReportingRole);
        var auth0Management = Auth0MutationMock(
            self,
            action: "add",
            role: reportingRole,
            currentRoles: [Auth0Role(Auth0Roles.Developer)],
            freshRoles: [Auth0Role(Auth0Roles.Developer), reportingRole]);
        var repo = StrictRepoReturningMock(self, expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = ReportingRole },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Developer, self.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_AllowsDeveloperRemovingFutureCapabilityRoleFromSelf()
    {
        var self = NewUser(DeveloperCallerId, Auth0Roles.Developer, "dev@cortex.com", "auth0|dev");
        var dashboardRole = Auth0Role(DashboardRole);
        var auth0Management = Auth0MutationMock(
            self,
            action: "remove",
            role: dashboardRole,
            currentRoles: [Auth0Role(Auth0Roles.Developer), dashboardRole],
            freshRoles: [Auth0Role(Auth0Roles.Developer)]);
        var repo = StrictRepoReturningMock(self, expectSave: true);
        var roleSync = StrictRoleSyncFor(self);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "remove", RoleName = DashboardRole },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: roleSync.Object,
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);
        Assert.Equal(Auth0Roles.Developer, self.Role);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        roleSync.Verify(s => s.SyncRoleToAuth0Async(self, It.IsAny<CancellationToken>()), Times.Once);
        auth0Management.VerifyAll();
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksNonAdminAddingAdmin()
    {
        var target = NewUser(OtherUserId, Auth0Roles.User, "target@cortex.com", "auth0|target");

        var result = await UserHandlers.MutateUserAuth0Role(
            id: target.Id,
            request: new UserRoleMutationRequest { Action = "add", RoleName = Auth0Roles.Admin },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksNonAdminRemovingAdmin()
    {
        var target = NewUser(AdminTargetId, Auth0Roles.Admin, "admin@cortex.com", "auth0|admin-target");

        var result = await UserHandlers.MutateUserAuth0Role(
            id: target.Id,
            request: new UserRoleMutationRequest { Action = "remove", RoleName = Auth0Roles.Admin },
            repo: StrictRepoReturning(target),
            auth0Management: StrictMock<IAuth0ManagementService>(),
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: false, sub: "auth0|dev"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MutateUserAuth0Role_BlocksRemovingLastAdmin()
    {
        var self = NewUser(AdminCallerId, Auth0Roles.Admin, "admin@cortex.com", "auth0|admin");
        var adminRole = Auth0Role(Auth0Roles.Admin);
        var repo = StrictRepoReturningMock(self, [self]);
        var auth0Management = Auth0RoleLookupMock(self, [adminRole]);

        var result = await UserHandlers.MutateUserAuth0Role(
            id: self.Id,
            request: new UserRoleMutationRequest { Action = "remove", RoleName = Auth0Roles.Admin },
            repo: repo.Object,
            auth0Management: auth0Management.Object,
            roleSync: StrictMock<IAuth0UserRoleSyncService>(),
            userContext: StrictMock<IUserContextService>(),
            httpContextAccessor: AuthedHttpContextAccessor(isAdmin: true, sub: "auth0|admin"),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status400BadRequest);
        repo.Verify(r => r.SaveChangesAsync(), Times.Never);
        auth0Management.VerifyAll();
    }

    private static User NewUser(
        int id,
        string role,
        string email,
        string? auth0Id = null) =>
        new()
        {
            Id = id,
            DisplayName = email,
            Email = email,
            Role = role,
            IsActive = true,
            Auth0Id = auth0Id,
        };

    private static IUserRepository StrictRepoReturning(User user) =>
        StrictRepoReturningMock(user).Object;

    private static Mock<IUserRepository> StrictRepoReturningMock(
        User user,
        IEnumerable<User>? allUsers = null,
        bool expectSave = false)
    {
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        if (allUsers is not null)
        {
            repo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(allUsers);
        }

        if (expectSave)
        {
            repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        }

        return repo;
    }

    private static Mock<IAuth0ManagementService> Auth0RoleLookupMock(
        User user,
        IReadOnlyList<Auth0RoleDto> currentRoles)
    {
        var mock = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        mock.Setup(m => m.GetAllRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllAuth0Roles());
        mock.Setup(m => m.GetUserRolesAsync(user.Auth0Id!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentRoles);
        return mock;
    }

    private static Mock<IAuth0ManagementService> Auth0MutationMock(
        User user,
        string action,
        Auth0RoleDto role,
        IReadOnlyList<Auth0RoleDto> currentRoles,
        IReadOnlyList<Auth0RoleDto> freshRoles)
    {
        var mock = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        mock.Setup(m => m.GetAllRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllAuth0Roles());
        mock.SetupSequence(m => m.GetUserRolesAsync(user.Auth0Id!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentRoles)
            .ReturnsAsync(freshRoles);

        if (action == "add")
        {
            mock.Setup(m => m.AssignRolesToUserAsync(
                    user.Auth0Id!,
                    It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { role.Id })),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }
        else
        {
            mock.Setup(m => m.RemoveRolesFromUserAsync(
                    user.Auth0Id!,
                    It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { role.Id })),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        return mock;
    }

    private static IReadOnlyList<Auth0RoleDto> AllAuth0Roles() =>
    [
        Auth0Role(Auth0Roles.Admin),
        Auth0Role(Auth0Roles.Developer),
        Auth0Role(Auth0Roles.BusinessManager),
        Auth0Role(Auth0Roles.User),
        Auth0Role(Auth0Roles.Guest),
        Auth0Role(ReportingRole),
        Auth0Role(DashboardRole),
        Auth0Role(BusinessOwnerRole),
        Auth0Role(SynitiOwnerRole),
    ];

    private static Auth0RoleDto Auth0Role(string name) =>
        new()
        {
            Id = name switch
            {
                Auth0Roles.Admin => "role-admin",
                Auth0Roles.Developer => "role-developer",
                Auth0Roles.BusinessManager => "role-business-manager",
                Auth0Roles.User => "role-user",
                Auth0Roles.Guest => "role-guest",
                _ => $"role-{name.ToLowerInvariant().Replace(' ', '-')}",
            },
            Name = name,
        };

    private static Mock<IAuth0UserRoleSyncService> StrictRoleSyncFor(User user)
    {
        var mock = new Mock<IAuth0UserRoleSyncService>(MockBehavior.Strict);
        mock.Setup(s => s.SyncRoleToAuth0Async(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static T StrictMock<T>() where T : class => new Mock<T>(MockBehavior.Strict).Object;

    private static IUserContextService UserContextReturning(bool developerCaller)
    {
        var caller = NewUser(
            developerCaller ? DeveloperCallerId : AdminCallerId,
            developerCaller ? Auth0Roles.Developer : Auth0Roles.Admin,
            developerCaller ? "dev@cortex.com" : "admin@cortex.com");
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
