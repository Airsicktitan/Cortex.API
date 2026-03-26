using Cortex.API.Models;

namespace Cortex.API.DTO;


public static class UserResponseExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate
        };
    }
}