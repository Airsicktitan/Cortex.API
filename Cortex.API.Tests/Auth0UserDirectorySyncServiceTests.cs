using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

/// <summary>
/// Guards the v1 access-approval posture: Auth0 directory sync may discover and
/// link users, but must never grant access. Creating a new local row or flipping
/// an inactive row to active is a policy change and must go through an explicit
/// admin action — not a background sync.
/// </summary>
public class Auth0UserDirectorySyncServiceTests
{
    [Fact]
    public async Task Sync_CreatesNewUserAsInactive_EvenWhenRemoteNotBlocked()
    {
        var remote = new Auth0DirectoryUserDto
        {
            UserId = "auth0|new-user",
            Email = "new.user@acme.com",
            Name = "New User",
            Blocked = false,
        };

        var fake = new FakeUserRepository();
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0.Setup(a => a.GetAllDirectoryUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Auth0DirectoryUserDto> { remote });
        auth0.Setup(a => a.GetUserRolesAsync(remote.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Auth0RoleDto>());

        var sut = new Auth0UserDirectorySyncService(fake, auth0.Object);

        var response = await sut.SyncFromAuth0Async();

        Assert.Equal(1, response.Created);
        var created = Assert.Single(fake.Users);
        Assert.False(created.IsActive);
        Assert.Equal("auth0|new-user", created.Auth0Id);
        Assert.Equal("new.user@acme.com", created.Email);
    }

    [Fact]
    public async Task Sync_DoesNotActivateInactiveLocalUser_WhenRemoteNotBlocked()
    {
        var existing = new User
        {
            Id = 1,
            Email = "pending@acme.com",
            Auth0Id = "auth0|pending",
            IsActive = false, // pending admin approval
        };
        var remote = new Auth0DirectoryUserDto
        {
            UserId = "auth0|pending",
            Email = "pending@acme.com",
            Name = "Pending User",
            Blocked = false,
        };

        var fake = new FakeUserRepository(existing);
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0.Setup(a => a.GetAllDirectoryUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Auth0DirectoryUserDto> { remote });

        var sut = new Auth0UserDirectorySyncService(fake, auth0.Object);

        await sut.SyncFromAuth0Async();

        Assert.False(fake.Users.Single().IsActive);
    }

    [Fact]
    public async Task Sync_DeactivatesActiveLocalUser_WhenRemoteBlocked()
    {
        var existing = new User
        {
            Id = 1,
            Email = "approved@acme.com",
            Auth0Id = "auth0|approved",
            IsActive = true,
        };
        var remote = new Auth0DirectoryUserDto
        {
            UserId = "auth0|approved",
            Email = "approved@acme.com",
            Name = "Approved User",
            Blocked = true,
        };

        var fake = new FakeUserRepository(existing);
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0.Setup(a => a.GetAllDirectoryUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Auth0DirectoryUserDto> { remote });

        var sut = new Auth0UserDirectorySyncService(fake, auth0.Object);

        await sut.SyncFromAuth0Async();

        Assert.False(fake.Users.Single().IsActive);
    }

    [Fact]
    public async Task Sync_LinkByEmail_DoesNotActivateInactiveLocalUser()
    {
        // Local row exists by email but has no Auth0Id yet — the link-by-email path.
        var existing = new User
        {
            Id = 1,
            Email = "pending@acme.com",
            Auth0Id = null,
            IsActive = false,
        };
        var remote = new Auth0DirectoryUserDto
        {
            UserId = "auth0|pending",
            Email = "pending@acme.com",
            Name = "Pending User",
            Blocked = false,
        };

        var fake = new FakeUserRepository(existing);
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0.Setup(a => a.GetAllDirectoryUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Auth0DirectoryUserDto> { remote });

        var sut = new Auth0UserDirectorySyncService(fake, auth0.Object);

        var response = await sut.SyncFromAuth0Async();

        Assert.Equal(1, response.LinkedByEmail);
        var linked = fake.Users.Single();
        Assert.Equal("auth0|pending", linked.Auth0Id);
        Assert.False(linked.IsActive);
    }

    /// <summary>
    /// Minimal in-memory IUserRepository. Only the members exercised by sync are
    /// implemented; everything else throws so an unintended call is loud.
    /// </summary>
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; }

        public FakeUserRepository(params User[] seed)
        {
            Users = new List<User>(seed);
        }

        public Task<User?> GetByAuth0IdAsync(string auth0Id) =>
            Task.FromResult(Users.FirstOrDefault(u =>
                string.Equals(u.Auth0Id, auth0Id, StringComparison.Ordinal)));

        public Task<User?> GetByEmailAsync(string email) =>
            Task.FromResult(Users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<User?> GetByIdAsync(int id) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User> CreateUserAsync(User user)
        {
            user.Id = Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;
            Users.Add(user);
            return Task.FromResult(user);
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<IEnumerable<User>> GetAllUsersAsync() =>
            throw new NotImplementedException();
        public Task<IEnumerable<User>> GetOnlineUsersAsync(DateTime cutoffUtc, DateTime utcNow) =>
            throw new NotImplementedException();
        public Task UpdateUserAsync(User user) => throw new NotImplementedException();
    }
}
