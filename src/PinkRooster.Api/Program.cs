using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PinkRooster.Api.Middleware;
using PinkRooster.Api.Services;
using PinkRooster.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IIssueService, IssueService>();
builder.Services.AddScoped<IFeatureRequestService, FeatureRequestService>();
builder.Services.AddScoped<IWorkPackageService, WorkPackageService>();
builder.Services.AddScoped<IPhaseService, PhaseService>();
builder.Services.AddScoped<IWorkPackageTaskService, WorkPackageTaskService>();
builder.Services.AddScoped<IStateCascadeService, StateCascadeService>();
builder.Services.AddScoped<IProjectMemoryService, ProjectMemoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectRoleService, ProjectRoleService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<IEventBroadcaster, EventBroadcaster>();
builder.Services.AddSingleton<ActivityLogChannel>();
builder.Services.AddHostedService<ActivityLogWriterService>();
builder.Services.AddHostedService<SessionCleanupService>();

// Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for dashboard
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Rate limiting (disabled in Testing environment for integration tests)
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter("auth-login", opt =>
        {
            opt.PermitLimit = 5;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });

        options.AddFixedWindowLimiter("auth-register", opt =>
        {
            opt.PermitLimit = 3;
            opt.Window = TimeSpan.FromHours(1);
            opt.QueueLimit = 0;
        });

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });
}

var app = builder.Build();

// Auto-migrate: enabled in Development, or via AUTO_MIGRATE=true env var
var autoMigrate = app.Environment.IsDevelopment()
    || string.Equals(app.Configuration["AUTO_MIGRATE"], "true", StringComparison.OrdinalIgnoreCase);
if (autoMigrate)
    await DbInitializer.InitializeAsync(app.Services);

// Swagger: enabled in Development, or via ENABLE_SWAGGER=true env var
var enableSwagger = app.Environment.IsDevelopment()
    || string.Equals(app.Configuration["ENABLE_SWAGGER"], "true", StringComparison.OrdinalIgnoreCase);
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global exception handler for non-Development (suppresses stack traces)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"An internal error occurred."}""");
        });
    });
}

// Middleware pipeline
app.UseCors();
if (!app.Environment.IsEnvironment("Testing"))
    app.UseRateLimiter();
app.UseMiddleware<SessionAuthMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<ProjectAuthorizationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program;

