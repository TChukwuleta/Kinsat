using Kinsat.Api.Services;
using Kinsat.Core.Configuration;
using Kinsat.Core.Events;
using Kinsat.Core.Identity;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Events;
using Kinsat.Sdk.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IIdentityProvider, StandaloneIdentityProvider>();
builder.Services.AddSingleton<IConfigResolver, ConfigurationEngine>();
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// The seam for continuous LTV monitoring (see Services/LtvMonitorService.cs).
builder.Services.AddHostedService<LtvMonitorService>();

var app = builder.Build();

app.UseHttpsRedirection();

// Confirms the host is up and the core contracts resolved through DI correctly.
// Replace with real endpoints as the loan lifecycle, custody, and scoring land.
app.MapGet("/health", (IConfigResolver config, IEventBus events, IIdentityProvider identity) =>
    Results.Ok(new
    {
        status = "ok",
        configResolver = config.GetType().Name,
        eventBus = events.GetType().Name,
        identityProvider = identity.GetType().Name
    }));

app.Run();
