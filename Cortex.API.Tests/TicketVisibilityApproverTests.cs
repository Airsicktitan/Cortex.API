using System.Security.Claims;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Cortex.API.Tests;

public sealed class TicketVisibilityApproverTests
{
    [Fact]
    public async Task ApproverJwt_Resolves_To_PendingApproverScope()
    {
        var cortexUser = new User
        {
            Id = 7,
            DisplayName = "Reviewer",
            Email = "reviewer@example.com",
            Role = Auth0Roles.User,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
        };

        var userContextMock = new Mock<IUserContextService>(MockBehavior.Strict);
        userContextMock.Setup(s => s.GetCurrentUserAsync()).ReturnsAsync(cortexUser);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, Auth0Roles.Approver)],
            authenticationType: "Test");

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        var accessorMock = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        ITicketVisibilityService service = new TicketVisibilityService(
            userContextMock.Object,
            accessorMock.Object);

        var visibility = await service.GetCurrentVisibilityAsync();

        Assert.Equal(TicketVisibilityScope.PendingApprover, visibility.Scope);

        var strangerPending = new Ticket
        {
            CreatedBy = 999,
            ApprovalStatus = ApprovalStatus.PendingApproval,
        };
        Assert.True(visibility.CanView(strangerPending));
    }

    [Fact]
    public void PendingApprover_CanView_StrangerPendingApprovalTicket()
    {
        var visibility = new TicketVisibilityContext(
            UserId: 1,
            DisplayName: "A",
            Email: "a@x",
            Scope: TicketVisibilityScope.PendingApprover);

        var pending = new Ticket
        {
            Id = "t-pending",
            CreatedBy = 999,
            ApprovalStatus = ApprovalStatus.PendingApproval,
        };

        Assert.True(visibility.CanView(pending));
    }

    [Fact]
    public void PendingApprover_Cannot_View_StrangerActiveTicket_From_OtherCreator()
    {
        var visibility = new TicketVisibilityContext(
            UserId: 1,
            DisplayName: "A",
            Email: "a@x",
            Scope: TicketVisibilityScope.PendingApprover);

        var other = new Ticket
        {
            Id = "t-active",
            CreatedBy = 88,
            ApprovalStatus = ApprovalStatus.Approved,
        };

        Assert.False(visibility.CanView(other));
    }
}
