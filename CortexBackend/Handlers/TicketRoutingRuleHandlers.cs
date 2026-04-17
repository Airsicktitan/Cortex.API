using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class TicketRoutingRuleHandlers
{
    public static async Task<IResult> GetTicketRoutingRules(
        ITicketRoutingRuleService service)
    {
        var rules = await service.GetAllAsync();
        return Results.Ok(rules.Select(rule => rule.ToResponse()));
    }

    public static async Task<IResult> CreateTicketRoutingRule(
        UpsertTicketRoutingRuleRequest request,
        ITicketRoutingRuleService service)
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
            return Results.Created($"/api/settings/ticket-routing/{savedRule.Id}", savedRule.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateTicketRoutingRule(
        int id,
        UpsertTicketRoutingRuleRequest request,
        ITicketRoutingRuleService service)
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
            return Results.Ok(savedRule.ToResponse());
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
