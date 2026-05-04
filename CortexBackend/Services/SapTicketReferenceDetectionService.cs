using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class SapTicketReferenceDetectionService(CortexDbContext db) : ISapTicketReferenceDetectionService
{
    public async Task<SapTicketReferenceContextDto> DetectSapReferencesForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var text = BuildSearchText(ticket);
        var (tables, fields) = await LoadEnabledCatalogAsync(cancellationToken).ConfigureAwait(false);
        return SapTicketReferenceDetector.DetectForTicket(ticket.Id, text, tables, fields);
    }

    private static string BuildSearchText(Ticket ticket)
    {
        return string.Join(
            "\n",
            new[] { ticket.Title, ticket.Description }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim()));
    }

    private async Task<(List<SapTicketCatalogTable> Tables, List<SapTicketCatalogField> Fields)> LoadEnabledCatalogAsync(
        CancellationToken cancellationToken)
    {
        var tables = await db.SapTables.AsNoTracking()
            .Where(t => t.SapReferenceSource.IsEnabled)
            .Select(t => new SapTicketCatalogTable(
                t.Id,
                t.SapReferenceSourceId,
                t.SapReferenceSource.Name,
                t.TableName,
                t.Description,
                t.Module,
                t.BusinessObject,
                t.DataDomain,
                t.IsCustom))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fields = await db.SapFields.AsNoTracking()
            .Where(f => f.SapTableMetadata.SapReferenceSource.IsEnabled)
            .Select(f => new SapTicketCatalogField(
                f.Id,
                f.SapTableMetadataId,
                f.SapTableMetadata.SapReferenceSourceId,
                f.SapTableMetadata.SapReferenceSource.Name,
                f.SapTableMetadata.TableName,
                f.SapTableMetadata.Description,
                f.SapTableMetadata.Module,
                f.SapTableMetadata.BusinessObject,
                f.SapTableMetadata.DataDomain,
                f.SapTableMetadata.IsCustom,
                f.FieldName,
                f.Description,
                f.IsCustom))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (tables, fields);
    }
}
