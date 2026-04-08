namespace Cortex.API.DTO;

public class UpdateSlaConfigurationRequest
{
    public required List<SlaConfigurationItemRequest> Policies { get; set; }
}

public class SlaConfigurationItemRequest
{
    public required string Priority { get; set; }
    public int TargetHours { get; set; }
    public int WarningHours { get; set; }
}
