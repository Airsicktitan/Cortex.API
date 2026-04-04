using Microsoft.EntityFrameworkCore;
using Cortex.API.Extensions;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Cortex.API.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AzureCortexDB")
    ));


// Add services
builder.Services.AddEndpointsApiExplorer(); // for minimal APIs, needed for Swagger
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserContextService, UserContextService>();
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
    });

builder.Services.AddAuthorization(options =>
{
    // Broad admin/system policy
    options.AddPolicy("AdminSystem", policy =>
        policy.RequireClaim("permissions", "admin:system"));

    // Role-based policies if needed
    options.AddPolicy("DeveloperRole", policy =>
        policy.RequireClaim("https://cortex-api/roles", "Developer"));

    // Ticket permissions
    options.AddPolicy("TicketsRead", policy =>
        policy.RequireClaim("permissions", "tickets:read", "admin:system"));

    options.AddPolicy("TicketsCreate", policy =>
        policy.RequireClaim("permissions", "tickets:create", "admin:system"));

    options.AddPolicy("TicketsUpdate", policy =>
        policy.RequireClaim("permissions", "tickets:update", "admin:system"));

    options.AddPolicy("TicketsDelete", policy =>
        policy.RequireClaim("permissions", "tickets:delete", "admin:system"));

    // Comment permissions
    options.AddPolicy("CommentsRead", policy =>
        policy.RequireClaim("permissions", "comments:read", "admin:system"));

    options.AddPolicy("CommentsCreate", policy =>
        policy.RequireClaim("permissions", "comments:create", "admin:system"));

    // User permissions
    options.AddPolicy("UsersRead", policy =>
        policy.RequireClaim("permissions", "users:read", "admin:system"));

    options.AddPolicy("UsersUpdate", policy =>
        policy.RequireClaim("permissions", "users:update", "admin:system"));
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
app.MapUserEndpoints();
app.MapCommentEndpoints();
app.MapClaimEndpoint();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CortexDbContext>();
    db.Database.Migrate();
}

app.Run();