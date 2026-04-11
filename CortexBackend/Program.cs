using Microsoft.EntityFrameworkCore;
using Cortex.API.Extensions;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Cortex.API.Services;


var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("AzureCortexDb")
    ?? builder.Configuration.GetConnectionString("AzureCortexDB")
    ?? builder.Configuration.GetConnectionString("CortexDB")
    ?? throw new InvalidOperationException(
        "Connection string 'AzureCortexDb' is not configured. Set ConnectionStrings:AzureCortexDb or use CortexDB as a fallback.");

builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(connectionString));
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
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ISlaConfigurationRepository, SlaConfigurationRepository>();
builder.Services.AddScoped<IArchiveConfigurationRepository, ArchiveConfigurationRepository>();
builder.Services.AddScoped<ISessionConfigurationRepository, SessionConfigurationRepository>();
builder.Services.AddScoped<IReportDefinitionRepository, ReportDefinitionRepository>();
builder.Services.AddScoped<IStoredProcedureDefinitionRepository, StoredProcedureDefinitionRepository>();
builder.Services.AddScoped<ITicketStatusDefinitionRepository, TicketStatusDefinitionRepository>();
builder.Services.AddScoped<ITicketRoutingRuleRepository, TicketRoutingRuleRepository>();
builder.Services.AddScoped<IScheduledJobRepository, ScheduledJobRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<ISlaConfigurationService, SlaConfigurationService>();
builder.Services.AddScoped<IArchiveConfigurationService, ArchiveConfigurationService>();
builder.Services.AddScoped<IArchiveAutomationService, ArchiveAutomationService>();
builder.Services.AddScoped<ISessionConfigurationService, SessionConfigurationService>();
builder.Services.AddScoped<IReportDefinitionService, ReportDefinitionService>();
builder.Services.AddScoped<IStoredProcedureDefinitionService, StoredProcedureDefinitionService>();
builder.Services.AddScoped<ITicketStatusService, TicketStatusService>();
builder.Services.AddScoped<ITicketRoutingRuleService, TicketRoutingRuleService>();
builder.Services.AddScoped<IScheduledJobService, ScheduledJobService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITicketArchivalService, TicketArchivalService>();
builder.Services.AddScoped<ITicketVisibilityService, TicketVisibilityService>();
builder.Services.AddScoped<ITicketAuditService, TicketAuditService>();
builder.Services.AddScoped<IDatabaseProgrammabilityService, DatabaseProgrammabilityService>();
builder.Services.AddScoped<IResponseMappingContextFactory, ResponseMappingContextFactory>();
builder.Services.AddSingleton<IRealtimeEventService, RealtimeEventService>();
builder.Services.AddHostedService<ScheduledJobHostedService>();
builder.Services.AddHostedService<SlaNotificationHostedService>();
builder.Services.AddHttpContextAccessor(); // Register the built-in IHttpContextAccessor


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

// Add CORS for React Frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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
            RoleClaimType = "role"
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
            }
        };
    });

var permissions = new Dictionary<string, string>
{
    ["TicketsRead"] = "tickets:read",
    ["TicketsCreate"] = "tickets:create",
    ["TicketsUpdate"] = "tickets:update",
    ["TicketsDelete"] = "tickets:delete",
    ["CommentsRead"] = "comments:read",
    ["CommentsCreate"] = "comments:create",
    ["UsersRead"] = "users:read",
    ["UsersUpdate"] = "users:update"
};

builder.Services.AddAuthorization(options =>
{
    // Broad admin/system policy
    options.AddPolicy("AdminSystem", policy =>
        policy.RequireClaim("permissions", "admin"));
    
    options.AddPolicy("DeveloperRole", policy =>
        policy.RequireClaim("permissions", "developer"));

    options.AddPolicy("SlaManage", policy =>
        policy.RequireClaim("permissions", "admin:system", "developer"));

    options.AddPolicy("ReportsAdvanced", policy =>
        policy.RequireClaim("permissions", "admin:system", "developer"));

    options.AddPolicy("UsersAdminRead", policy =>
        policy.RequireClaim("permissions", "admin:system", "developer"));

    options.AddPolicy("UsersCreate", policy =>
        policy.RequireClaim("permissions", "admin:system", "developer"));

    options.AddPolicy("UsersAdminUpdate", policy =>
        policy.RequireClaim("permissions", "admin:system"));

    options.AddPolicy("UsersAdminDelete", policy =>
        policy.RequireClaim("permissions", "admin:system"));

    options.AddPolicy("TicketsWrite", policy =>
        policy.RequireClaim("permissions", "tickets:create", "tickets:update", "admin:system"));

    
    // Specific policies for users with granular permissions
    foreach (var (name, permission) in permissions)
    {
        options.AddPolicy(name, policy =>
            policy.RequireClaim("permissions", permission, "admin:system"));
    }
});

// Build app
var app = builder.Build();

// Enable Swagger in Dev or Production, probably want to lock this down later
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

app.UseCors();

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

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
app.MapReportDefinitionEndpoints();
app.MapStoredProcedureDefinitionEndpoints();
app.MapTicketStatusEndpoints();
app.MapTicketRoutingRuleEndpoints();
app.MapScheduledJobEndpoints();
app.MapNotificationEndpoints();
app.MapRealtimeEndpoints();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CortexDbContext>();
    db.Database.Migrate();
}

app.Run();
