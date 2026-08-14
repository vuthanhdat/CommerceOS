using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Infrastructure;
using CommerceOS.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformModule();
builder.Services.AddProblemDetails();
builder.Services.AddOnboardingServices(builder.Configuration);
builder.Services.AddMerchantContextServices(builder.Configuration);
builder.Services.AddStorefrontDeliveryServices(builder.Configuration);
builder.Services.AddMerchantCatalogServices(builder.Configuration);
builder.Services.AddProductDataIngestionServices(builder.Configuration);
builder.Services.AddMerchantCustomerPricingServices(builder.Configuration);
builder.Services.AddMerchantProcurementServices(builder.Configuration);
builder.Services.AddMerchantAccountingServices(builder.Configuration);
builder.Services.AddMerchantReportingServices(builder.Configuration);
builder.Services.AddMerchantSettingsServices(builder.Configuration);
builder.Services.AddPlatformSupportServices(builder.Configuration);

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
app.MapMerchantContextEndpoints();
app.MapStorefrontDeliveryEndpoints();
app.MapMerchantCatalogEndpoints();
app.MapProductDataIngestionEndpoints();
app.MapMerchantSalesEndpoints();
app.MapMerchantInventoryEndpoints();
app.MapMerchantCustomerPricingEndpoints();
app.MapMerchantProcurementEndpoints();
app.MapMerchantAccountingEndpoints();
app.MapMerchantReportingEndpoints();
app.MapMerchantSettingsEndpoints();
app.MapPlatformSupportEndpoints();

app.Run();

public partial class Program;

