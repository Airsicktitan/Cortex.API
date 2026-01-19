namespace Cortex.API.Data;

using Cortex.API.Models;

public static class SampleData
{
    public static List<Ticket> GetSampleTickets()
    {
        return new List<Ticket>
        {
            new()
            {
                Id = "TICKET-001",
                Title = "BAPI Failure in Production",
                Description = "Customer creation BAPI is throwing null reference errors",
                Status = "In Progress",
                Priority = "Critical",
                SynitiOwner = "Adam Hooper",
                BusinessOwner = "Sarah Johnson",
                CreatedBy = "Sarah Johnson",
                CreatedDate = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = "TICKET-002",
                Title = "Interface Mapping Issue",
                Description = "Data not flowing correctly from SAP to Salesforce",
                Status = "New",
                Priority = "High",
                BusinessOwner = "Mike Chen",
                CreatedBy = "Mike Chen",
                CreatedDate = DateTime.UtcNow.AddHours(-5)
            },
            new()
            {
                Id = "TICKET-003",
                Title = "Report Performance Optimization",
                Description = "Monthly report taking 45 minutes to generate",
                Status = "Pending Business Review",
                Priority = "Medium",
                SynitiOwner = "Adam Hooper",
                BusinessOwner = "Lisa Martinez",
                CreatedBy = "Lisa Martinez",
                CreatedDate = DateTime.UtcNow.AddDays(-7)
            },
            new()
            {
                Id = "TICKET-004",
                Title = "Fix React Filtering",
                Description = "React Filtering is not working for Status or Priority in the search bar",
                Status = "New",
                Priority = "Critical",
                SynitiOwner = "Adam Hooper",
                BusinessOwner = "Adam Hooper",
                CreatedBy = "Adam Hooper",
                CreatedDate = DateTime.UtcNow.AddDays(-7)
            }
        };
    }
}