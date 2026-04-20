using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class AccessApprovalServiceTests
{
    private static readonly AccessApprovalService Sut = new();

    [Fact]
    public void IsDemoCaller_VerifiedDemoEmail_ReturnsTrue()
    {
        Assert.True(Sut.IsDemoCaller("demo@cortex.com", emailVerified: true));
    }

    [Theory]
    [InlineData("demo@cortex.com", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("someone.else@cortex.com", true)]
    public void IsDemoCaller_Rejects_UnverifiedOrNonDemo(string? email, bool verified)
    {
        Assert.False(Sut.IsDemoCaller(email, verified));
    }

    [Fact]
    public void Evaluate_VerifiedDemoWithoutLocalUser_IsApproved()
    {
        var decision = Sut.Evaluate(existingLocalUser: null, email: "demo@cortex.com", emailVerified: true);
        Assert.True(decision.IsApproved);
    }

    [Fact]
    public void Evaluate_UnverifiedDemoEmail_IsDeniedUnknownUser()
    {
        var decision = Sut.Evaluate(existingLocalUser: null, email: "demo@cortex.com", emailVerified: false);
        Assert.False(decision.IsApproved);
        Assert.Equal(AccessNotApprovedException.Reasons.UnknownUser, decision.DenialReason);
    }

    [Fact]
    public void Evaluate_ActiveNonExpiredUser_IsApproved()
    {
        var user = new User { Email = "u@c.com", IsActive = true, ExpiryDate = null };
        var decision = Sut.Evaluate(user, email: "u@c.com", emailVerified: true);
        Assert.True(decision.IsApproved);
    }

    [Fact]
    public void Evaluate_InactiveUser_IsDeniedInactive()
    {
        var user = new User { Email = "u@c.com", IsActive = false };
        var decision = Sut.Evaluate(user, email: "u@c.com", emailVerified: true);
        Assert.False(decision.IsApproved);
        Assert.Equal(AccessNotApprovedException.Reasons.Inactive, decision.DenialReason);
    }

    [Fact]
    public void Evaluate_ExpiredUser_IsDeniedExpired()
    {
        var user = new User
        {
            Email = "u@c.com",
            IsActive = true,
            ExpiryDate = DateTime.UtcNow.AddMinutes(-1),
        };
        var decision = Sut.Evaluate(user, email: "u@c.com", emailVerified: true);
        Assert.False(decision.IsApproved);
        Assert.Equal(AccessNotApprovedException.Reasons.Expired, decision.DenialReason);
    }

    [Fact]
    public void Evaluate_UnknownUserAndNotDemo_IsDeniedUnknownUser()
    {
        var decision = Sut.Evaluate(existingLocalUser: null, email: "random@external.com", emailVerified: true);
        Assert.False(decision.IsApproved);
        Assert.Equal(AccessNotApprovedException.Reasons.UnknownUser, decision.DenialReason);
    }
}
