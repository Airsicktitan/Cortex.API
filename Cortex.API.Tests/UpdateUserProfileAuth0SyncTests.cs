using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class UpdateUserProfileAuth0SyncTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task UpdateUserProfile_DisabledSync_Returns_NotConfigured()
    {
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = false,
        });
        var user = SeedUser(displayNameBeforeSave: "Local");
        var userContext = UserContextMutatingStub(user);

        using var strictAuth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);

        var http = new DefaultHttpContext
        {
            TraceIdentifier = "trace-not-configured",
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
        };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(http);

        var result = await UserHandlers.UpdateUserProfile(
            new UpdateUserProfileRequest { DisplayName = "Local" },
            userContext.Object,
            strictAuth0.Object,
            management,
            accessorMock.Object,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var (statusCode, dto) = await ReadUpdateProfileEnvelopeAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.NotNull(dto);
        Assert.Equal(Auth0ProfileSyncStatus.NotConfigured, dto!.Auth0ProfileSyncStatus);

        strictAuth0.Verify(
            service => service.PatchUserRootProfileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserProfile_EnableSync_Skipped_NoNameFields_NoPatch()
    {
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "tenant.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });
        var user = SeedUser(displayNameBeforeSave: "Keep", nickname: null, auth0Id: "auth0|x");
        var userContext = UserContextMutatingStub(user);

        using var strictAuth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);

        var http = new DefaultHttpContext
        {
            TraceIdentifier = "trace-skip-phone",
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
        };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(http);

        var result = await UserHandlers.UpdateUserProfile(
            new UpdateUserProfileRequest { PhoneNumber = "503-555-0100" },
            userContext.Object,
            strictAuth0.Object,
            management,
            accessorMock.Object,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var (statusCode, dto) = await ReadUpdateProfileEnvelopeAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(Auth0ProfileSyncStatus.Skipped, dto!.Auth0ProfileSyncStatus);

        strictAuth0.Verify(
            service => service.PatchUserRootProfileAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserProfile_EnableSync_Synced_CallsManagementApi()
    {
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "tenant.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var user = SeedUser(displayNameBeforeSave: "Was", nickname: null, auth0Id: "auth0|sync");
        var userContext = UserContextMutatingStub(user);

        var auth0Mock = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0Mock
            .Setup(service => service.PatchUserRootProfileAsync(
                user.Auth0Id!,
                "Next",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var http = new DefaultHttpContext
        {
            TraceIdentifier = "trace-synced",
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
        };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(http);

        var result = await UserHandlers.UpdateUserProfile(
            new UpdateUserProfileRequest { DisplayName = "Next" },
            userContext.Object,
            auth0Mock.Object,
            management,
            accessorMock.Object,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var (statusCode, dto) = await ReadUpdateProfileEnvelopeAsync(result);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
        Assert.Equal(Auth0ProfileSyncStatus.Synced, dto!.Auth0ProfileSyncStatus);
        auth0Mock.VerifyAll();
    }

    [Fact]
    public async Task UpdateUserProfile_EnableSync_ManagementFails_Returns_StatusFailed_WithoutLeak()
    {
        var management = Options.Create(new Auth0ManagementOptions
        {
            EnableProfileWriteBack = true,
            Domain = "tenant.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var user = SeedUser(displayNameBeforeSave: "Was", nickname: null, auth0Id: "auth0|fail");
        var userContext = UserContextMutatingStub(user);

        var auth0Mock = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0Mock
            .Setup(service => service.PatchUserRootProfileAsync(
                user.Auth0Id!,
                "Bad",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("upstream-leak-detail", StatusCodes.Status400BadRequest));

        var http = new DefaultHttpContext
        {
            TraceIdentifier = "trace-safe",
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider(),
        };
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(http);

        var result = await UserHandlers.UpdateUserProfile(
            new UpdateUserProfileRequest { DisplayName = "Bad" },
            userContext.Object,
            auth0Mock.Object,
            management,
            accessorMock.Object,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var (_, dto) = await ReadUpdateProfileEnvelopeAsync(result);

        Assert.Equal(Auth0ProfileSyncStatus.Failed, dto!.Auth0ProfileSyncStatus);
        Assert.Contains("Saved in Cortex", dto.Auth0ProfileSyncMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upstream-leak-detail", dto.Auth0ProfileSyncMessage, StringComparison.Ordinal);
    }

    private static User SeedUser(
        string displayNameBeforeSave,
        string? nickname = null,
        string? auth0Id = "auth0|seed") =>
        new()
        {
            Id = 42,
            DisplayName = displayNameBeforeSave,
            NickName = nickname,
            Email = "user@example.com",
            Role = Auth0Roles.User,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            Auth0Id = auth0Id,
        };

    private static Mock<IUserContextService> UserContextMutatingStub(User user)
    {
        var mock = new Mock<IUserContextService>(MockBehavior.Strict);
        mock.Setup(c => c.GetCurrentUserAsync()).ReturnsAsync(user);
        mock
            .Setup(c => c.UpdateProfileAsync(user, It.IsAny<UpdateUserProfileRequest>()))
            .Callback<User, UpdateUserProfileRequest>((u, request) =>
            {
                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    u.DisplayName = request.DisplayName.Trim();
                }

                u.NickName = string.IsNullOrWhiteSpace(request.NickName)
                    ? null
                    : request.NickName.Trim();

                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    u.PhoneNumber = request.PhoneNumber.Trim();
                }
            })
            .ReturnsAsync(user);

        return mock;
    }

    private static async Task<(int StatusCode, UpdateUserProfileResponse? Body)> ReadUpdateProfileEnvelopeAsync(
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

        var dto = JsonSerializer.Deserialize<UpdateUserProfileResponse>(payload, JsonOptions);
        return (context.Response.StatusCode, dto);
    }
}
