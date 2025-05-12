using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UpdateBooking.Function.Data;
using Microsoft.Azure.Cosmos;
using System;
using Azure.Messaging.EventGrid;
using Azure;

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

// Register EventGridPublisherClient as a singleton
builder.Services.AddSingleton(sp =>
{
    var eventGridEndpoint = Environment.GetEnvironmentVariable("EventGridEndpoint");
    var eventGridKey = Environment.GetEnvironmentVariable("EventGridKey");
    
    if (string.IsNullOrEmpty(eventGridEndpoint) || string.IsNullOrEmpty(eventGridKey))
    {
        throw new InvalidOperationException("EventGrid settings are not configured. Please add 'EventGridEndpoint' and 'EventGridKey' to the configuration.");
    }
    
    return new EventGridPublisherClient(
        new Uri(eventGridEndpoint),
        new AzureKeyCredential(eventGridKey));
});

// Register HttpClient as a singleton
builder.Services.AddHttpClient();

// Register CosmosDbService as a singleton
builder.Services.AddSingleton<CosmosDbService>();

var app = builder.Build();
app.Run();
