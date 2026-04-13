using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using System.Text;

namespace Cortex.API.Handlers;

public static class ReportDefinitionHandlers
{
    public static async Task<IResult> GetReportDefinitions(
        IReportDefinitionService service)
    {
        var definitions = await service.GetAllAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> GetAvailableDatabaseViews(
        IReportDefinitionService service)
    {
        var definitions = await service.GetAvailableViewsAsync();
        return Results.Ok(definitions.Select(definition => definition.ToResponse()));
    }

    public static async Task<IResult> CreateReportDefinition(
        UpsertReportDefinitionRequest request,
        IReportDefinitionService service)
    {
        try
        {
            var definition = new ReportDefinition
            {
                Name = request.Name,
                ViewName = request.ViewName,
                Description = request.Description,
                SqlQuery = request.SqlQuery,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.CreateAsync(definition);
            return Results.Created($"/api/settings/reports/{saved.Id}", saved.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateReportDefinition(
        int id,
        UpsertReportDefinitionRequest request,
        IReportDefinitionService service)
    {
        try
        {
            var definition = new ReportDefinition
            {
                Name = request.Name,
                ViewName = request.ViewName,
                Description = request.Description,
                SqlQuery = request.SqlQuery,
                IsEnabled = request.IsEnabled
            };

            var saved = await service.UpdateAsync(id, definition);
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

    public static async Task<IResult> DeleteReportDefinition(
        int id,
        IReportDefinitionService service)
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

    public static async Task<IResult> RunReportDefinition(
        int id,
        IReportDefinitionService service)
    {
        try
        {
            var result = await service.ExecuteAsync(id);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> ExportReport(
        string? format,
        ITicketRepository ticketRepository,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Only CSV export is currently supported." });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var tickets = (await ticketRepository.GetAllTicketsAsync())
            .Where(visibilityContext.CanView)
            .OrderByDescending(ticket => ticket.CreatedDate)
            .ThenByDescending(ticket => ticket.Id)
            .ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            tickets.Select(ticket => ticket.CreatedBy),
            null,
            tickets.Select(ticket => ticket.BoardId));
        var ticketResponses = tickets
            .Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext))
            .ToList();

        var csv = BuildCsv(ticketResponses);
        var fileName = $"cortex-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv))
            .ToArray();

        return Results.File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static string BuildCsv(IReadOnlyList<TicketResponse> tickets)
    {
        var builder = new StringBuilder();
        var headers = new[]
        {
            "Ticket Id",
            "Title",
            "Description",
            "Status",
            "Priority",
            "Board",
            "Story Points",
            "Syniti Owner",
            "Business Owner",
            "Created By",
            "Created Date (UTC)",
            "Last Modified Date (UTC)",
            "SLA Status",
            "SLA Target Date (UTC)",
            "SLA Remaining Minutes",
            "SLA Breached"
        };

        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var ticket in tickets)
        {
            var values = new[]
            {
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.BoardName,
                ticket.StoryPoints?.ToString() ?? string.Empty,
                ticket.SynitiOwner ?? string.Empty,
                ticket.BusinessOwner ?? string.Empty,
                ticket.CreatedByDisplayName,
                ticket.CreatedDate.ToString("O"),
                ticket.LastModifiedDate?.ToString("O") ?? string.Empty,
                ticket.SlaStatus,
                ticket.SlaTargetDate.ToString("O"),
                ticket.SlaRemainingMinutes.ToString(),
                ticket.IsSlaBreached ? "Yes" : "No"
            };

            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        var normalizedValue = value ?? string.Empty;
        var escapedValue = normalizedValue.Replace("\"", "\"\"");
        return $"\"{escapedValue}\"";
    }
}
