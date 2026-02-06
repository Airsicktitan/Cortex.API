using Microsoft.EntityFrameworkCore;
using Cortex.API.Extensions;
using Cortex.API.Database;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("CortexDb")
    ));


// Add services
builder.Services.AddEndpointsApiExplorer(); // for minimal APIs, needed for Swagger
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

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

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
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured");
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// Build app
var app = builder.Build();

// Enable Swagger in Dev or Production, probably want to lock this down later
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CORTEX API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CORTEX API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map all ticket endpoints
app.MapRootEndpoint();
app.MapTicketEndpoints();
app.MapUserEndpoints();
app.MapCommentEndpoints();
app.MapAuthEndpoints();

app.Run();