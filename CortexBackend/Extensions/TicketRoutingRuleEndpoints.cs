using Cortex.API.Authorization;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class TicketRoutingRuleEndpoints
{
    public static void MapTicketRoutingRuleEndpoints(this WebApplication app)
    {
        var routing = app.MapGroup("/api/settings/ticket-routing")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("Ticket Routing");

        routing.MapGet("/", TicketRoutingRuleHandlers.GetTicketRoutingRules)
            .WithName("GetTicketRoutingRules")
            .Produces(StatusCodes.Status200OK);

        routing.MapPost("/", TicketRoutingRuleHandlers.CreateTicketRoutingRule)
            .WithName("CreateTicketRoutingRule")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        routing.MapPut("/{id:int}", TicketRoutingRuleHandlers.UpdateTicketRoutingRule)
            .WithName("UpdateTicketRoutingRule")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        routing.MapDelete("/{id:int}", TicketRoutingRuleHandlers.DeleteTicketRoutingRule)
            .WithName("DeleteTicketRoutingRule")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
