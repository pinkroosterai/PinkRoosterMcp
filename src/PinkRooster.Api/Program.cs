using System.Text.Json.Serialization;
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
builder.Services.AddSingleton<IEventBroadcaster, EventBroadcaster>();
builder.Services.AddSingleton<ActivityLogChannel>();
builder.Services.AddHostedService<ActivityLogWriterService>();

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
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Dev-only: auto-migrate and enable Swagger
if (app.Environment.IsDevelopment())
{
    await DbInitializer.InitializeAsync(app.Services);
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware pipeline
app.UseCors();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program;

