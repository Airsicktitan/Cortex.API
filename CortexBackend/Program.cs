using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Cortex.API.ExceptionHandling;
using Cortex.API.Extensions;
using Cortex.API.Health;
using Cortex.API.Middleware;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Configuration;
using Cortex.API.Authorization;
using Cortex.API.Hubs;
using System.Security.Claims;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var connectionString = DatabaseConnectionConfiguration.ResolveFirstNonEmpty(builder.Configuration)
    ?? throw new InvalidOperationException(
        "Database connection string is not configured. " +
        "For local Development, set ConnectionStrings:CortexDb (see appsettings.Development.json). " +
        "For Azure, set ConnectionStrings__CortexDb as an environment variable.");

builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(
            connectionString,
            sqlServerOptions =>
            {
                // Handles transient connection failures (for example, sleeping SQL instances waking up).
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            })
        .ConfigureWarnings(warnings =>
            warnings.Log(CoreEventId.ExecutionStrategyRetrying)));

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<CortexDbContext>(tags: ["ready"]);

builder.Services.Configure<Auth0ManagementOptions>(builder.Configuration.GetSection("Auth0"));
builder.Services.Configure<SharePointGraphOptions>(builder.Configuration.GetSection("SharePointGraph"));
builder.Services.AddHttpClient(
    "SharePointGraph",
    client =>
    {
        client.Timeout = TimeSpan.FromMinutes(2);
    });
builder.Services.AddScoped<ISharePointGraphClient, SharePointGraphClient>();


