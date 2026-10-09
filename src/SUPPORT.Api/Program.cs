using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using SUPPORT.Api.Middleware;
using SUPPORT.Api.Services;
using SUPPORT.Api.Settings;
using SUPPORT.Application;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Common.Settings;
using SUPPORT.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

builder.Services.Configure<ChatSettings>(builder.Configuration.GetSection(ChatSettings.SectionName));
builder.Services.Configure<List<InternalClientSettings>>(builder.Configuration.GetSection(InternalClientSettings.SectionName));

builder.Services.AddSupportApplication();
builder.Services.AddSupportInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICallerContext, HttpCallerContext>();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await app.Services.MigrateSupportDatabaseAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Before routing to controllers so /internal/* is rejected without running any endpoint code.
app.UseMiddleware<InternalClientAuthMiddleware>();

// live = the process answers; ready = PostgreSQL answers too. Neither calls Gemini, so probes never spend quota.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();

app.Run();
