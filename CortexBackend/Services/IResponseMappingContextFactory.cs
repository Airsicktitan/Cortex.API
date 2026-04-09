using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IResponseMappingContextFactory
{
    Task<ResponseMappingContext> CreateAsync(
        IEnumerable<int> userIds,
        IEnumerable<int>? storedProcedureDefinitionIds = null,
        CancellationToken cancellationToken = default);
}
