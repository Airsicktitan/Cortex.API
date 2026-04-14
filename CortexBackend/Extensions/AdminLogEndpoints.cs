using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class AdminLogEndpoints
{
    public static void MapAdminLogEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin/logs")
            .RequireAuthorization("AdminLogsExport")
            .WithTags("Admin Logs");

        admin.MapGet("/export", AdminLogExportHandlers.ExportRequestLogs)
            .WithName("ExportRequestLogs")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces(StatusCodes.Status400BadRequest);
    }
}