// Add services
builder.Services.AddEndpointsApiExplorer(); // for minimal APIs, needed for Swagger
builder.Services.AddMemoryCache();
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
builder.Services.AddRateLimiter(AiRateLimitPolicies.Configure);
builder.Services.AddDataProtection();
builder.Services.AddScoped<IIntegrationCredentialStore, EncryptedIntegrationCredentialStore>();
builder.Services.AddScoped<IIntegrationCredentialAdminService, IntegrationCredentialAdminService>();
builder.Services.AddScoped<IIntegrationConnectionHealthService, IntegrationConnectionHealthService>();
builder.Services.AddScoped<IIntegrationActivityService, IntegrationActivityService>();
builder.Services.AddScoped<IExternalIntegrationService, ExternalIntegrationService>();
builder.Services.AddScoped<ISapReferenceService, SapReferenceService>();
builder.Services.AddScoped<ReviewerTicketContextAssembler>();
builder.Services.AddScoped<ISapReferenceContextService, SapReferenceContextService>();
builder.Services.AddScoped<ISynitiKnowledgeContextService, SynitiKnowledgeContextService>();
builder.Services.AddScoped<ISynitiKnowledgeCatalogReadService, SynitiKnowledgeCatalogReadService>();
builder.Services.AddScoped<ISapReferenceCatalogReadService, SapReferenceCatalogReadService>();
builder.Services.AddScoped<ITicketCreationApplicationService, TicketCreationApplicationService>();
builder.Services.AddScoped<SharePointExternalWorkSourceAdapter>();
builder.Services.AddScoped<IExternalWorkSourceAdapter>(sp => sp.GetRequiredService<SharePointExternalWorkSourceAdapter>());
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDemoEligibilityBootstrapService, DemoEligibilityBootstrapService>();
builder.Services.AddScoped<IAuth0UserDirectorySyncService, Auth0UserDirectorySyncService>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ISlaConfigurationRepository, SlaConfigurationRepository>();
builder.Services.AddScoped<IArchiveConfigurationRepository, ArchiveConfigurationRepository>();
builder.Services.AddScoped<ISessionConfigurationRepository, SessionConfigurationRepository>();
builder.Services.AddScoped<INotificationChannelConfigurationRepository, NotificationChannelConfigurationRepository>();
builder.Services.AddScoped<IAiSettingsConfigurationRepository, AiSettingsConfigurationRepository>();
builder.Services.AddScoped<IRoleDefinitionRepository, RoleDefinitionRepository>();
builder.Services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
builder.Services.AddScoped<IStoredProcedureDefinitionRepository, StoredProcedureDefinitionRepository>();
builder.Services.AddScoped<ITicketStatusDefinitionRepository, TicketStatusDefinitionRepository>();
builder.Services.AddScoped<ITicketRoutingRuleRepository, TicketRoutingRuleRepository>();
builder.Services.AddScoped<ITicketBoardDefinitionRepository, TicketBoardDefinitionRepository>();
builder.Services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IHttpRequestLogRepository, HttpRequestLogRepository>();
builder.Services.AddSingleton<IAccessApprovalService, AccessApprovalService>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<ISlaConfigurationService, SlaConfigurationService>();
builder.Services.AddScoped<IArchiveConfigurationService, ArchiveConfigurationService>();
builder.Services.AddScoped<IArchiveAutomationService, ArchiveAutomationService>();
builder.Services.AddScoped<ISessionConfigurationService, SessionConfigurationService>();
builder.Services.AddScoped<INotificationChannelConfigurationService, NotificationChannelConfigurationService>();
builder.Services.AddScoped<IAiSettingsService, AiSettingsService>();
builder.Services.AddScoped<IRoleDefinitionService, RoleDefinitionService>();
builder.Services.AddScoped<IReportDefinitionService, ReportDefinitionService>();
builder.Services.AddScoped<IStoredProcedureDefinitionService, StoredProcedureDefinitionService>();
builder.Services.AddScoped<ITicketStatusService, TicketStatusService>();
builder.Services.AddScoped<ITicketRoutingRuleService, TicketRoutingRuleService>();
builder.Services.AddScoped<IRoutingRuleHealthService, RoutingRuleHealthService>();
builder.Services.AddScoped<IIntakeLearningService, IntakeLearningService>();
builder.Services.AddScoped<ITicketBoardService, TicketBoardService>();
builder.Services.AddScoped<IScheduledJobService, ScheduledJobService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITicketArchivalService, TicketArchivalService>();
builder.Services.AddScoped<ITicketVisibilityService, TicketVisibilityService>();
builder.Services.AddScoped<IOwnerWorkloadScoringService, OwnerWorkloadScoringService>();
builder.Services.AddScoped<IWorkloadSnapshotService, WorkloadSnapshotService>();
builder.Services.AddScoped<ICortexCandidateResolutionService, CortexCandidateResolutionService>();
builder.Services.AddScoped<ICortexDecisionService, CortexDecisionService>();
builder.Services.AddScoped<ICortexAiAssessmentService, CortexAiAssessmentService>();
builder.Services.AddSingleton<IAiOutputSanitizer, AiOutputSanitizer>();
builder.Services.AddHttpClient<ICortexEmbeddingService, CortexEmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ICortexMemoryFeedbackService, CortexMemoryFeedbackService>();
builder.Services.AddScoped<ITicketOutcomeService, TicketOutcomeService>();
builder.Services.AddScoped<ICortexLearningService, CortexLearningService>();
builder.Services.AddHttpClient<ICortexInsightService, CortexInsightService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddScoped<IOwnerWorkloadPreviewService, OwnerWorkloadPreviewService>();
builder.Services.AddScoped<IOperationalRiskService, OperationalRiskService>();
builder.Services.AddScoped<IReassignmentRecommendationService, ReassignmentRecommendationService>();
builder.Services.AddScoped<IReassignmentExecutionService, ReassignmentExecutionService>();
builder.Services.AddScoped<IDecisionImpactService, DecisionImpactService>();
builder.Services.Configure<CortexAutonomyOptions>(builder.Configuration.GetSection(CortexAutonomyOptions.SectionName));
builder.Services.AddScoped<ICortexAutonomySettingsService, CortexAutonomySettingsService>();
builder.Services.AddScoped<ICortexAutonomyService, CortexAutonomyService>();
builder.Services.AddScoped<ICortexSlaRiskService, CortexSlaRiskService>();
builder.Services.AddScoped<IRebalanceOverviewService, RebalanceOverviewService>();
builder.Services.AddScoped<ITicketAuditService, TicketAuditService>();
builder.Services.AddScoped<IDatabaseProgrammabilityService, DatabaseProgrammabilityService>();
builder.Services.AddScoped<IResponseMappingContextFactory, ResponseMappingContextFactory>();
builder.Services.AddScoped<IRealtimeAudienceResolver, RealtimeAudienceResolver>();
builder.Services.AddScoped<IWorkflowMetricsService, WorkflowMetricsService>();
builder.Services.AddHttpClient<INotificationDeliveryService, NotificationDeliveryService>();
builder.Services.Configure<EmailNotificationOptions>(builder.Configuration.GetSection("Notifications:Email"));
builder.Services.Configure<TeamsNotificationOptions>(builder.Configuration.GetSection("Notifications:Teams"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionName));
builder.Services.AddHttpClient<ITicketTriageAiService, TicketTriageAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddHttpClient<IRebalanceAiAdvisoryService, RebalanceAiAdvisoryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ITicketTriageVocabularyProvider, TicketTriageVocabularyProvider>();
builder.Services.AddScoped<ITicketIntakeAssistPromptBuilder, TicketIntakeAssistPromptBuilder>();
builder.Services.AddHttpClient<ITicketIntakeAssistAiService, TicketIntakeAssistAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddScoped<IScreenshotInsightPromptBuilder, ScreenshotInsightPromptBuilder>();
builder.Services.AddHttpClient<IScreenshotInsightAiService, ScreenshotInsightAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddScoped<IRepeatIssueAnalyticsService, RepeatIssueAnalyticsService>();
builder.Services.AddHttpClient<IRepeatIssueAiReviewService, RepeatIssueAiReviewService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddSingleton<IRealtimeEventService, RealtimeEventService>();
builder.Services.AddHostedService<ScheduledJobHostedService>();
builder.Services.AddHostedService<SlaNotificationHostedService>();
builder.Services.AddHostedService<OwnerAssignmentRoleIntegrityAuditHostedService>();
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
var defaultDevelopmentOrigins = new[]
{
    "http://localhost:5173",
    "http://localhost:4173"
};

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
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Development only — AllowedOrigins is not set.
            policy.WithOrigins(defaultDevelopmentOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
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
                    && (path.StartsWithSegments("/api/realtime")
                        || path.StartsWithSegments("/api/realtime/hub")))
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

