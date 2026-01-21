using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StarApi.Data;
using StarApi.Services;
using StarApi.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Linq;
using StarApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register your services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Add SignalR with JWT authentication
builder.Services.AddSignalR();

// Configure file upload settings
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB max for file uploads
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

// Configure file storage settings from appsettings.json
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Add controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StarApi",
        Version = "v1",
        Description = "API for user management with authentication and profile features"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JWT Authentication
var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing in configuration.");
var keyBytes = Encoding.UTF8.GetBytes(key);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.Zero // Remove default 5 minute tolerance
        };

        // For debugging purposes
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                Console.WriteLine($"Request Path: {context.HttpContext.Request.Path}");
                Console.WriteLine($"Authorization Header: {context.HttpContext.Request.Headers["Authorization"]}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"Token validated for user: {context.Principal?.Identity?.Name}");
                var claims = context.Principal?.Claims?.Select(c => $"{c.Type}:{c.Value}") ?? Enumerable.Empty<string>();
                Console.WriteLine($"User claims: {string.Join(", ", claims)}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/chat")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };

        // Configure JWT for SignalR
        options.SaveToken = true;
    });

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins(
                "http://localhost:4200",  // Angular dev server
                "http://localhost:3000",  // React dev server
                "https://localhost:4200", // HTTPS Angular
                "https://localhost:3000"  // HTTPS React
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition"); // For file downloads
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "StarApi v1");
        c.RoutePrefix = "swagger"; // Access Swagger at /swagger
    });

    // Enable detailed error pages in development
    app.UseDeveloperExceptionPage();
}

// Create uploads directory if it doesn't exist
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads", "avatars");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"Created uploads directory: {uploadsPath}");
}

// Serve static files from wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads")),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        // Cache static files (images) for 30 days
        ctx.Context.Response.Headers.Append(
            "Cache-Control", "public, max-age=2592000");
    }
});

// Middleware order is important!
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Add SignalR hub mapping - use separate hub class
app.MapHub<ChatHub>("/api/hubs/chat");

// Add test endpoint to verify JWT
app.MapGet("/test-auth", (HttpContext context) => {
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        return Results.Ok(new { 
            authenticated = true, 
            username = user.Identity.Name,
            userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        });
    }
    else
    {
        return Results.Unauthorized();
    }
}).RequireAuthorization();

// Global exception handling middleware
app.UseExceptionHandler("/error");

// Fallback for unhandled routes
app.Map("/", () => Results.Redirect("/swagger"));

// Health check endpoint
app.MapGet("/health", () =>
{
    var dbStatus = "Unknown";
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbStatus = dbContext.Database.CanConnect() ? "Healthy" : "Unhealthy";
    }
    catch (Exception ex)
    {
        dbStatus = $"Error: {ex.Message}";
    }

    return Results.Ok(new
    {
        status = "API is running",
        timestamp = DateTime.UtcNow,
        database = dbStatus,
        environment = app.Environment.EnvironmentName
    });
});

// Error handling endpoint
app.Map("/error", () =>
    Results.Problem("An unexpected error occurred.", statusCode: 500));

Console.WriteLine($"Starting StarApi in {app.Environment.EnvironmentName} environment...");
Console.WriteLine($"Database: {builder.Configuration.GetConnectionString("DefaultConnection")}");

app.Run();
