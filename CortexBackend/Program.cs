using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Cortex.API.ExceptionHandling;
using Cortex.API.Extensions;
using Cortex.API.Health;
using Cortex.API.Middleware;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Cortex.API.Services;
using Cortex.API.Authorization;
using System.Security.Claims;


var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AzureCortexDb")
    ?? builder.Configuration.GetConnectionString("CortexDB")
    ?? throw new InvalidOperationException(
        "Connection string 'AzureCortexDb' is not configured. Set ConnectionStrings:AzureCortexDb or use CortexDB as a fallback.");

builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<CortexDbContext>(tags: ["ready"]);

builder.Services.Configure<Auth0ManagementOptions>(builder.Configuration.GetSection("Auth0"));


// Add services
builder.Services.AddEndpointsApiExplorer(); // for minimal APIs, needed for Swagger
builder.Services.AddHttpClient<IAuth0ManagementService, Auth0ManagementService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Auth0ManagementOptions>>()
            .Value;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Clear();

        var normalizedDomain = options.Domain?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(normalizedDomain) &&
            Uri.TryCreate($"https://{normalizedDomain}", UriKind.Absolute, out var baseAddress))
        {
            client.BaseAddress = baseAddress;
        }
    });
builder.Services.AddHttpClient<IAuth0UserRoleSyncService, Auth0UserRoleSyncService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Auth0ManagementOptions>>()
            .Value;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Clear();

        var normalizedDomain = options.Domain?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(normalizedDomain) &&
            Uri.TryCreate($"https://{normalizedDomain}", UriKind.Absolute, out var baseAddress))
        {
            client.BaseAddress = baseAddress;
        }
    });
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ISlaConfigurationRepository, SlaConfigurationRepository>();
builder.Services.AddScoped<IArchiveConfigurationRepository, ArchiveConfigurationRepository>();
builder.Services.AddScoped<ISessionConfigurationRepository, SessionConfigurationRepository>();
builder.Services.AddScoped<INotificationChannelConfigurationRepository, NotificationChannelConfigurationRepository>();
builder.Services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
builder.Services.AddScoped<IStoredProcedureDefinitionRepository, StoredProcedureDefinitionRepository>();
builder.Services.AddScoped<ITicketStatusDefinitionRepository, TicketStatusDefinitionRepository>();
builder.Services.AddScoped<ITicketRoutingRuleRepository, TicketRoutingRuleRepository>();
builder.Services.AddScoped<ITicketBoardDefinitionRepository, TicketBoardDefinitionRepository>();
builder.Services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IHttpRequestLogRepository, HttpRequestLogRepository>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<ISlaConfigurationService, SlaConfigurationService>();
builder.Services.AddScoped<IArchiveConfigurationService, ArchiveConfigurationService>();
builder.Services.AddScoped<IArchiveAutomationService, ArchiveAutomationService>();
builder.Services.AddScoped<ISessionConfigurationService, SessionConfigurationService>();
builder.Services.AddScoped<INotificationChannelConfigurationService, NotificationChannelConfigurationService>();
builder.Services.AddScoped<IReportDefinitionService, ReportDefinitionService>();
builder.Services.AddScoped<IStoredProcedureDefinitionService, StoredProcedureDefinitionService>();
builder.Services.AddScoped<ITicketStatusService, TicketStatusService>();
builder.Services.AddScoped<ITicketRoutingRuleService, TicketRoutingRuleService>();
builder.Services.AddScoped<ITicketBoardService, TicketBoardService>();
builder.Services.AddScoped<IScheduledJobService, ScheduledJobService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITicketArchivalService, TicketArchivalService>();
builder.Services.AddScoped<ITicketVisibilityService, TicketVisibilityService>();
builder.Services.AddScoped<ITicketAuditService, TicketAuditService>();
builder.Services.AddScoped<IDatabaseProgrammabilityService, DatabaseProgrammabilityService>();
builder.Services.AddScoped<IResponseMappingContextFactory, ResponseMappingContextFactory>();
builder.Services.AddHttpClient<INotificationDeliveryService, NotificationDeliveryService>();
builder.Services.Configure<EmailNotificationOptions>(builder.Configuration.GetSection("Notifications:Email"));
builder.Services.Configure<TeamsNotificationOptions>(builder.Configuration.GetSection("Notifications:Teams"));
builder.Services.AddSingleton<IRealtimeEventService, RealtimeEventService>();
builder.Services.AddHostedService<ScheduledJobHostedService>();
builder.Services.AddHostedService<SlaNotificationHostedService>();
builder.Services.AddHttpContextAccessor(); // Register the built-in IHttpContextAccessor

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Required with AddExceptionHandler<T>(): wires ExceptionHandlerOptions so UseExceptionHandler() has a valid ExceptionHandler (otherwise startup throws).
builder.Services.AddProblemDetails();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    // Basic info
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CORTEX API",
        Version = "v1",
        Description = "Central Operations & Routing Technology EXpert - Intelligent Support Operations Platform",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Adam Hooper",
            Email = "adam.hooper@syniti.com"
        }
    });

    options.AddSecurityDefinition("oauth2", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
        Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
        {
            AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"https://{builder.Configuration["Auth0:Domain"]}/authorize"),
                TokenUrl = new Uri($"https://{builder.Configuration["Auth0:Domain"]}/oauth/token"),

                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect scope" },
                    { "profile", "Profile scope" },
                    { "email", "Email scope" }
                }
            }
        }
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "openid", "profile", "email" }
        }
    });

    options.DocumentFilter<RemoveBearerDocumentFilter>();

});

