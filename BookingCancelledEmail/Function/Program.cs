using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Communication.Email;
using System;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register Azure Communication Email client
builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("AzureCommunicationServicesConnectionString");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Azure Communication Services connection string is not configured. " +
                                           "Please add 'AzureCommunicationServicesConnectionString' to the configuration.");
    }
    return new EmailClient(connectionString);
});

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
