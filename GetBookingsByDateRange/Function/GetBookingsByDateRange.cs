using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GetBookingsByDateRange.Function.Models;
using GetBookingsByDateRange.Function.Data;
using System.Threading;
using System.Linq;
using System.Text;

namespace Justloccit.Function
{
    public class GetBookingsByDateRange
    {
        private readonly ILogger<GetBookingsByDateRange> _logger;
        private readonly CosmosDbService _cosmosDbService;

        public GetBookingsByDateRange(ILogger<GetBookingsByDateRange> logger, CosmosDbService cosmosDbService)
        {
            _logger = logger;
            _cosmosDbService = cosmosDbService;
        }

        [Function("GetBookingsByDateRange")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "getbookingsbydaterange")] HttpRequest req,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetBookingsByDateRange function processing request");

            try
            {
                GetBookingsByDateRangeRequest request;
                
                // Check if the request is GET or POST
                if (HttpMethods.IsGet(req.Method))
                {
                    // Get the parameters from query string
                    if (!DateTime.TryParse(req.Query["startDate"], out var startDate) ||
                        !DateTime.TryParse(req.Query["endDate"], out var endDate))
                    {
                        return new BadRequestObjectResult(new GetBookingsByDateRangeResponse 
                        { 
                            Success = false, 
                            Message = "Valid startDate and endDate are required" 
                        });
                    }

                    request = new GetBookingsByDateRangeRequest
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        CustomerId = req.Query["customerId"],
                        ServiceId = req.Query["serviceId"],
                        SubServiceId = req.Query["subServiceId"]
                    };
                }
                else
                {
                    // Read and deserialize the request body for POST
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
                    request = JsonConvert.DeserializeObject<GetBookingsByDateRangeRequest>(requestBody);
                    
                    if (request == null)
                    {
                        return new BadRequestObjectResult(new GetBookingsByDateRangeResponse 
                        { 
                            Success = false, 
                            Message = "Invalid request format" 
                        });
                    }
                }
                
                // Validate request
                if (request.StartDate > request.EndDate)
                {
                    return new BadRequestObjectResult(new GetBookingsByDateRangeResponse 
                    { 
                        Success = false, 
                        Message = "StartDate must be earlier than or equal to EndDate" 
                    });
                }
                
                // Build the query
                var queryBuilder = new StringBuilder($"SELECT * FROM c WHERE c.date >= '{request.StartDate:yyyy-MM-dd}' AND c.date <= '{request.EndDate:yyyy-MM-dd}'");
                
                if (!string.IsNullOrEmpty(request.CustomerId))
                {
                    queryBuilder.Append($" AND c.customerId = '{request.CustomerId}'");
                }
                
                if (!string.IsNullOrEmpty(request.ServiceId))
                {
                    queryBuilder.Append($" AND c.serviceId = '{request.ServiceId}'");
                }
                
                if (!string.IsNullOrEmpty(request.SubServiceId))
                {
                    queryBuilder.Append($" AND c.subServiceId = '{request.SubServiceId}'");
                }
                
                // Get the bookings from the database
                var bookings = await _cosmosDbService.GetItemsAsync<BookingModel>("Bookings", queryBuilder.ToString());
                
                // Map the booking entities to booking DTOs
                var bookingDtos = bookings.Select(booking => new BookingModel
                {
                    Id = booking.Id,
                    CustomerId = booking.CustomerId,
                    CustomerName = booking.CustomerName,
                    CustomerEmail = booking.CustomerEmail,
                    CustomerPhone = booking.CustomerPhone,
                    ServiceId = booking.ServiceId,
                    ServiceName = booking.ServiceName,
                    SubServiceId = booking.SubServiceId,
                    SubServiceName = booking.SubServiceName,
                    Date = booking.Date,
                    StartTime = booking.StartTime,
                    EndTime = booking.EndTime,
                    BookingStatus = booking.BookingStatus,
                    Notes = booking.Notes,
                    CreatedAt = booking.CreatedAt,
                    UpdatedAt = booking.UpdatedAt
                }).ToList();
                
                _logger.LogInformation($"Successfully retrieved {bookingDtos.Count} bookings between {request.StartDate:yyyy-MM-dd} and {request.EndDate:yyyy-MM-dd}");
                
                // Return the bookings
                return new OkObjectResult(new GetBookingsByDateRangeResponse
                { 
                    Success = true,
                    Bookings = bookingDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetBookingsByDateRange request");
                return new ObjectResult(new GetBookingsByDateRangeResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving bookings" 
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
