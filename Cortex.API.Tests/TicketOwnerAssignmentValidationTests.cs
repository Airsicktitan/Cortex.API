using Cortex.API.Data;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class TicketOwnerAssignmentValidationTests
{
    [Fact]
    public async Task NormalizeAndValidateAsync_DisplayNameAndEmail_ReturnsCanonicalUserKeys()
    {
        var repository = CreateRepository(
            new User
            {
                Id = 42,
                DisplayName = "Adam Hooper",
                Email = "adamcwhooper@yahoo.com",
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true,
            });

        var owners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
            repository.Object,
            "Adam Hooper",
            "adamcwhooper@yahoo.com");

        Assert.Equal("user:42", owners.SynitiOwner);
        Assert.Equal("user:42", owners.BusinessOwner);
    }

    [Fact]
    public async Task NormalizeAndValidateAsync_CanonicalToken_ReturnsCanonicalUserKey()
    {
        var repository = CreateRepository(
            new User
            {
                Id = 42,
                DisplayName = "Adam Hooper",
                Email = "adamcwhooper@yahoo.com",
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true,
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
                Email = "adamcwhooper@yahoo.com",
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true,
            });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                repository.Object,
                "Unknown Owner",
                null));

        Assert.Equal("Syniti owner must reference a user from the directory.", ex.Message);
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
