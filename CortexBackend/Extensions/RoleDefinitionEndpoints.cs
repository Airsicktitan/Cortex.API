using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class RoleDefinitionEndpoints
{
    public static void MapRoleDefinitionEndpoints(this WebApplication app)
    {
        var roles = app.MapGroup("/api/settings/roles")
            .RequireAuthorization()
            .WithTags("Role Definitions");

        roles.MapGet("/", RoleDefinitionHandlers.GetRoles)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("GetRoleDefinitions")
            .Produces<List<RoleDefinitionResponse>>(StatusCodes.Status200OK);

        roles.MapGet("/permissions", RoleDefinitionHandlers.GetRolePermissions)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("GetRoleDefinitionPermissions")
            .Produces<List<string>>(StatusCodes.Status200OK);

        roles.MapPost("/", RoleDefinitionHandlers.CreateRole)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("CreateRoleDefinition")
            .Accepts<UpsertRoleDefinitionRequest>("application/json")
            .Produces<RoleDefinitionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        roles.MapPut("/{id:int}", RoleDefinitionHandlers.UpdateRole)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("UpdateRoleDefinition")
            .Accepts<UpsertRoleDefinitionRequest>("application/json")
            .Produces<RoleDefinitionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        roles.MapDelete("/{id:int}", RoleDefinitionHandlers.DeleteRole)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("DeleteRoleDefinition")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
