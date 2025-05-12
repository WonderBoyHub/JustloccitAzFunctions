using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using System;
using Justloccit.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register CosmosClient as a singleton
builder.Services.AddSingleton(sp => 
{
    var connectionString = builder.Configuration["CosmosDb:ConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("CosmosDB connection string is not configured. Please add 'CosmosDb:ConnectionString' to the configuration.");
    }
    return new CosmosClient(connectionString);
});

// Register CosmosDbService as a singleton
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
