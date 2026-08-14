using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Infrastructure;
using CommerceOS.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformModule();
builder.Services.AddProblemDetails();
builder.Services.AddOnboardingServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapGet(
        "/health",
        (IPlatformReadiness readiness) => Results.Ok(readiness.GetSnapshot()))
    .AllowAnonymous()
    .WithName("GetHealth")
    .WithTags("Platform");

app.MapOnboardingEndpoints();

app.Run();

public partial class Program;

