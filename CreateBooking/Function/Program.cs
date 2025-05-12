using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CreateBooking.Function.Data;
using Microsoft.Azure.Cosmos;
using System;

var builder = FunctionsApplication.CreateBuilder(args);

// Enable ASP.NET Core integration for HTTP triggers (CORS, etc.)
builder.ConfigureFunctionsWebApplication();

// Register CosmosClient as a singleton
builder.Services.AddSingleton(sp => 
{
    var connectionString = builder.Configuration["CosmosDBConnectionString"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("CosmosDB connection string is not configured. Please add 'CosmosDBConnectionString' to the configuration.");
    }
    return new CosmosClient(connectionString);
});

// Register HttpClient as a singleton
builder.Services.AddHttpClient();

// Register CosmosDbService as a singleton
builder.Services.AddSingleton<CosmosDbService>();

var app = builder.Build();
app.Run();
