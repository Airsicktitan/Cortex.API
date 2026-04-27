using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Handlers;

public static class RoleDefinitionHandlers
{
    public static async Task<IResult> SyncFromAuth0(
        IRoleDefinitionService roleDefinitionService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await roleDefinitionService.SyncFromAuth0Async(cancellationToken);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return SafeErrorResponses.ServerError("Auth0 management is not configured");
        }
        catch (Auth0ManagementException exception)
        {
            return SafeErrorResponses.UpstreamError(exception.StatusCode, "Failed to sync roles from Auth0");
        }
    }

    public static async Task<IResult> GetRoles(IRoleDefinitionService roleDefinitionService)
    {
        var roles = await roleDefinitionService.GetAllAsync();
        return Results.Ok(roles.Select(role => role.ToResponse()));
    }

    public static IResult GetRolePermissions(IRoleDefinitionService roleDefinitionService)
    {
        return Results.Ok(roleDefinitionService.AllowedPermissions);
    }

    public static async Task<IResult> CreateRole(
        UpsertRoleDefinitionRequest request,
        IRoleDefinitionService roleDefinitionService)
    {
        try
        {
            var role = new RoleDefinition
            {
                Name = request.Name,
                Description = request.Description,
                Permissions = request.Permissions ?? [],
                IsEnabled = request.IsEnabled
            };

            var saved = await roleDefinitionService.CreateAsync(role);
            return Results.Created($"/api/settings/roles/{saved.Id}", saved.ToResponse());
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
        catch (DbUpdateException exception) when (IsRoleDefinitionUniqueConstraintViolation(exception))
        {
            return Results.Conflict(new { message = "A role with this name already exists." });
        }
    }

    public static async Task<IResult> UpdateRole(
        int id,
        UpsertRoleDefinitionRequest request,
        IRoleDefinitionService roleDefinitionService)
    {
        try
        {
            var role = new RoleDefinition
            {
                Name = request.Name,
                Description = request.Description,
                Permissions = request.Permissions ?? [],
                IsEnabled = request.IsEnabled
            };

            var saved = await roleDefinitionService.UpdateAsync(id, role);
            return Results.Ok(saved.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
        catch (DbUpdateException exception) when (IsRoleDefinitionUniqueConstraintViolation(exception))
        {
            return Results.Conflict(new { message = "A role with this name already exists." });
        }
    }

    public static async Task<IResult> DeleteRole(
        int id,
        IRoleDefinitionService roleDefinitionService)
    {
        try
        {
            await roleDefinitionService.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    private static bool IsRoleDefinitionUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }
}
