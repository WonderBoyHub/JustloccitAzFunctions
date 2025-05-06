using System.Net;
using System.Text.RegularExpressions;
using Justloccit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class GetTimeslotsByDate
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<GetTimeslotsByDate> _logger;

        public GetTimeslotsByDate(ICosmosDbService cosmosDbService, ILogger<GetTimeslotsByDate> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a timeslot document for a specific date
        /// </summary>
        /// <param name="req">HTTP request</param>
        /// <param name="date">Date in format YYYY-MM-DD</param>
        [Function("GetTimeslotsByDate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "timeslots/{date}")] HttpRequestData req,
            string date)
        {
            _logger.LogInformation("Getting timeslot for date: {Date}", date);

            // Validate date format (YYYY-MM-DD)
            if (!Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}$"))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Date must be in format YYYY-MM-DD");
                return badRequestResponse;
            }

            try
            {
                // Calculate partition key from date
                var partitionKey = date.Substring(0, 7); // YYYY-MM

                // Retrieve document
                var timeslot = await _cosmosDbService.GetTimeslotAsync(date, partitionKey);
                
                if (timeslot == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Timeslot with date {date} not found");
                    return notFoundResponse;
                }

                // Return success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(timeslot);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving timeslot for date {Date}", date);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error retrieving timeslot: " + ex.Message);
                return errorResponse;
            }
        }
    }
}
