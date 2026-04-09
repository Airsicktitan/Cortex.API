using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ReportDefinitionEndpoints
{
    public static void MapReportDefinitionEndpoints(this WebApplication app)
    {
        var reportSettings = app.MapGroup("/api/settings/reports")
            .RequireAuthorization("ReportsAdvanced")
            .WithTags("Report Definitions");

        reportSettings.MapGet("/", ReportDefinitionHandlers.GetReportDefinitions)
            .WithName("GetReportDefinitions")
            .Produces(StatusCodes.Status200OK);

        reportSettings.MapGet("/database-views", ReportDefinitionHandlers.GetAvailableDatabaseViews)
            .WithName("GetAvailableDatabaseViews")
            .Produces(StatusCodes.Status200OK);

        reportSettings.MapPost("/", ReportDefinitionHandlers.CreateReportDefinition)
            .WithName("CreateReportDefinition")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        reportSettings.MapPut("/{id:int}", ReportDefinitionHandlers.UpdateReportDefinition)
            .WithName("UpdateReportDefinition")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        reportSettings.MapDelete("/{id:int}", ReportDefinitionHandlers.DeleteReportDefinition)
            .WithName("DeleteReportDefinition")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        var reports = app.MapGroup("/api/reports")
            .RequireAuthorization("ReportsAdvanced")
            .WithTags("Reports");

        reports.MapGet("/custom", ReportDefinitionHandlers.GetReportDefinitions)
            .WithName("GetCustomReports")
            .Produces(StatusCodes.Status200OK);

        reports.MapGet("/custom/{id:int}", ReportDefinitionHandlers.RunReportDefinition)
            .WithName("RunCustomReport")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
