using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Cortex.API.Tests;

public class ReassignmentExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidSuggestedTarget_UpdatesAssignment()
    {
        var recommendationService = new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        recommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse
            {
                ShouldSuggestReassignment = true,
                Reason = "test",
                AssignmentField = "synitiOwner",
                CurrentOwner = new ReassignmentOwnerSnapshotResponse
                {
                    UserId = 10,
                    OwnerKey = "John",
                    DisplayName = "John",
                    WorkloadScore = 27,
                    PressureLevel = "high",
                },
                SuggestedTargets =
                [
                    new ReassignmentTargetResponse
                    {
                        UserId = 11,
                        OwnerKey = "Adam",
                        DisplayName = "Adam",
                        WorkloadScore = 12,
                        PressureLevel = "moderate",
                        IsBetterThanCurrent = true,
                        ImprovementReason = "Lower workload",
                    },
                ],
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(11))
            .ReturnsAsync(new User
            {
                Id = 11,
                DisplayName = "Adam",
                Email = "adam@example.com",
                Department = "Syniti",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
            });
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync(
            [
                new User
                {
                    Id = 10,
                    DisplayName = "John",
                    Email = "john@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
                new User
                {
                    Id = 11,
                    DisplayName = "Adam",
                    Email = "adam@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
            ]);

        var riskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        riskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse
            {
                RiskLevel = "high",
            });
        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>
            {
                ["High"] = new() { Priority = "High", TargetHours = 8, WarningHours = 2 },
            });

        var service = new ReassignmentExecutionService(
            recommendationService.Object,
            userRepository.Object,
            riskService.Object,
            slaConfigurationService.Object);

        var ticket = CreateTicket("John");
        var result = await service.ExecuteAsync(
            ticket,
            new ReassignmentApplyRequest
            {
                SelectedOwnerId = 11,
                Source = "cortex_recommendation_review",
            },
            CreateActor());

        Assert.True(result.Succeeded);
        Assert.Equal("user:11", ticket.SynitiOwner);
        Assert.Equal("user:10", result.PreviousOwner);
        Assert.Equal("user:11", result.NewOwner);
        Assert.NotNull(result.DecisionImpactSnapshot);
        Assert.Equal("high", result.DecisionImpactSnapshot!.PreviousRiskLevel);
        Assert.Equal(27, result.DecisionImpactSnapshot.PreviousOwnerWorkload);
    }

    [Fact]
    public async Task ExecuteAsync_TargetNoLongerEligible_FailsSafely()
    {
        var recommendationService = new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        recommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse
            {
                ShouldSuggestReassignment = true,
                AssignmentField = "synitiOwner",
                SuggestedTargets = [],
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync(
            [
                new User
                {
                    Id = 10,
                    DisplayName = "John",
                    Email = "john@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
                new User
                {
                    Id = 11,
                    DisplayName = "Adam",
                    Email = "adam@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
            ]);

        var service = new ReassignmentExecutionService(
            recommendationService.Object,
            userRepository.Object,
            Mock.Of<IOperationalRiskService>(),
            Mock.Of<ISlaConfigurationService>());

        var result = await service.ExecuteAsync(
            CreateTicket("John"),
            new ReassignmentApplyRequest { SelectedOwnerId = 22 },
            CreateActor());

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Suggested target is no longer eligible for this ticket.", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_StaleOwnerContext_FailsConflict()
    {
        var recommendationService = new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        recommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse
            {
                ShouldSuggestReassignment = true,
                AssignmentField = "synitiOwner",
                SuggestedTargets =
                [
                    new ReassignmentTargetResponse
                    {
                        UserId = 11,
                        OwnerKey = "Adam",
                        DisplayName = "Adam",
                        WorkloadScore = 12,
                        PressureLevel = "moderate",
                        IsBetterThanCurrent = true,
                    },
                ],
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync(
            [
                new User
                {
                    Id = 10,
                    DisplayName = "John",
                    Email = "john@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
                new User
                {
                    Id = 11,
                    DisplayName = "Adam",
                    Email = "adam@example.com",
                    Department = "Syniti",
                    Role = Auth0Roles.Developer,
                    IsActive = true,
                    IsSynitiOwnerEligible = true,
                },
            ]);

        var service = new ReassignmentExecutionService(
            recommendationService.Object,
            userRepository.Object,
            Mock.Of<IOperationalRiskService>(),
            Mock.Of<ISlaConfigurationService>());

        var result = await service.ExecuteAsync(
            CreateTicket("John"),
            new ReassignmentApplyRequest
            {
                SelectedOwnerId = 11,
                ExpectedCurrentOwnerKey = "SomeoneElse",
            },
            CreateActor());

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_NoLongerSuggested_FailsConflict()
    {
        var recommendationService = new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        recommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse
            {
                ShouldSuggestReassignment = false,
                Reason = "No longer valid",
                AssignmentField = "synitiOwner",
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);

        var service = new ReassignmentExecutionService(
            recommendationService.Object,
            userRepository.Object,
            Mock.Of<IOperationalRiskService>(),
            Mock.Of<ISlaConfigurationService>());

        var result = await service.ExecuteAsync(
            CreateTicket("John"),
            new ReassignmentApplyRequest { SelectedOwnerId = 11 },
            CreateActor());

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(
            "This recommendation is no longer valid because assignment conditions changed.",
            result.Message);
    }

    private static Ticket CreateTicket(string synitiOwner)
    {
        return new Ticket
        {
            Id = "T-5001",
            Title = "Ticket",
            Description = "Desc",
            Status = "New",
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = "High",
            BoardId = 1,
            SynitiOwner = synitiOwner,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 1,
            LastModifiedDate = DateTime.UtcNow,
        };
    }

    private static User CreateActor() =>
        new()
        {
            Id = 99,
            DisplayName = "Reviewer",
            Email = "reviewer@example.com",
            Role = Auth0Roles.BusinessManager,
        };
}
