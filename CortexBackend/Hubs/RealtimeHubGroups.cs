namespace Cortex.API.Hubs;

public static class RealtimeHubGroups
{
    public static string ForUser(int userId) => $"user:{userId}";
}
