using Justloccit.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Register CosmosClient as a singleton
        services.AddSingleton(s =>
        {
            var connectionString = context.Configuration["CosmosDb:ConnectionString"] 
                ?? throw new InvalidOperationException("Cosmos DB connection string is not configured.");
            return new CosmosClient(connectionString);
        });
        
        // Register CosmosDbService
        services.AddSingleton<ICosmosDbService, CosmosDbService>();
    })
    .Build();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

host.Run();
