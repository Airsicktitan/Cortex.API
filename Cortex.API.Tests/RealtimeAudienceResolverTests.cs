using Cortex.API.Data;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class RealtimeAudienceResolverTests
{
    [Fact]
    public async Task GetAudienceUserIdsAsync_IncludesAssignedUserAndGuestOwners_AlongsideGlobalRoles()
    {
        var users = new[]
        {
            new User
            {
                Id = 1,
                Email = "admin@test.com",
                DisplayName = "Admin",
                Role = Auth0Roles.Admin,
            },
            new User
            {
                Id = 2,
                Email = "developer@test.com",
                DisplayName = "Developer",
                Role = Auth0Roles.Developer,
            },
            new User
            {
                Id = 3,
                Email = "manager@test.com",
                DisplayName = "Manager",
                Role = Auth0Roles.BusinessManager,
            },
            new User
            {
                Id = 4,
                Email = "user.owner@test.com",
                DisplayName = "Assigned User",
                Role = Auth0Roles.User,
            },
            new User
            {
                Id = 5,
                Email = "guest.owner@test.com",
                DisplayName = "Assigned Guest",
                Role = Auth0Roles.Guest,
            },
            new User
            {
                Id = 6,
                Email = "creator@test.com",
                DisplayName = "Creator",
                Role = Auth0Roles.User,
            },
            new User
            {
                Id = 7,
                Email = "outsider@test.com",
                DisplayName = "Outsider",
                Role = Auth0Roles.User,
            },
        };

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync(users);

        var resolver = new RealtimeAudienceResolver(userRepository.Object);

        var audience = await resolver.GetAudienceUserIdsAsync(
            createdBy: 6,
            synitiOwner: "Assigned User",
            businessOwner: "guest.owner@test.com");

        Assert.Collection(
            audience.OrderBy(id => id),
            userId => Assert.Equal(1, userId),
            userId => Assert.Equal(2, userId),
            userId => Assert.Equal(3, userId),
            userId => Assert.Equal(4, userId),
            userId => Assert.Equal(5, userId),
            userId => Assert.Equal(6, userId));
        Assert.DoesNotContain(7, audience);
    }
}