// Add CORS for React Frontend.
// Non-development environments must have AllowedOrigins configured — startup fails if absent.
// Set via Azure Container Apps environment variable:
//   AllowedOrigins__0=https://cortex-frontend.<env>.azurecontainerapps.io
// Read and validate here (builder phase) so the failure is eager, not on the first request.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is not { Length: > 0 } && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "AllowedOrigins must be configured in non-development environments. " +
        "Set AllowedOrigins__0 (and additional entries as needed) via the " +
        "Azure Container Apps environment variables.");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Development only — AllowedOrigins is not set.
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Authentication and Authorization can be added here
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{builder.Configuration["Auth0:Domain"]}";
        options.Audience = builder.Configuration["Auth0:Audience"];
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudiences = [builder.Configuration["Auth0:Audience"]],
            ValidateIssuer = true,
            ValidIssuer = $"https://{builder.Configuration["Auth0:Domain"]}/",  // <-- Trailing slash added here
            ValidateLifetime = true,
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };

        options.MapInboundClaims = false; // Prevents default claim type mapping
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken)
                    && path.StartsWithSegments("/api/realtime"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                if (context.Principal is not null)
                {
                    JwtRoleClaims.AddNormalizedRoleClaims(context.Principal);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options => options.AddCortexPolicies());

// Build app
var app = builder.Build();

var auth0ManagementOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<Auth0ManagementOptions>>()
    .Value;
var startupLogger = app.Services
    .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
    .CreateLogger("Auth0Management");
if (string.IsNullOrWhiteSpace(auth0ManagementOptions.ManagementClientSecret))
{
    if (app.Environment.IsDevelopment())
    {
        startupLogger.LogWarning(
            "Auth0:ManagementClientSecret is not configured. User creation, role assignment, and Auth0 role sync " +
            "will fail at runtime. Set Auth0__ManagementClientSecret before using admin management features.");
    }
    else
    {
        throw new InvalidOperationException(
            "Auth0:ManagementClientSecret is required in non-development environments. " +
            "Set this value via the Azure Container Apps environment variable Auth0__ManagementClientSecret.");
    }
}

app.UseStructuredRequestLogging();
app.UseExceptionHandler();

// Swagger is disabled in Production. Available in Development and Staging only.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CORTEX API v1");
        options.RoutePrefix = "swagger";
        options.OAuthClientId(builder.Configuration["Auth0:ClientId"]);
        options.OAuthUsePkce(); // Use PKCE for enhanced security in Swagger UI
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
        {
            { "audience", builder.Configuration["Auth0:Audience"] ?? string.Empty },
            { "connection", "Username-Password-Authentication" }
        });
    });
}

app.UseCors();

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

var healthJsonWriter = MinimalHealthCheckResponseWriter.WriteAsync;

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = healthJsonWriter,
}).WithTags("Health").WithName("HealthLive").AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = healthJsonWriter,
}).WithTags("Health").WithName("HealthReady").AllowAnonymous();

// Map all ticket endpoints
app.MapRootEndpoint();
app.MapTicketEndpoints();
app.MapTicketAttachmentEndpoints();
app.MapUserEndpoints();
app.MapCommentEndpoints();
app.MapClaimEndpoint();
app.MapSlaConfigurationEndpoints();
app.MapArchiveConfigurationEndpoints();
app.MapSessionConfigurationEndpoints();
app.MapNotificationChannelConfigurationEndpoints();
app.MapReportDefinitionEndpoints();
app.MapAdminLogEndpoints();
app.MapStoredProcedureDefinitionEndpoints();
app.MapTicketStatusEndpoints();
app.MapTicketRoutingRuleEndpoints();
app.MapTicketBoardEndpoints();
app.MapScheduledJobEndpoints();
app.MapNotificationEndpoints();
app.MapRealtimeEndpoints();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CortexDbContext>();
    db.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    var ticketBoardService = scope.ServiceProvider.GetRequiredService<ITicketBoardService>();
    await ticketBoardService.EnsureDefaultsAsync();
}

app.Run();