using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformModule();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapGet(
        "/health",
        (IPlatformReadiness readiness) => Results.Ok(readiness.GetSnapshot()))
    .AllowAnonymous()
    .WithName("GetHealth")
    .WithTags("Platform");

app.Run();

public partial class Program;

