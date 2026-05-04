using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Handlers;

public static class TicketRoutingRuleHandlers
{
    public static async Task<IResult> GetTicketRoutingRules(
        ITicketRoutingRuleService service,
        CortexDbContext dbContext)
    {
        var rules = await service.GetAllAsync();
        var aliases = await BuildOwnerAliasesAsync(dbContext);
        return Results.Ok(rules.Select(rule => rule.ToResponse(aliases)));
    }

    public static async Task<IResult> GetRoutingRuleHealth(
        IRoutingRuleHealthService routingRuleHealthService,
        CancellationToken cancellationToken)
    {
        var overview = await routingRuleHealthService.GetOverviewAsync(cancellationToken);
        return Results.Ok(overview);
    }

    public static async Task<IResult> CreateTicketRoutingRule(
        UpsertTicketRoutingRuleRequest request,
        ITicketRoutingRuleService service,
        CortexDbContext dbContext)
    {
        try
        {
            var rule = new TicketRoutingRule
            {
                BoardId = request.BoardId,
                Priority = request.Priority,
                RequesterDepartment = request.RequesterDepartment,
                RequesterRole = request.RequesterRole,
                RulePriority = request.RulePriority,
                Weight = request.Weight,
                Department = request.Department,
                TitleContains = request.TitleContains,
                SynitiOwner = request.SynitiOwner,
                BusinessOwner = request.BusinessOwner,
                IsEnabled = request.IsEnabled
            };

            var savedRule = await service.CreateAsync(rule);
            var aliases = await BuildOwnerAliasesAsync(dbContext);
            return Results.Created($"/api/settings/ticket-routing/{savedRule.Id}", savedRule.ToResponse(aliases));
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    public static async Task<IResult> UpdateTicketRoutingRule(
        int id,
        UpsertTicketRoutingRuleRequest request,
        ITicketRoutingRuleService service,
        CortexDbContext dbContext)
    {
        try
        {
            var rule = new TicketRoutingRule
            {
                BoardId = request.BoardId,
                Priority = request.Priority,
                RequesterDepartment = request.RequesterDepartment,
                RequesterRole = request.RequesterRole,
                RulePriority = request.RulePriority,
                Weight = request.Weight,
                Department = request.Department,
                TitleContains = request.TitleContains,
                SynitiOwner = request.SynitiOwner,
                BusinessOwner = request.BusinessOwner,
                IsEnabled = request.IsEnabled
            };

            var savedRule = await service.UpdateAsync(id, rule);
            var aliases = await BuildOwnerAliasesAsync(dbContext);
            return Results.Ok(savedRule.ToResponse(aliases));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }

    private static async Task<IReadOnlyDictionary<string, User>> BuildOwnerAliasesAsync(CortexDbContext dbContext)
    {
        var users = await dbContext.Users.AsNoTracking().ToListAsync();
        return OwnerFieldResolution.BuildAliasLookup(users);
    }

    public static async Task<IResult> DeleteTicketRoutingRule(
        int id,
        ITicketRoutingRuleService service)
    {
        try
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
