using System.Net;
using System.Text.RegularExpressions;
using Justloccit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class DeleteTimeslotsByDate
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<DeleteTimeslotsByDate> _logger;

        public DeleteTimeslotsByDate(ICosmosDbService cosmosDbService, ILogger<DeleteTimeslotsByDate> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Deletes a timeslot document for a specific date
        /// </summary>
        /// <param name="req">HTTP request</param>
        /// <param name="date">Date in format YYYY-MM-DD</param>
        [Function("DeleteTimeslotsByDate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "timeslots/{date}")] HttpRequestData req,
            string date)
        {
            _logger.LogInformation("Deleting timeslot for date: {Date}", date);

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

                // Check if document exists
                var existingTimeslot = await _cosmosDbService.GetTimeslotAsync(date, partitionKey);
                if (existingTimeslot == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Timeslot with date {date} not found");
                    return notFoundResponse;
                }

                // Delete document
                await _cosmosDbService.DeleteTimeslotAsync(date, partitionKey);

                // Return success response
                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting timeslot for date {Date}", date);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error deleting timeslot: " + ex.Message);
                return errorResponse;
            }
        }
    }
}
