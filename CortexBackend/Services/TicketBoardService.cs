using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class TicketBoardService(
    ITicketBoardDefinitionRepository repository) : ITicketBoardService
{
    private readonly ITicketBoardDefinitionRepository _repository = repository;

    public async Task<IReadOnlyList<TicketBoardDefinition>> GetAllAsync()
    {
        var definitions = await _repository.GetAllAsync();
        return definitions.Select(Clone).ToList();
    }

    public async Task<IReadOnlyList<TicketBoardDefinition>> GetEnabledAsync()
    {
        return (await _repository.GetAllAsync())
            .Where(definition => definition.IsEnabled)
            .Select(Clone)
            .ToList();
    }

    public async Task<TicketBoardDefinition> CreateAsync(TicketBoardDefinition definition)
    {
        var normalizedDefinition = Normalize(definition);
        await ValidateAsync(normalizedDefinition, null);

        await _repository.AddAsync(normalizedDefinition);
        await _repository.SaveChangesAsync();

        return Clone(normalizedDefinition);
    }

    public async Task<TicketBoardDefinition> UpdateAsync(int id, TicketBoardDefinition definition)
    {
        var existingDefinition = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket board was not found.");

        var normalizedDefinition = Normalize(definition);
        await ValidateAsync(normalizedDefinition, id);

        existingDefinition.Name = normalizedDefinition.Name;
        existingDefinition.Description = normalizedDefinition.Description;
        existingDefinition.RequiresStoryPoints = normalizedDefinition.RequiresStoryPoints;
        existingDefinition.IsEnabled = normalizedDefinition.IsEnabled;
        existingDefinition.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
        return Clone(existingDefinition);
    }

    public async Task DeleteAsync(int id)
    {
        var existingDefinition = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket board was not found.");

        if (await _repository.IsBoardInUseAsync(id))
        {
            throw new InvalidOperationException(
                "Move active and archived tickets off this board before deleting it.");
        }

        var allBoards = await _repository.GetAllAsync();
        var remainingEnabledCount = allBoards.Count(definition =>
            definition.Id != id && definition.IsEnabled);
        if (existingDefinition.IsEnabled && remainingEnabledCount == 0)
        {
            throw new InvalidOperationException(
                "At least one enabled board must remain available.");
        }

        _repository.Delete(existingDefinition);
        await _repository.SaveChangesAsync();
    }

    public async Task<TicketBoardDefinition> GetDefaultCreateBoardAsync()
    {
        var enabledBoards = await GetEnabledAsync();
        var defaultBoard = enabledBoards.FirstOrDefault(definition =>
            string.Equals(definition.Name, "Regular", StringComparison.OrdinalIgnoreCase))
            ?? enabledBoards.FirstOrDefault();

        return defaultBoard ?? throw new InvalidOperationException(
            "No enabled ticket boards are configured.");
    }

    public async Task<TicketBoardDefinition?> GetByIdAsync(int id)
    {
        var definition = await _repository.GetByIdAsync(id);
        return definition is null ? null : Clone(definition);
    }

    private async Task ValidateAsync(TicketBoardDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Board name is required.", nameof(definition));
        }

        var existingDefinitions = await _repository.GetAllAsync();
        var duplicateDefinition = existingDefinitions.FirstOrDefault(existingDefinition =>
            existingDefinition.Id != existingId &&
            string.Equals(
                existingDefinition.Name.Trim(),
                definition.Name.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (duplicateDefinition is not null)
        {
            throw new ArgumentException(
                "A board with that name already exists.",
                nameof(definition));
        }

        var remainingEnabledCount = existingDefinitions.Count(existingDefinition =>
            existingDefinition.Id != existingId && existingDefinition.IsEnabled);
        if (!definition.IsEnabled && remainingEnabledCount == 0)
        {
            throw new ArgumentException(
                "At least one enabled board must remain available.",
                nameof(definition));
        }
    }

    private static TicketBoardDefinition Normalize(TicketBoardDefinition definition)
    {
        return new TicketBoardDefinition
        {
            Name = definition.Name.Trim(),
            Description = NormalizeOptionalValue(definition.Description),
            RequiresStoryPoints = definition.RequiresStoryPoints,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc == default
                ? DateTime.UtcNow
                : definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }

    private static TicketBoardDefinition Clone(TicketBoardDefinition definition)
    {
        return new TicketBoardDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            RequiresStoryPoints = definition.RequiresStoryPoints,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
