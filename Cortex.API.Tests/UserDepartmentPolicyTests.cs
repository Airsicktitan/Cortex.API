using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Tests;

public class UserDepartmentPolicyTests
{
    [Fact]
    public void ApplyDeveloperDepartmentDefault_EmptyInput_ReturnsSyniti()
    {
        var d = UserDepartmentPolicy.ApplyDeveloperDepartmentDefault(null, Auth0Roles.Developer);
        Assert.Equal(UserDepartmentPolicy.DefaultDeveloperDepartment, d);
        Assert.Equal(
            UserDepartmentPolicy.DefaultDeveloperDepartment,
            UserDepartmentPolicy.ApplyDeveloperDepartmentDefault("   ", Auth0Roles.Developer));
    }

    [Fact]
    public void ApplyDeveloperDepartmentDefault_NonDeveloper_PassesThroughOrTrims()
    {
        Assert.Null(UserDepartmentPolicy.ApplyDeveloperDepartmentDefault(null, Auth0Roles.User));
        Assert.Equal("HR", UserDepartmentPolicy.ApplyDeveloperDepartmentDefault("HR", Auth0Roles.User));
    }

    [Fact]
    public void ApplyDeveloperDepartmentDefault_DeveloperWithOverride_Preserves()
    {
        Assert.Equal(
            "Contracting",
            UserDepartmentPolicy.ApplyDeveloperDepartmentDefault("Contracting", Auth0Roles.Developer));
    }
}
