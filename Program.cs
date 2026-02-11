using Microsoft.EntityFrameworkCore;
using Cortex.API.Extensions;
using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.Data.Repositories;

using Microsoft.AspNetCore.Authentication.JwtBearer;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<CortexDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("CortexDb")
    ));


// Add services
builder.Services.AddEndpointsApiExplorer(); // for minimal APIs, needed for Swagger
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();


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
            Array.Empty<string>()
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
            options.Authority = builder.Configuration["Auth0:Domain"];
            options.Audience = builder.Configuration["Auth0:Audience"];
            options.TokenValidationParameters.ValidAudience = builder.Configuration["Auth0:Audience"];
        
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

app.Run();