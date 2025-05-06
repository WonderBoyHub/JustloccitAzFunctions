using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Justloccit.Models;
using Justloccit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class UpdateTimeslotsByDate
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<UpdateTimeslotsByDate> _logger;

        public UpdateTimeslotsByDate(ICosmosDbService cosmosDbService, ILogger<UpdateTimeslotsByDate> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Updates a timeslot document for a specific date
        /// </summary>
        /// <param name="req">HTTP request containing the partial timeslot document update</param>
        /// <param name="date">Date in format YYYY-MM-DD</param>
        [Function("UpdateTimeslotsByDate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "timeslots/{date}")] HttpRequestData req,
            string date)
        {
            _logger.LogInformation("Updating timeslot for date: {Date}", date);

            // Validate date format (YYYY-MM-DD)
            if (!Regex.IsMatch(date, @"^\d{4}-\d{2}-\d{2}$"))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Date must be in format YYYY-MM-DD");
                return badRequestResponse;
            }

            try
            {
                // Parse request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var updateData = JsonSerializer.Deserialize<TimeslotDocument>(requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (updateData == null)
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid request body");
                    return badRequestResponse;
                }

                // Calculate partition key from date
                var partitionKey = date.Substring(0, 7); // YYYY-MM

                // Retrieve existing document
                var existingTimeslot = await _cosmosDbService.GetTimeslotAsync(date, partitionKey);
                if (existingTimeslot == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"Timeslot with date {date} not found");
                    return notFoundResponse;
                }

                // Apply updates (partial update)
                var updatedTimeslot = existingTimeslot with
                {
                    TimeSlots = updateData.TimeSlots.Count > 0 ? updateData.TimeSlots : existingTimeslot.TimeSlots,
                    IsAvailable = updateData.IsAvailable,
                    SpecialNotes = !string.IsNullOrEmpty(updateData.SpecialNotes) ? updateData.SpecialNotes : existingTimeslot.SpecialNotes
                };

                // Save updated document
                var savedTimeslot = await _cosmosDbService.UpdateTimeslotAsync(updatedTimeslot);

                // Return success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(savedTimeslot);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timeslot for date {Date}", date);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error updating timeslot: " + ex.Message);
                return errorResponse;
            }
        }
    }
}
