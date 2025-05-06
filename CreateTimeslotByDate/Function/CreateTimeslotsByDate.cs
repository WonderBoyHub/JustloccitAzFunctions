using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Justloccit.Data;
using Justloccit.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class CreateTimeslotsByDate
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<CreateTimeslotsByDate> _logger;

        public CreateTimeslotsByDate(ICosmosDbService cosmosDbService, ILogger<CreateTimeslotsByDate> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new timeslot document for a specific date
        /// </summary>
        /// <param name="req">HTTP request containing timeslot information</param>
        /// <param name="date">Date in format YYYY-MM-DD</param>
        [Function("CreateTimeslotsByDate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "timeslots/{date}")] HttpRequestData req,
            string date)
        {
            _logger.LogInformation("Creating timeslot for date: {Date}", date);

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
                
                // Check if document already exists
                var existingTimeslot = await _cosmosDbService.GetTimeslotAsync(date, partitionKey);
                if (existingTimeslot != null)
                {
                    var conflictResponse = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflictResponse.WriteStringAsync($"Timeslot document for date {date} already exists");
                    return conflictResponse;
                }

                // Parse request body for timeslot details or use default values
                TimeslotDocument timeslotDocument;
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                
                if (!string.IsNullOrEmpty(requestBody))
                {
                    // If body is provided, try to deserialize it
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        timeslotDocument = JsonSerializer.Deserialize<TimeslotDocument>(requestBody, options);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing request body");
                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequestResponse.WriteStringAsync("Invalid request format: " + ex.Message);
                        return badRequestResponse;
                    }
                }
                else
                {
                    // Create default timeslot document
                    timeslotDocument = new TimeslotDocument();
                }

                // Set the required properties
                timeslotDocument = timeslotDocument with 
                {
                    Id = date,
                    Date = date,
                    PartitionKey = partitionKey,
                    IsAvailable = true
                };

                // If no timeslots were provided, generate default timeslots (every 30 minutes from 9:00 to 17:00)
                if (timeslotDocument.TimeSlots.Count == 0)
                {
                    var timeSlots = new List<TimeSlot>();
                    
                    // Start at 9:00 and end at 17:00
                    for (int hour = 9; hour < 17; hour++)
                    {
                        for (int minute = 0; minute < 60; minute += 30)
                        {
                            int totalMinutes = hour * 60 + minute;
                            var displayTime = $"{hour:D2}:{minute:D2}";
                            
                            var timeInfo = new TimeInfo
                            {
                                Hours = hour,
                                Minutes = minute,
                                TotalMinutes = totalMinutes,
                                DisplayTime = displayTime
                            };
                            
                            timeSlots.Add(new TimeSlot
                            {
                                Time = timeInfo,
                                IsAvailable = true,
                                Hours = hour,
                                Minutes = minute,
                                TotalMinutes = totalMinutes,
                                DisplayTime = displayTime,
                                BookedBy = string.Empty,
                                BookingId = string.Empty,
                                SubServiceId = string.Empty
                            });
                        }
                    }
                    
                    timeslotDocument = timeslotDocument with { TimeSlots = timeSlots };
                }

                // Create the document in the database
                var createdDocument = await _cosmosDbService.CreateTimeslotAsync(timeslotDocument, partitionKey);

                // Return success response with the created document
                var response = req.CreateResponse(HttpStatusCode.Created);
                await response.WriteAsJsonAsync(createdDocument);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating timeslot for date {Date}: {Message}", date, ex.Message);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error creating timeslot: " + ex.Message);
                return errorResponse;
            }
        }
    }
}