// SignalR: use Azure SignalR Service when a connection string is configured (multi-instance / Container Apps).
// Omit or leave empty for local development — falls back to in-memory scale-out (single process).
var azureSignalRConnectionString =
    builder.Configuration["Azure:SignalR:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("AzureSignalR");

var useAzureSignalR = !string.IsNullOrWhiteSpace(azureSignalRConnectionString);

if (useAzureSignalR)
{
    builder.Services.AddSignalR().AddAzureSignalR(options =>
    {
        options.ConnectionString = azureSignalRConnectionString;
    });
}
else
{
    builder.Services.AddSignalR();
}

// Build app
var app = builder.Build();

var realtimeLogger = app.Logger;
if (useAzureSignalR)
{
    realtimeLogger.LogInformation(
        "Realtime: Azure SignalR Service is enabled (multi-instance safe).");
}
else
{
    realtimeLogger.LogInformation(
        "Realtime: using in-process SignalR (set Azure__SignalR__ConnectionString or ConnectionStrings__AzureSignalR for Azure SignalR Service).");
}

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
app.UseRateLimiter();

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
app.MapAiSettingsEndpoints();
app.MapAiEndpoints();
app.MapRoleDefinitionEndpoints();
app.MapReportDefinitionEndpoints();
app.MapMetricsEndpoints();
app.MapWorkloadEndpoints();
app.MapRebalanceEndpoints();
app.MapRepeatIssueEndpoints();
app.MapAdminLogEndpoints();
app.MapStoredProcedureDefinitionEndpoints();
app.MapTicketStatusEndpoints();
app.MapTicketRoutingRuleEndpoints();
app.MapTicketBoardEndpoints();
app.MapIntegrationEndpoints();
app.MapSapReferenceEndpoints();
app.MapReferenceCatalogEndpoints();
app.MapScheduledJobEndpoints();
app.MapNotificationEndpoints();
app.MapSystemEndpoints();
app.MapSystemAutonomyEndpoints();
app.MapRealtimeEndpoints();
app.MapHub<RealtimeHub>("/api/realtime/hub").RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    var db = scope.ServiceProvider.GetRequiredService<CortexDbContext>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        StartupDatabaseResilience.LogStartupDatabaseFailure(
            ex,
            logger,
            operation: "EF Core Migrate");

        throw; // CRITICAL: fail fast if schema is wrong
    }

    if (app.Environment.IsDevelopment())
    {
        try
        {
            await Cortex.API.Infrastructure.SapReferenceDevCatalogSeed.EnsureAsync(db);
            await Cortex.API.Infrastructure.SynitiKnowledgeDevCatalogSeed.EnsureAsync(db);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Development reference catalog seeds (SAP / Syniti) skipped or failed.");
        }
    }
}

using (var scope = app.Services.CreateScope())
{
    var ensureDefaultsLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var ticketBoardService = scope.ServiceProvider.GetRequiredService<ITicketBoardService>();
    try
    {
        await ticketBoardService.EnsureDefaultsAsync();
    }
    catch (Exception exception)
    {
        StartupDatabaseResilience.LogStartupDatabaseFailure(
            exception,
            ensureDefaultsLogger,
            operation: "Ensure default ticket boards");
    }
}

var enableDemoEligibilityBootstrap =
    app.Environment.IsDevelopment()
    || app.Configuration.GetValue<bool>("Demo:EnableEligibilityBootstrap");
if (enableDemoEligibilityBootstrap)
{
    using var scope = app.Services.CreateScope();
    var bootstrapLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    var demoEligibilityBootstrapService = scope.ServiceProvider
        .GetRequiredService<IDemoEligibilityBootstrapService>();
    try
    {
        await demoEligibilityBootstrapService.EnsureDemoEligibilityAsync();
    }
    catch (Exception exception)
    {
        bootstrapLogger.LogWarning(
            exception,
            "Demo owner-eligibility bootstrap failed; continuing startup.");
    }
}

app.Run();
