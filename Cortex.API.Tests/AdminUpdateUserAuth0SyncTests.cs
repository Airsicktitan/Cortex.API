using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Cortex.API.Tests;

public sealed class AdminUpdateUserAuth0SyncTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private const int AdminCallerId = 901;
    private const int TargetUserId = 902;

    [Fact]
    public async Task AdminUpdateUser_DisabledSync_SavesNickName_Returns_NotConfigured()
    {
        var target = SeedTargetUser(nickName: "Before", auth0Id: "auth0|t1");
        var management = Options.Create(new Auth0ManagementOptions { EnableProfileWriteBack = false });
        var auth0 = StrictAuth0ForRoleFetch(target);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: BaseRequest("After"),
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.NotNull(dto);
        Assert.Equal("After", dto!.User.NickName);
        Assert.Equal(Auth0ProfileSyncStatus.NotConfigured, dto.Auth0ProfileSyncStatus);
        VerifyNoProfilePatch(auth0);
    }

    [Fact]
    public async Task AdminUpdateUser_EnabledSync_NoNickChange_Returns_Skipped()
    {
        var target = SeedTargetUser(nickName: "Same", auth0Id: "auth0|t2");
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });
        var auth0 = StrictAuth0ForRoleFetch(target);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: BaseRequest("Same"),
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto!.Auth0ProfileSyncStatus);
        Assert.Contains(
            "unchanged",
            dto.Auth0ProfileSyncMessage ?? "",
            StringComparison.OrdinalIgnoreCase);
        VerifyNoProfilePatch(auth0);
    }

    [Fact]
    public async Task AdminUpdateUser_Enabled_NoAuth0Id_Returns_Skipped_WhenNickChanges()
    {
        var target = SeedTargetUser(nickName: "A", auth0Id: null);
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: BaseRequest("B"),
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto!.Auth0ProfileSyncStatus);
        VerifyNoProfilePatch(auth0);
    }

    [Fact]
    public async Task AdminUpdateUser_Enabled_NickChanged_CallsPatch_Returns_Synced()
    {
        var target = SeedTargetUser(nickName: "Was", auth0Id: "auth0|sync");
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|sync",
                false,
                It.IsAny<string?>(),
                true,
                "WillBe",
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: BaseRequest("WillBe"),
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal(Auth0ProfileSyncStatus.Synced, dto!.Auth0ProfileSyncStatus);
        auth0.Verify(
            a => a.PatchUserRootProfileAsync(
                "auth0|sync",
                false,
                It.IsAny<string?>(),
                true,
                "WillBe",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUser_PatchFailure_Returns_Failed_WithoutLeak()
    {
        var target = SeedTargetUser(nickName: "Old", auth0Id: "auth0|bad");
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|bad",
                false,
                It.IsAny<string?>(),
                true,
                "New",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("secret-upstream-body", StatusCodes.Status400BadRequest));

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: BaseRequest("New"),
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal(Auth0ProfileSyncStatus.Failed, dto!.Auth0ProfileSyncStatus);
        Assert.DoesNotContain("secret-upstream-body", dto.Auth0ProfileSyncMessage, StringComparison.Ordinal);
        Assert.Equal("New", dto.User.NickName);
    }

    [Fact]
    public async Task AdminUpdateUser_DisplayNameChanged_CallsPatch_WithAuth0Subject_NotLocalId()
    {
        var target = SeedTargetUser(nickName: "KeepNick", auth0Id: "auth0|display-patch");
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|display-patch",
                true,
                "Legal Name",
                false,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                DisplayName = "Legal Name",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal(Auth0ProfileSyncStatus.Synced, dto!.Auth0ProfileSyncStatus);
        Assert.Equal("KeepNick", dto.User.NickName);
        Assert.Equal("Legal Name", dto.User.DisplayName);
        auth0.Verify(
            a => a.PatchUserRootProfileAsync(
                "auth0|display-patch",
                true,
                It.IsAny<string?>(),
                false,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUser_DisplayNameOnly_DoesNotClearNickName()
    {
        var target = SeedTargetUser(nickName: "Preserved", auth0Id: "auth0|partial");
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|partial",
                true,
                "New Display",
                false,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                DisplayName = "New Display",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal("Preserved", dto!.User.NickName);
    }

    [Fact]
    public async Task AdminUpdateUser_BlankNickName_Clears_TriggersPatch()
    {
        var target = SeedTargetUser(nickName: "adam", auth0Id: "auth0|blank-nick");
        var management = EnabledManagementOptions();
        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|blank-nick",
                false,
                It.IsAny<string?>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                NickName = "",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Null(dto!.User.NickName);
        Assert.Equal(Auth0ProfileSyncStatus.Synced, dto.Auth0ProfileSyncStatus);
        auth0.Verify(
            a => a.PatchUserRootProfileAsync(
                "auth0|blank-nick",
                false,
                It.IsAny<string?>(),
                true,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUser_WhitespaceNickName_Clears_TriggersPatch()
    {
        var target = SeedTargetUser(nickName: "adam", auth0Id: "auth0|ws-nick");
        var management = EnabledManagementOptions();
        var auth0 = StrictAuth0ForRoleFetch(target);
        auth0
            .Setup(a => a.PatchUserRootProfileAsync(
                "auth0|ws-nick",
                false,
                It.IsAny<string?>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                NickName = " \t ",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Null(dto!.User.NickName);
        Assert.Equal(Auth0ProfileSyncStatus.Synced, dto.Auth0ProfileSyncStatus);
        auth0.Verify(
            a => a.PatchUserRootProfileAsync(
                "auth0|ws-nick",
                false,
                It.IsAny<string?>(),
                true,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminUpdateUser_OmittedNickName_LeavesNickname_NoPatch()
    {
        var target = SeedTargetUser(nickName: "keep-me", auth0Id: "auth0|omit");
        var management = EnabledManagementOptions();
        var auth0 = StrictAuth0ForRoleFetch(target);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal("keep-me", dto!.User.NickName);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto.Auth0ProfileSyncStatus);
        VerifyNoProfilePatch(auth0);
    }

    [Fact]
    public async Task AdminUpdateUser_BlankDisplayName_DoesNotClear_NoPatch()
    {
        var target = SeedTargetUser(
            nickName: "nick",
            auth0Id: "auth0|blank-disp",
            displayName: "Adam Hooper");
        var management = EnabledManagementOptions();
        var auth0 = StrictAuth0ForRoleFetch(target);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                DisplayName = "",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal("Adam Hooper", dto!.User.DisplayName);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto.Auth0ProfileSyncStatus);
        VerifyNoProfilePatch(auth0);
    }

    [Fact]
    public async Task AdminUpdateUser_WhitespaceDisplayName_DoesNotClear_NoPatch()
    {
        var target = SeedTargetUser(
            nickName: "nick",
            auth0Id: "auth0|ws-disp",
            displayName: "Adam Hooper");
        var management = EnabledManagementOptions();
        var auth0 = StrictAuth0ForRoleFetch(target);

        var result = await UserHandlers.UpdateUser(
            id: target.Id,
            request: new AdminUpdateUserRequest
            {
                DisplayName = "   ",
                IsActive = true,
                IsSynitiOwnerEligible = false,
                IsBusinessOwnerEligible = false,
            },
            repo: StrictRepo(target),
            auth0Management: auth0.Object,
            roleSync: LooseRoleSync(),
            userContext: UserContextForAdmin(),
            httpContextAccessor: AdminHttpAccessor(),
            loggerFactory: NullLoggerFactory.Instance,
            managementOptionsAccessor: management,
            cancellationToken: CancellationToken.None);

        var (_, dto) = await ReadAdminUpdateEnvelopeAsync(result);
        Assert.Equal("Adam Hooper", dto!.User.DisplayName);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto.Auth0ProfileSyncStatus);
        VerifyNoProfilePatch(auth0);
    }

    private static void VerifyNoProfilePatch(Mock<IAuth0ManagementService> auth0) =>
        auth0.Verify(
            a => a.PatchUserRootProfileAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private static IOptions<Auth0ManagementOptions> EnabledManagementOptions() =>
        Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "x.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

    private static User SeedTargetUser(
        string? nickName,
        string? auth0Id,
        string displayName = "Target Person") =>
        new()
        {
            Id = TargetUserId,
            DisplayName = displayName,
            NickName = nickName,
            Email = "target@example.com",
            Role = Auth0Roles.User,
            IsActive = true,
            IsSynitiOwnerEligible = false,
            IsBusinessOwnerEligible = false,
            CreatedDate = DateTime.UtcNow,
            Auth0Id = auth0Id,
        };

    private static AdminUpdateUserRequest BaseRequest(string nickName) =>
        new()
        {
            NickName = nickName,
            IsActive = true,
            IsSynitiOwnerEligible = false,
            IsBusinessOwnerEligible = false,
        };

    private static IUserRepository StrictRepo(User target)
    {
        var repo = new Mock<IUserRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(target.Id)).ReturnsAsync(target);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        return repo.Object;
    }

    private static Mock<IAuth0ManagementService> StrictAuth0ForRoleFetch(User target)
    {
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        if (!string.IsNullOrWhiteSpace(target.Auth0Id))
        {
            auth0
                .Setup(a => a.GetUserRolesAsync(target.Auth0Id!, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new Auth0RoleDto { Id = "role-user", Name = Auth0Roles.User },
                ]);
        }

        return auth0;
    }

    private static IAuth0UserRoleSyncService LooseRoleSync() =>
        Mock.Of<IAuth0UserRoleSyncService>();

    private static IUserContextService UserContextForAdmin()
    {
        var admin = new User
        {
            Id = AdminCallerId,
            DisplayName = "Admin",
            Email = "admin@example.com",
            Role = Auth0Roles.Admin,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
        };
        var mock = new Mock<IUserContextService>(MockBehavior.Strict);
        mock.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(admin);
        return mock.Object;
    }

    private static IHttpContextAccessor AdminHttpAccessor()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "auth0|admin"),
                new Claim("sub", "auth0|admin"),
                new Claim(ClaimTypes.Role, Auth0Roles.Admin),
            ],
            authenticationType: "Test");

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            TraceIdentifier = "trace-admin-update",
        };
        var acc = new Mock<IHttpContextAccessor>();
        acc.Setup(a => a.HttpContext).Returns(context);
        return acc.Object;
    }

    private static async Task<(int StatusCode, AdminUpdateUserResponse? Body)> ReadAdminUpdateEnvelopeAsync(
        IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();

        var dto = JsonSerializer.Deserialize<AdminUpdateUserResponse>(payload, JsonOptions);
        return (context.Response.StatusCode, dto);
    }
}
