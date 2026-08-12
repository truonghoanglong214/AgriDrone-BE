using AgriDrone.SharedInfrastructure.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGlobalExceptionHandling();

var app = builder.Build();

app.UseExceptionHandler();

app.Run();
