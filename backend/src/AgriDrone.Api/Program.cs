using AgriDrone.Modules.Farms;
using AgriDrone.Modules.FieldTasks;
using AgriDrone.Modules.Harvests;
using AgriDrone.Modules.Identity;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Notifications;
using AgriDrone.Modules.Plants;
using AgriDrone.SharedInfrastructure.ExceptionHandling;
using AgriDrone.SharedInfrastructure.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddFarmsModule()
    .AddFieldTasksModule()
    .AddHarvestsModule()
    .AddIdentityModule()
    .AddMissionsModule()
    .AddNotificationsModule()
    .AddPlantsModule()
    .AddValidationPipeline()
    .AddGlobalExceptionHandling();

var app = builder.Build();

app.UseExceptionHandler();

app.Run();
