using System.Security.Claims;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class UserContextService(
    IUserRepository userRepository,
    IHttpContextAccessor httpContextAccessor,
    IAccessApprovalService accessApproval,
    CortexDbContext dbContext,
    ILogger<UserContextService> logger) : IUserContextService
{
    private readonly IUserRepository _userRepo = userRepository;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly IAccessApprovalService _accessApproval = accessApproval;
    private readonly CortexDbContext _dbContext = dbContext;
    private readonly ILogger<UserContextService> _logger = logger;
    private static readonly HashSet<string> ElevatedAccessRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        Auth0Roles.Admin,
        Auth0Roles.Developer
    };

    public Task<User> GetCurrentUserAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return GetCurrentUserAsync(principal);
    }

    public async Task<User> GetCurrentUserAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var changed = false;

        if (principal is null || principal.Identity is null || !principal.Identity.IsAuthenticated)
            throw new UnauthorizedAccessException("No authenticated user found.");

        var auth0Id = principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(auth0Id))
            throw new UnauthorizedAccessException("Missing Sub Claim.");

        var email = principal.FindFirst("email")?.Value ??
                    principal.FindFirst(ClaimTypes.Email)?.Value;
        var normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim();

        var displayName = principal.FindFirst("https://cortex-api/display_name")?.Value ??
                        principal.FindFirst("name")?.Value ??
                        principal.FindFirst("nickname")?.Value ??
                        principal.FindFirst("preferred_username")?.Value ??
                        principal.FindFirst("username")?.Value ??
                        principal.FindFirst(ClaimTypes.Name)?.Value ??
                        normalizedEmail?.Split('@')[0] ?? // Fallback to email prefix if no name claim is found
                        auth0Id;
        var nickName = principal.FindFirst("nickname")?.Value;
        var phoneNumber = principal.FindFirst("phone_number")?.Value ??
                          principal.FindFirst(ClaimTypes.MobilePhone)?.Value;

        var emailVerified = ResolveEmailVerified(principal);

        var user = await _userRepo.GetByAuth0IdAsync(auth0Id);
        cancellationToken.ThrowIfCancellationRequested();

        var existingByEmail = user is null && !string.IsNullOrWhiteSpace(normalizedEmail)
            ? await _userRepo.GetByEmailAsync(normalizedEmail)
            : null;
        if (user is null && existingByEmail is not null)
        {
            user = existingByEmail;
        }

        if (user is not null &&
            string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            user.Auth0Id = auth0Id;
            changed = true;
        }

        var hasActiveElevatedUser = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(localUser =>
                localUser.IsActive &&
                ElevatedAccessRoles.Contains(localUser.Role),
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (user is not null && !hasActiveElevatedUser)
        {
            if (!string.Equals(user.Role, Auth0Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                user.Role = Auth0Roles.Admin;
                changed = true;
            }
            if (!user.IsActive)
            {
                user.IsActive = true;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.Department))
            {
                user.Department = UserDepartmentPolicy.DefaultDeveloperDepartment;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName;
                changed = true;
            }

            _logger.LogInformation(
                "[BOOTSTRAP] First admin user created from authenticated Auth0 principal: {Email}",
                normalizedEmail ?? user.Email);
        }

        if (user is null)
        {
            var shouldBootstrapFirstElevated = !hasActiveElevatedUser;
            user = new User
            {
                Auth0Id = auth0Id,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName,
                NickName = string.IsNullOrWhiteSpace(nickName) ? null : nickName.Trim(),
                Email = normalizedEmail ?? BuildFallbackEmail(auth0Id),
                PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                Department = shouldBootstrapFirstElevated
                    ? UserDepartmentPolicy.DefaultDeveloperDepartment
                    : ResolveDepartmentClaim(principal),
                Role = shouldBootstrapFirstElevated ? Auth0Roles.Admin : Auth0Roles.User,
                IsActive = shouldBootstrapFirstElevated,
                CreatedDate = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };

            await _userRepo.CreateUserAsync(user);
            changed = true;

            if (shouldBootstrapFirstElevated)
            {
                _logger.LogInformation(
                    "[BOOTSTRAP] First admin user created from authenticated Auth0 principal: {Email}",
                    user.Email);
            }
            else
            {
                _logger.LogInformation(
                    "[AUTH0-LINK] Pending local user shell created from authenticated Auth0 principal: {Email}",
                    user.Email);
            }
        }

        // Demo pilot bypass: only honored when the email claim is actually verified by
        // Auth0, so a brand-new identity claiming the demo email cannot impersonate the
        // pilot account. The demo row is pinned to its first-linked Auth0 subject — we
        // bind an unlinked row on first login but refuse to silently rotate a linked one.
        if (user is null && _accessApproval.IsDemoCaller(normalizedEmail, emailVerified))
        {
            var candidate = await _userRepo.GetByEmailAsync(normalizedEmail!);
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate is not null)
            {
                if (string.IsNullOrWhiteSpace(candidate.Auth0Id))
                {
                    candidate.Auth0Id = auth0Id;
                    user = candidate;
                    changed = true;
                }
                else if (string.Equals(candidate.Auth0Id, auth0Id, StringComparison.Ordinal))
                {
                    user = candidate;
                }
                else
                {
                    _logger.LogWarning(
                        "Demo bypass rejected: incoming Auth0 subject does not match the pinned demo subject. Email={Email}, IncomingAuth0Id={IncomingAuth0Id}",
                        normalizedEmail,
                        auth0Id);
                    // Leave `user` null; falls through to DeniedUnknownUser below.
                }
            }
        }
        else if (user is null &&
                 !emailVerified &&
                 AccessApprovalService.MatchesDemoEmail(normalizedEmail))
        {
            _logger.LogWarning(
                "Demo bypass rejected: token presented {Email} without email_verified=true. Auth0Id={Auth0Id}",
                normalizedEmail,
                auth0Id);
        }

        if (changed)
        {
            await _userRepo.SaveChangesAsync();
            changed = false;
        }

        // Centralized access approval: unknown/inactive/expired identities are rejected
        // here instead of being silently auto-provisioned. Verified demo is exempt.
        var decision = _accessApproval.Evaluate(user, normalizedEmail, emailVerified);
        if (!decision.IsApproved)
        {
            _logger.LogWarning(
                "Access denied for authenticated identity. Reason={Reason}, Email={Email}, Auth0Id={Auth0Id}, EmailVerified={EmailVerified}",
                decision.DenialReason ?? AccessNotApprovedException.Reasons.UnknownUser,
                normalizedEmail ?? "(missing)",
                auth0Id,
                emailVerified);

            throw new AccessNotApprovedException(
                decision.DenialReason ?? AccessNotApprovedException.Reasons.UnknownUser,
                normalizedEmail,
                auth0Id);
        }

        if (user is null)
        {
            throw new UnauthorizedAccessException("User resolution failed after access approval.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) &&
            !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = normalizedEmail;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.NickName) &&
            !string.IsNullOrWhiteSpace(nickName))
        {
            user.NickName = nickName.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber) &&
            !string.IsNullOrWhiteSpace(phoneNumber))
        {
            user.PhoneNumber = phoneNumber.Trim();
            changed = true;
        }

        // Update display name if it changed in Auth0
        if (user.DisplayName != displayName && !string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName;
            changed = true;
        }

        if (user.LastLoginDate == null || user.LastLoginDate < DateTime.UtcNow.AddMinutes(-10))
        {
            user.LastLoginDate = DateTime.UtcNow;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var shouldUpdateDisplayName =
                string.IsNullOrWhiteSpace(user.DisplayName) ||
                user.DisplayName != displayName ||
                LooksLikeEmail(user.DisplayName);

            if (shouldUpdateDisplayName)
            {
                user.DisplayName = displayName;
                changed = true;
            }
        }

        if (changed)
            await _userRepo.SaveChangesAsync();  

        cancellationToken.ThrowIfCancellationRequested();
        return user;
    }

    public async Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request)
    {
        var assignmentNotificationChannel = ParseNotificationChannelOrNull(
            request.AssignmentNotificationChannel,
            nameof(request.AssignmentNotificationChannel));
        var slaRiskNotificationChannel = ParseNotificationChannelOrNull(
            request.SlaRiskNotificationChannel,
            nameof(request.SlaRiskNotificationChannel));

        // Update only allowed fields
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName;
        }

        user.NickName = NormalizeOptionalValue(request.NickName);
        user.PhoneNumber = NormalizeOptionalValue(request.PhoneNumber);
        user.Department = NormalizeOptionalValue(request.Department);
        user.AssignmentNotificationChannel = assignmentNotificationChannel;
        user.SlaRiskNotificationChannel = slaRiskNotificationChannel;
        user.LastModifiedDate = DateTime.UtcNow;

        await _userRepo.SaveChangesAsync();

        return user;
    }

    public bool LooksLikeEmail(string value)
    {
        return !string.IsNullOrEmpty(value) && value.Contains("@");
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static NotificationChannelMode? ParseNotificationChannelOrNull(
        string? rawValue,
        string fieldName)
    {
        var normalized = NormalizeOptionalValue(rawValue);
        if (normalized is null)
        {
            return null;
        }

        if (Enum.TryParse<NotificationChannelMode>(normalized, true, out var mode) &&
            Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new ArgumentException(
            $"{fieldName} must be one of Neither, Email, Teams, Both, or left blank to use the system default.",
            fieldName);
    }

    /// <summary>
    /// Reads the <c>email_verified</c> JWT claim. Auth0 emits this as a JSON boolean, which
    /// arrives as the string "true"/"false" in <see cref="Claim.Value"/>. Any other shape
    /// (missing, malformed, "True", etc.) is treated as unverified — fail closed.
    /// </summary>
    private static bool ResolveEmailVerified(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst("email_verified")?.Value
                  ?? principal.FindFirst("https://cortex-api/email_verified")?.Value;

        return bool.TryParse(raw, out var verified) && verified;
    }

    private static string? ResolveDepartmentClaim(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst("department")?.Value
                  ?? principal.FindFirst("https://cortex-api/department")?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim();
    }

    private static string BuildFallbackEmail(string auth0Id)
    {
        var sanitized = string.Concat(
            auth0Id.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-'));

        sanitized = string.Join(
            "-",
            sanitized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"user-{Guid.NewGuid():N}";
        }

        if (sanitized.Length > 180)
        {
            sanitized = sanitized[..180];
        }

        return $"{sanitized}@unknown.local";
    }
}
