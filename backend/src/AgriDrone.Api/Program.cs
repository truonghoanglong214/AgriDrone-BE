using AgriDrone.Database;
using AgriDrone.Integrations.Email;
using AgriDrone.Modules.Farms;
using AgriDrone.Modules.FieldTasks;
using AgriDrone.Modules.Harvests;
using AgriDrone.Modules.Identity;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Notifications;
using AgriDrone.Modules.Plants;
using AgriDrone.SharedInfrastructure.Authentication;
using AgriDrone.SharedInfrastructure.ExceptionHandling;
using AgriDrone.SharedInfrastructure.Execution;
using AgriDrone.SharedInfrastructure.Health;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Validation;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(document =>
    new OpenApiSecurityRequirement
    {
        [
            new OpenApiSecuritySchemeReference(
                "Bearer",
                document)
        ] = []
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Modules
builder.Services
    .AddEmailIntegration(builder.Configuration)
    .AddFarmsModule(builder.Configuration)
    .AddFieldTasksModule(builder.Configuration)
    .AddHarvestsModule(builder.Configuration)
    .AddIdentityModule(builder.Configuration)
    .AddMissionsModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddPlantsModule(builder.Configuration)
    .AddIntegrationMessagingFoundation(builder.Configuration)
    .AddMappingPublicationPersistence(builder.Configuration)
    .AddAgriDroneHealthChecks()
    .AddExecutionContext()
    .AddJwtAuthentication(builder.Configuration)
    .AddValidationPipeline()
    .AddGlobalExceptionHandling();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseExecutionContext();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    });

app.Run();
