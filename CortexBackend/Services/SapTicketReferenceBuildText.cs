using Cortex.API.Models;

namespace Cortex.API.Services;

internal static class SapTicketReferenceBuildText
{
    internal static string BuildTicketText(Ticket ticket) =>
        string.Join(
                "\n",
                new[]
                {
                        ticket.Title,
                        ticket.Description,
                    }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()))
            .TrimEnd();

    internal static string CombineSections(params string?[] chunks)
    {
        var body = string.Join(
            "\n\n",
            chunks
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim()));
        return body.Trim();
    }
}
