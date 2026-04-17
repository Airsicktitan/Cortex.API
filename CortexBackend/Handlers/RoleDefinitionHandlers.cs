using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class RoleDefinitionHandlers
{
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
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
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
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
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
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
