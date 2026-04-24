using Cortex.API.Data;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class TicketOwnerAssignmentValidationTests
{
    [Fact]
    public async Task NormalizeAndValidateAsync_DeveloperAndBusinessUser_ReturnsCanonicalUserKeys()
    {
        var dev = new User
        {
            Id = 42,
            DisplayName = "Adam Hooper",
            Email = "adam@example.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true,
            IsBusinessOwnerEligible = false,
        };
        var business = new User
        {
            Id = 7,
            DisplayName = "Biz Owner",
            Email = "biz@example.com",
            Role = Auth0Roles.User,
            IsActive = true,
            IsSynitiOwnerEligible = false,
            IsBusinessOwnerEligible = true,
        };
        var repository = CreateRepository(dev, business);

        var owners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
            repository.Object,
            "Adam Hooper",
            "biz@example.com");

        Assert.Equal("user:42", owners.SynitiOwner);
        Assert.Equal("user:7", owners.BusinessOwner);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_CanonicalToken_ReturnsCanonicalUserKey()
    {
        var repository = CreateRepository(
            new User
            {
                Id = 42,
                DisplayName = "Adam Hooper",
                Email = "adam@example.com",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = false,
            });

        var owners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
            repository.Object,
            "user:42",
            null);

        Assert.Equal("user:42", owners.SynitiOwner);
        Assert.Null(owners.BusinessOwner);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_UnresolvedOwner_Throws()
    {
        var repository = CreateRepository(
            new User
            {
                Id = 42,
                DisplayName = "Adam Hooper",
                Email = "adam@example.com",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = false,
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                repository.Object,
                "Unknown Owner",
                null));

        Assert.Equal("Syniti owner must reference a user from the directory.", ex.Message);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_NonDeveloperSyniti_Throws()
    {
        var repository = CreateRepository(
            new User
            {
                Id = 2,
                DisplayName = "Regular",
                Email = "reg@example.com",
                Role = Auth0Roles.User,
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true,
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                repository.Object,
                "user:2",
                null));

        Assert.Equal("Syniti Owner must be a Developer.", ex.Message);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_DeveloperAsBusiness_Throws()
    {
        var dev = new User
        {
            Id = 2,
            DisplayName = "Dev",
            Email = "dev@example.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true,
            IsBusinessOwnerEligible = true,
        };
        var repository = CreateRepository(dev);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                repository.Object,
                null,
                "user:2"));

        Assert.Equal("Business Owner cannot be a Developer.", ex.Message);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_GuestAsBusiness_Throws()
    {
        var guest = new User
        {
            Id = 3,
            DisplayName = "Guest User",
            Email = "guest@example.com",
            Role = Auth0Roles.Guest,
            IsActive = true,
            IsSynitiOwnerEligible = false,
            IsBusinessOwnerEligible = true,
        };
        var repository = CreateRepository(guest);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                repository.Object,
                null,
                "user:3"));

        Assert.Equal("Business Owner cannot be a Guest.", ex.Message);
    }

    private static Mock<IUserRepository> CreateRepository(params User[] users)
    {
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetAllUsersAsync())
            .ReturnsAsync(users);
        return repository;
    }
}
